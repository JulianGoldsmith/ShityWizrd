Shader "Toon/HDRP_ToonLit"
{
    Properties
    {
        _BaseMap    ("Base Map", 2D) = "white" {}
        _BaseColor  ("Base Color", Color) = (1,1,1,1)

        _NormalMap  ("Normal Map", 2D) = "bump" {}
        _NormalScale("Normal Strength", Range(0,4)) = 1

        _Steps      ("Band Steps", Range(2,12)) = 5
        _HalfLambert("Half Lambert (0..1)", Range(0,1)) = 0.5
        _Exposure   ("Exposure", Range(0.1,2)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardToon"
            Tags{ "LightMode"="ForwardOnly"}

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   Vert
            #pragma fragment Frag

            // Optional: enable to quickly test only directional lighting compile
            //#define DISABLE_PUNCTUAL

            // Local keyword for normal map usage
            #pragma shader_feature_local _NORMALMAP

            // ---------- REQUIRED HDRP SETUP ----------
            #ifndef SHADERPASS
              #define SHADERPASS SHADERPASS_FORWARD
            #endif

            // Core / common
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariablesFunctions.hlsl"

            // Shadow context & sampling
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Shadow/HDShadowContext.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Shadow/HDShadowSampling.hlsl"

            // ***** FORCE FILTER ALGORITHMS FOR ALL PASSES (fixes 'Undefined punctual shadow filter algorithm') *****
            #undef  SHADOW_PUNCTUAL_FILTER_ALGORITHM
            #undef  SHADOW_DIRECTIONAL_FILTER_ALGORITHM
            #undef  SHADOW_AREA_FILTER_ALGORITHM
            #define SHADOW_PUNCTUAL_FILTER_ALGORITHM    SHADOWFILTERING_PCF_5x5
            #define SHADOW_DIRECTIONAL_FILTER_ALGORITHM SHADOWFILTERING_PCF_5x5
            #define SHADOW_AREA_FILTER_ALGORITHM        SHADOWFILTERING_PCF_5x5

            // Algorithms must come AFTER the defines above
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Shadow/HDShadowAlgorithms.hlsl"

            // Lighting helpers (clustered)
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/PunctualLightCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Lighting.hlsl"

            // ---------- MATERIAL UNIFORMS ----------
            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);      SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _NormalScale;
                float  _HalfLambert;
                float  _Exposure;
                float  _Steps;
            CBUFFER_END

            // ---------- VERT/FRAG I/O ----------
            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 posWS      : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 tangentWS  : TEXCOORD2;
                float3 bitanWS    : TEXCOORD3;
                float2 uv         : TEXCOORD4;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;

                float4 posWS4 = TransformObjectToWorld(float4(v.positionOS, 1.0));
                o.posWS       = posWS4.xyz;
                o.positionCS  = TransformWorldToHClip(o.posWS);

                float3 nWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                float3 tWS = normalize(TransformObjectToWorldDir(v.tangentOS.xyz));
                float  signW = v.tangentOS.w * unity_WorldTransformParams.w;
                float3 bWS = normalize(cross(nWS, tWS) * signW);

                o.normalWS  = nWS;
                o.tangentWS = tWS;
                o.bitanWS   = bWS;
                o.uv        = v.uv;

                return o;
            }

            // ---------- HELPERS ----------
            float3 UnpackNormalMap(float2 uv, float3 tWS, float3 bWS, float3 nWS)
            {
                // Standard tangent-space normal (DXT5nm or normal map)
                float3 tn = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv).xyz * 2.0 - 1.0;
                tn.xy *= _NormalScale;
                tn = normalize(tn);
                float3x3 TBN = float3x3(normalize(tWS), normalize(bWS), normalize(nWS));
                return normalize(mul(TBN, tn));
            }

            inline float NdotL_Wrap(float3 N, float3 L, float halfLambert)
            {
                float d = dot(N,L);
                float lambert = saturate(d);
                float halfLam = saturate(0.5 * (d + 1.0));
                return lerp(lambert, halfLam, saturate(halfLambert));
            }

            float Posterize(float x, float steps)
            {
                steps = max(2.0, steps);
                return floor(saturate(x) * steps) / (steps - 1.0);
            }

            // Accumulate shadowed diffuse from main directional + punctual lights
            float ShadeShadowedDiffuse(float3 posWS, float3 normalWS)
            {
                float sum = 0.0;

                // ---- Directional (main) ----
                {
                    DirectionalLightData dirLight;
                    if (LightLoopGetMainLight(dirLight))
                    {
                        float3 L = -dirLight.forward;  // to light
                        float  S = GetDirectionalShadowAttenuation(HDShadowContext, dirLight, posWS);
                        float  ndl = NdotL_Wrap(normalWS, L, _HalfLambert);

                        float3 rgb = dirLight.color * dirLight.diffuseScale;
                        float  lum = Luminance(rgb);

                        sum += ndl * S * lum;
                    }
                }

                // ---- Punctual (spot/point) ----
                #ifndef DISABLE_PUNCTUAL
                {
                    uint start = 0, count = 0;

                    // Many HDRP minors expose this WS helper:
                    GetPunctualLightStartAndCountWS(posWS, start, count);

                    // If the line above errors in your exact HDRP minor, comment it
                    // and use this pair instead:
                    //FragInputs fi = (FragInputs)0; fi.positionRWS = posWS; fi.normalWS = normalWS; fi.isFrontFace = true;
                    //GetPunctualLightStartAndCount(fi, start, count);

                    uint maxLights = min(count, (uint)32);

                    [loop]
                    for (uint i=0u; i<maxLights; i++)
                    {
                        uint li = start + i;
                        LightData Ld = GetPunctualLight(li);

                        float3 Lvec = Ld.positionRWS - posWS;
                        float  dist = max(length(Lvec), 1e-6);
                        float3 L    = Lvec / dist;

                        float  ang  = 1.0;
                        if (Ld.lightType == GPULIGHTTYPE_SPOT)
                            ang = ComputeSpotAttenuation(L, Ld.forward, Ld.angleScale, Ld.angleOffset);

                        float  distAtt = DistanceAttenuation(dist, Ld.range);
                        float  S       = GetPunctualShadowAttenuation(HDShadowContext, Ld, posWS);

                        float  ndl = NdotL_Wrap(normalWS, L, _HalfLambert);
                        float3 rgb = Ld.color * Ld.diffuseScale;
                        float  lum = Luminance(rgb);

                        sum += ndl * ang * distAtt * S * lum;
                    }
                }
                #endif

                // Map to 0..1 with mild rolloff, apply exposure
                float m = sum * max(_Exposure, 1e-3);
                return saturate(m / (1.0 + m));
            }

            // ---------- FRAGMENT ----------
            struct FragOut { float4 color : SV_Target; };

            FragOut Frag(Varyings i)
            {
                FragOut o;

                float3 N = i.normalWS;

                #if defined(_NORMALMAP)
                    N = UnpackNormalMap(i.uv, i.tangentWS, i.bitanWS, i.normalWS);
                #endif

                float4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;

                float lit01 = ShadeShadowedDiffuse(i.posWS, N);
                float bands = Posterize(lit01, _Steps);

                o.color = float4(albedo.rgb * bands, albedo.a);
                return o;
            }
            ENDHLSL
        }
    }

    FallBack Off
}