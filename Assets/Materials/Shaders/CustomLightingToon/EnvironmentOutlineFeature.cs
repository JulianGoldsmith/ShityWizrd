using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

public class EnvironmentOutlineFeature : ScriptableRendererFeature
{
    class OutlinePass : ScriptableRenderPass
    {
        public Material material;

        public OutlinePass(Material mat)
        {
            material = mat;
            renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
        }

        private class PassData
        {
            public TextureHandle readTex; // This is our JFA Buffer
            public TextureHandle writeTex;
            public TextureHandle sceneTex; // ADD THIS
            public Material material;
            public float jfaStep;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            TextureHandle srcCamColor = resourceData.activeColorTexture;
            if (!srcCamColor.IsValid()) return;

            // 16-bit Float buffers are strictly required to store exact X/Y pixel coordinates
            var sdfDesc = cameraData.cameraTargetDescriptor;
            sdfDesc.msaaSamples = 1;
            sdfDesc.depthBufferBits = 0;
            // Change this line from R16 to R32!
            sdfDesc.graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat;

            TextureHandle rta = UniversalRenderer.CreateRenderGraphTexture(renderGraph, sdfDesc, "_JFABufferA", false);
            TextureHandle rtb = UniversalRenderer.CreateRenderGraphTexture(renderGraph, sdfDesc, "_JFABufferB", false);

            // ==========================================
            // PASS 0: INITIAL SEED (Outputs to rta)
            // ==========================================
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("JFA Init Seed", out var passData))
            {
                passData.material = material;
                passData.readTex = srcCamColor;

                builder.UseTexture(srcCamColor, AccessFlags.Read);
                builder.SetRenderAttachment(rta, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.readTex, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // ==========================================
            // PASS 1: JUMP FLOOD LOOP (Ping-Pong)
            // ==========================================
            TextureHandle currentRead = rta;
            TextureHandle currentWrite = rtb;
            int[] jumpSteps = { 16, 8, 4, 2, 1 };

            foreach (int step in jumpSteps)
            {
                int capturedStep = step;
                using (var builder = renderGraph.AddRasterRenderPass<PassData>($"JFA Flood Step {capturedStep}", out var passData))
                {
                    passData.material = material;
                    passData.readTex = currentRead;
                    passData.writeTex = currentWrite;
                    passData.jfaStep = capturedStep;

                    // Allow the Command Buffer to set global shader properties
                    builder.AllowGlobalStateModification(true);

                    builder.UseTexture(currentRead, AccessFlags.Read);
                    builder.SetRenderAttachment(currentWrite, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalFloat("_JFA_Step", data.jfaStep);
                        context.cmd.SetGlobalTexture("_JFABuffer", data.readTex);
                        Blitter.BlitTexture(context.cmd, data.readTex, new Vector4(1, 1, 0, 0), data.material, 1);
                    });
                }

                // Swap buffers for the next jump
                TextureHandle temp = currentRead;
                currentRead = currentWrite;
                currentWrite = temp;
            }

            // ==========================================
            // PASS 2: COMPOSITE (Draws Scene + Outlines)
            // ==========================================
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("JFA Composite", out var passData))
            {
                passData.material = material;
                passData.readTex = currentRead;      // The final JFA Buffer
                passData.sceneTex = srcCamColor;     // The original scene color

                builder.AllowGlobalStateModification(true);

                // ONLY declare the JFA buffer as a read dependency
                builder.UseTexture(currentRead, AccessFlags.Read);

                // Set the camera target as the write target (DO NOT UseTexture on this)
                builder.SetRenderAttachment(srcCamColor, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture("_FinalJFABuffer", data.readTex);

                    // Blitter handles the binding of data.sceneTex to _BlitTexture automatically
                    Blitter.BlitTexture(context.cmd, data.sceneTex, new Vector4(1, 1, 0, 0), data.material, 2);
                });
            }
        }
    }

    public Material outlineMaterial;
    private OutlinePass pass;

    public override void Create() => pass = new OutlinePass(outlineMaterial);

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (outlineMaterial == null) return;
        pass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        renderer.EnqueuePass(pass);
    }
}