#ifndef CUSTOM_ENV_OUTLINE_INCLUDED
#define CUSTOM_ENV_OUTLINE_INCLUDED

// NO VARIABLE DECLARATIONS OR INCLUDES.
// We are relying entirely on Shader Graph injecting _CameraDepthTexture 
// and _ZBufferParams invisibly in the background.

// Helper: Safely unpack linear depth
float GetLinearDepth(float2 uv)
{
    float rawDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
    return 1.0 / (_ZBufferParams.z * rawDepth + _ZBufferParams.w);
}

// 1. CONTINUOUS DEPTH EDGE DETECTOR
float GetDepthEdge(float2 UV, float DepthThresh, float2 TexelSize)
{
    float d0 = GetLinearDepth(UV);
    float d1 = GetLinearDepth(UV + float2(0, 1) * TexelSize);
    float d2 = GetLinearDepth(UV + float2(0, -1) * TexelSize);
    float d3 = GetLinearDepth(UV + float2(-1, 0) * TexelSize);
    float d4 = GetLinearDepth(UV + float2(1, 0) * TexelSize);

    float depthDiff = abs(d1 - d0) + abs(d2 - d0) + abs(d3 - d0) + abs(d4 - d0);
    
    // Non-linear threshold scaling (keeps distant silhouettes alive!)
    float scaledThresh = DepthThresh * sqrt(max(d0, 1.0));
    
    // Smooth, continuous edge (0.0 to 1.0) instead of a binary pop
    return smoothstep(scaledThresh, scaledThresh * 2.0, depthDiff);
}

// 2. PERSPECTIVE-SCALED OUTLINE
void CalculateEnvOutline_float(float2 ScreenUV, float WorldThickness, float MinPixels, float MaxPixels, float DepthThreshold, out float OutlineMask)
{
    OutlineMask = 1.0;
    
#ifndef SHADERGRAPH_PREVIEW
    float2 texel = 1.0 / _ScreenParams.xy;
    float dC = GetLinearDepth(ScreenUV);

    // Step 1: Calculate desired thickness via perspective projection.
    // By dividing by depth, the line naturally scales with the camera.
    float pixelWidth = clamp(WorldThickness / max(dC, 0.001), MinPixels, MaxPixels);

    // Get the base edge at the exact pixel
    float maxEdge = GetDepthEdge(ScreenUV, DepthThreshold, texel);

    // Step 2 & 3: Sample outward continuously
    if (pixelWidth > 0.5)
    {
        const int NUM_TAPS = 8;
        float currentEdge = maxEdge;
        
        [unroll]
        for (int i = 0; i < NUM_TAPS; i++)
        {
            float angle = 0.785398 * float(i); // 45 degrees in radians
            float2 offset = float2(cos(angle), sin(angle)) * pixelWidth * texel;
            float2 sampleUV = ScreenUV + offset;
            
            float tapEdge = GetDepthEdge(sampleUV, DepthThreshold, texel);
            
            // Depth Rejection: Prevents outline from bleeding inward onto foreground objects
            float dTap = GetLinearDepth(sampleUV);
            if (abs(dTap - dC) < max(0.1, dC * 0.05))
            {
                currentEdge = max(currentEdge, tapEdge);
            }
        }
        maxEdge = currentEdge;
    }

    // Invert so the line is black (0.0) and the empty space is white (1.0)
    OutlineMask = saturate(1.0 - maxEdge);
#endif
}

#endif