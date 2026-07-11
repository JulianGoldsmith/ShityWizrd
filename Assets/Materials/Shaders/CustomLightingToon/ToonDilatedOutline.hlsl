#ifndef CUSTOM_CLEAN_OUTLINE_INCLUDED
#define CUSTOM_CLEAN_OUTLINE_INCLUDED

TEXTURE2D(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

// 1. A ultra-clean 1px depth edge detector (Roberts Cross variant)
float GetDepthEdge(float2 UV, float DepthThresh, float2 TexelSize)
{
    float d[4];
    float2 offsets[4] =
    {
        float2(-1, -1), float2(1, 1),
        float2(-1, 1), float2(1, -1)
    };

    [unroll]
    for (int i = 0; i < 4; i++)
        d[i] = LinearEyeDepth(SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, UV + offsets[i] * TexelSize, 0).r, _ZBufferParams);

    float diff = abs(d[1] - d[0]) + abs(d[3] - d[2]);
    float dC = LinearEyeDepth(SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, UV, 0).r, _ZBufferParams);
    
    // Scale threshold by distance
    float scaledThresh = DepthThresh * dC;
    return step(scaledThresh, diff);
}

// 2. The Main Pass
void CalculateOutline_float(float2 ScreenUV, float MaxPixels, float MinPixels, float EndDist, float DepthThreshold, out float OutlineMask)
{
    OutlineMask = 1.0;
    
#ifndef SHADERGRAPH_PREVIEW
    float2 texel = 1.0 / _ScreenParams.xy;
    float rawDepth = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, ScreenUV, 0).r;
    float dC = LinearEyeDepth(rawDepth, _ZBufferParams);

    // Natural Thickness Curve (t*t)
    float t = saturate(dC / EndDist);
    float width = lerp(MaxPixels, MinPixels, t * t);

    // Distance Fade (optional, makes it disappear nicely at range)
    float fade = 1.0 - smoothstep(EndDist * 0.7, EndDist, dC);

    float maxEdge = GetDepthEdge(ScreenUV, DepthThreshold, texel);

    // Dilation
    if (width > 1.0)
    {
        const int NUM_TAPS = 8; // Reduced to 8 for stability/performance
        [unroll]
        for (int i = 0; i < NUM_TAPS; i++)
        {
            float angle = 2.39996 * float(i);
            float radius = sqrt(float(i + 0.5) / float(NUM_TAPS)) * (width - 1.0);
            float2 offset = float2(cos(angle), sin(angle)) * radius * texel;
            float2 sampleUV = ScreenUV + offset;
            
            float dTap = LinearEyeDepth(SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, sampleUV, 0).r, _ZBufferParams);
            
            // Tight Depth Rejection
            if (abs(dTap - dC) < max(0.01, dC * 0.0005))
            {
                maxEdge = max(maxEdge, GetDepthEdge(sampleUV, DepthThreshold, texel));
            }
        }
    }

    // Apply fade and invert for mask
    OutlineMask = saturate(1.0 - (maxEdge * fade));
#endif
}

#endif