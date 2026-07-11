#ifndef CUSTOM_OUTLINE_INCLUDED
#define CUSTOM_OUTLINE_INCLUDED

TEXTURE2D(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

TEXTURE2D(_CameraNormalsTexture);
SAMPLER(sampler_CameraNormalsTexture);

// Added ViewDir and MaxPixelThickness to the inputs
void CalculateOutlineMask_float(float2 ScreenUV, float3 ViewDir, float WorldThickness, float MaxPixelThickness, float DepthThreshold, float NormalThreshold, out float OutlineMask)
{
    OutlineMask = 1.0;
    
#ifndef SHADERGRAPH_PREVIEW
    
    float rawDepth = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, ScreenUV, 0).r;
    float dC = LinearEyeDepth(rawDepth, _ZBufferParams);
    
    // ----------------------------------------------------
    // 1. THE CAPPED PERSPECTIVE LOCK
    // ----------------------------------------------------
    float ndcSize = (WorldThickness * UNITY_MATRIX_P._m11) / max(dC, 0.001);
    float uvSizeY = ndcSize * 0.5;
    
    // NEW: The Pixel Cap. This stops the line from swallowing the screen 
    // when you walk right up to the lockers!
    float maxUvY = MaxPixelThickness / _ScreenParams.y;
    uvSizeY = min(uvSizeY, maxUvY);
    
    float aspectRatio = _ScreenParams.y / _ScreenParams.x;
    float uvSizeX = uvSizeY * aspectRatio;
    
    float2 texel = float2(uvSizeX, uvSizeY);
    
    // ----------------------------------------------------
    // 2. 8-TAP RADIAL SAMPLING
    // ----------------------------------------------------
    float2 offsets[8] =
    {
        float2(0.0, 1.0), float2(0.0, -1.0), float2(1.0, 0.0), float2(-1.0, 0.0),
        float2(0.707, 0.707), float2(-0.707, -0.707), float2(0.707, -0.707), float2(-0.707, 0.707)
    };
    
    float maxDepthDiff = 0.0;
    float maxNormDiff = 0.0;
    float3 nC = SAMPLE_TEXTURE2D_LOD(_CameraNormalsTexture, sampler_CameraNormalsTexture, ScreenUV, 0).xyz;
    
    [unroll]
    for (int i = 0; i < 8; i++)
    {
        float2 uv = ScreenUV + (offsets[i] * texel);
        
        float dTap = LinearEyeDepth(SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, uv, 0).r, _ZBufferParams);
        maxDepthDiff = max(maxDepthDiff, abs(dTap - dC));
        
        float3 nTap = SAMPLE_TEXTURE2D_LOD(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv, 0).xyz;
        maxNormDiff = max(maxNormDiff, length(nTap - nC));
    }
    
    // ----------------------------------------------------
    // 3. GRAZING ANGLE COMPENSATION
    // ----------------------------------------------------
    // NdotV measures how 'tilted' the surface is away from the camera.
    // 1.0 = looking flat at a wall. 0.0 = looking along a sharp edge.
    float NdotV = max(dot(nC, normalize(ViewDir)), 0.1);
    
    // By dividing the threshold by NdotV, we mathematically forgive the massive 
    // depth leaps that naturally occur when looking down a slanted row of lockers!
    float grazingDepthThreshold = DepthThreshold * (1.0 / NdotV);
    
    float depthEdge = smoothstep(grazingDepthThreshold * 0.8, grazingDepthThreshold * 1.2, maxDepthDiff);
    float normalEdge = smoothstep(NormalThreshold * 0.8, NormalThreshold * 1.2, maxNormDiff);
    
    float edge = max(depthEdge, normalEdge);
    
    // saturate() prevents weird HDR bloom artifacts if edge goes above 1.0
    OutlineMask = saturate(1.0 - edge);
    
#endif
}

#endif