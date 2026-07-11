Shader "Hidden/Custom/EnvironmentOutlinePure"
{
    Properties
    {
        [Header(Outline Settings)]
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Thickness (Pixels)", Range(0, 10)) = 2.0
        _MinThickness("Min Thickness", Range(0, 5)) = 0.5
        
        [Header(Falloff Settings)]
        _FalloffStart("Falloff Start Distance", Float) = 10.0
        _FalloffRange("Falloff Range", Float) = 40.0
        _PerspectiveScale("Perspective Scale Factor", Range(0.01, 1.0)) = 0.1
        
        [Header(Detection Thresholds)]
        _DepthThreshold("Depth Sensitivity", Float) = 0.5
        _NormalThreshold("Angle Threshold (Degrees)", Range(0, 180)) = 20.0

        _AASoftness("Edge Softness", Range(0.1, 2.0)) = 1.0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

        struct Attributes { uint vertexID : SV_VertexID; };
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

        float4 _BlitTexture_TexelSize; 

        Varyings vert(Attributes input)
        {
            Varyings output;
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
            return output;
        }
        ENDHLSL

       // ==================================================
        // PASS 0: INITIAL SEED (Stabilized UVs & Logic)
        // ==================================================
        Pass
        {
            Name "Pass0_Seed"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            float _DepthThreshold;
            float _NormalThreshold;
     

            float4 frag(Varyings input) : SV_Target
            {
                float2 texel = _BlitTexture_TexelSize.xy; 
                
                // FIX 1: Strict Pixel Centre Reconstruction
                float2 absolutePixel = floor(input.uv * _BlitTexture_TexelSize.zw) + 0.5;
                // We use snappedUV for ALL sampling so we never rely on floating-point interpolation
                float2 snappedUV = absolutePixel * texel;

                float dC = LinearEyeDepth(SampleSceneDepth(snappedUV), _ZBufferParams);
                if (dC > 3000.0) return float4(0, 0, 0, 0);

                float3 nC = SampleSceneNormals(snappedUV);
                float dL = LinearEyeDepth(SampleSceneDepth(snappedUV + float2(-texel.x, 0)), _ZBufferParams);
                float dR = LinearEyeDepth(SampleSceneDepth(snappedUV + float2(texel.x, 0)), _ZBufferParams);
                float dT = LinearEyeDepth(SampleSceneDepth(snappedUV + float2(0, texel.y)), _ZBufferParams);
                float dB = LinearEyeDepth(SampleSceneDepth(snappedUV + float2(0, -texel.y)), _ZBufferParams);

                // --- 1. DEPTH CHECK ---
                float diffL = dC - dL;
                float diffR = dC - dR;
                float diffT = dC - dT;
                float diffB = dC - dB;
                
                // FIX 2: Eliminate Float Equality for Depth
                float maxDepthDiff = diffL;
                float fgDepth = dL;
                
                if (diffR > maxDepthDiff) { maxDepthDiff = diffR; fgDepth = dR; }
                if (diffT > maxDepthDiff) { maxDepthDiff = diffT; fgDepth = dT; }
                if (diffB > maxDepthDiff) { maxDepthDiff = diffB; fgDepth = dB; }
                
                // Ensure it's a positive difference
                maxDepthDiff = max(0.0, maxDepthDiff);

                float slopeX = abs(dR - dL) * 0.5;
                float slopeY = abs(dT - dB) * 0.5;
                float maxSlope = max(slopeX, slopeY) * 1.5; 
                
                float depthThresh = max(0.005, _DepthThreshold * dC * 0.05);
                bool isDepthEdge = maxDepthDiff > (maxSlope + depthThresh);
                 
                if (!isDepthEdge) fgDepth = dC; // Fallback if no edge

                // --- 2. NORMAL CHECK ---
                float dotThreshold = cos(radians(_NormalThreshold));
                
                // We use (1.0 - dot) for the "difference" so your Yielding logic still works perfectly
                float ndL = 1.0 - dot(nC, SampleSceneNormals(snappedUV + float2(-texel.x, 0)));
                float ndR = 1.0 - dot(nC, SampleSceneNormals(snappedUV + float2(texel.x, 0)));
                float ndT = 1.0 - dot(nC, SampleSceneNormals(snappedUV + float2(0, texel.y)));
                float ndB = 1.0 - dot(nC, SampleSceneNormals(snappedUV + float2(0, -texel.y)));
                
                // FIX: Threshold for "difference" is (1.0 - cos(degrees))
                float thresholdDiff = 1.0 - dotThreshold;

                // Yield Logic
                float maxNormDiff = ndL;
                float nNeighborDepth = dL;
                bool yieldToNeighbor = true; 
                
                if (ndR > maxNormDiff) { maxNormDiff = ndR; nNeighborDepth = dR; yieldToNeighbor = false; }
                if (ndT > maxNormDiff) { maxNormDiff = ndT; nNeighborDepth = dT; yieldToNeighbor = false; }
                if (ndB > maxNormDiff) { maxNormDiff = ndB; nNeighborDepth = dB; yieldToNeighbor = true; }  

                // Now use the degree-based threshold
                bool isNormEdge = maxNormDiff > thresholdDiff;

                // FIX 4: Scaled Normal Tie-Breaker
                float depthTolerance = max(0.005, dC * 0.01);
                
                if (isNormEdge)
                {
                    if (nNeighborDepth < dC - depthTolerance) 
                    {
                        isNormEdge = false; 
                    }
                    else if (abs(nNeighborDepth - dC) <= depthTolerance && yieldToNeighbor) 
                    {
                        isNormEdge = false;
                    }
                }

                // --- 3. OUTPUT SEED ---
                if (isDepthEdge || isNormEdge) return float4(absolutePixel, fgDepth, 1.0); 
                
                return float4(0, 0, 0, 0); 
            }
            ENDHLSL
        }

        // ==================================================
        // PASS 1: JUMP FLOOD STEP
        // ==================================================
        Pass
        {
            Name "Pass1_JumpFlood"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            TEXTURE2D_X(_JFABuffer);
            SAMPLER(sampler_JFABuffer);
            float _JFA_Step;

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 texel = _BlitTexture_TexelSize.xy;
                
                // SYNCHRONIZED ANCHORING: Prevents seeds from drifting during the flood
                float2 myPixelPos = floor(uv * _BlitTexture_TexelSize.zw) + 0.5;

                float4 bestData = float4(0, 0, 0, 0);
                float bestDistSq = 999999.0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 sampleUV = uv + float2(x, y) * texel * _JFA_Step;
                        float4 data = SAMPLE_TEXTURE2D_X(_JFABuffer, sampler_JFABuffer, sampleUV);
                        
                        if (data.a > 0.5) 
                        {
                            float2 diff = myPixelPos - data.xy;
                            float distSq = dot(diff, diff);
                            
                            if (distSq < bestDistSq)
                            {
                                bestDistSq = distSq;
                                bestData = data;
                            }
                        }
                    }
                }
                return bestData;
            }
            ENDHLSL
        }

        // ==================================================
        // PASS 2: DEBUG DISPLAY
        // ==================================================
        /*Pass
        {
            Name "Pass2_DebugDisplay"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            TEXTURE2D_X(_FinalJFABuffer);
            SAMPLER(sampler_FinalJFABuffer);

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 myPixelPos = floor(uv * _BlitTexture_TexelSize.zw) + 0.5;

                float4 sdfData = SAMPLE_TEXTURE2D_X(_FinalJFABuffer, sampler_FinalJFABuffer, uv);
                
                // The Void Fix: If un-seeded (outside the 31px max range), render as pure white, NOT red.
                if (sdfData.a < 0.5) return float4(1, 1, 1, 1); 

                float dist = length(myPixelPos - sdfData.xy);
                float displayColor = saturate(dist / 20.0);
                
                return float4(displayColor, displayColor, displayColor, 1.0);
            }
            ENDHLSL
        }*/

        // ==================================================
        // PASS 2: COMPOSITE
        // ==================================================
        Pass
        {
            Name "Pass2_Composite"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            TEXTURE2D_X(_FinalJFABuffer);
            SAMPLER(sampler_FinalJFABuffer);

            float4 _OutlineColor;
            float _OutlineWidth;
            float _FalloffStart;
            float _FalloffRange;
            float _MinThickness;
            float _PerspectiveScale;
            float _AASoftness;

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // Original scene colour
                float4 sceneColor =
                    SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);

                // JFA result
                float4 sdf =
                    SAMPLE_TEXTURE2D_X(_FinalJFABuffer, sampler_FinalJFABuffer, uv);

                // No edge nearby
                if (sdf.a < 0.5)
                    return sceneColor;

                // Current pixel position
                float2 pixelPos = floor(uv * _BlitTexture_TexelSize.zw) + 0.5;

                // Distance to nearest seed
                float dist = length(pixelPos - sdf.xy);

                // --- DISTANCE FALLOFF & PERSPECTIVE ---
                float depth = sdf.z; // Your stored fgDepth
                
                // 1. Calculate Falloff Factor (t)
                float t = saturate((depth - _FalloffStart) / _FalloffRange);
                
                // 2. Interpolate base thickness
                float currentBaseWidth = lerp(_OutlineWidth, _MinThickness, smoothstep(0, 1, t));
                
                // 3. Apply Perspective Scaling (Lines shrink at distance)
                float scaledWidth = currentBaseWidth / (1.0 + (depth * _PerspectiveScale));

                // 1 pixel AA around the outline (using our new scaledWidth)
                float outline = 1.0 - smoothstep(scaledWidth - 1.0, scaledWidth, dist);

                float edgeStrength = 1.0 - smoothstep(scaledWidth - _AASoftness, scaledWidth, dist);

                // Use the outline color's alpha to scale the blend strength
                float blendedAlpha = edgeStrength * _OutlineColor.a;

                // Blend the outline color over the scene color using that alpha
                float3 finalRGB = lerp(sceneColor.rgb, _OutlineColor.rgb, blendedAlpha);
                
                return float4(finalRGB, sceneColor.a);
            }

            ENDHLSL
        }
    }
}