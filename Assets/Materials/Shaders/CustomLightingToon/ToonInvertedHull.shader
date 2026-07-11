Shader "Custom/ToonInvertedHull"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _MaxPixels ("Near Width (Pixels)", Float) = 4.0
        _MinPixels ("Far Width (Pixels)", Float) = 1.0
        _StartDist ("Start Shrink Distance", Float) = 5.0
        _EndDist ("End Shrink Distance", Float) = 50.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1" }
        Cull Front 
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "HullOutline"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float4 texcoord2  : TEXCOORD2; // UPGRADE: Now a float4 to read the W signature
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _MaxPixels;
                float _MinPixels;
                float _StartDist;
                float _EndDist;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 smoothNormalOS;

                // SECURITY CHECK: Does this mesh have our C# Magic Number (0.5) baked into W?
                // We use abs(w - 0.5) > 0.01 to account for tiny floating point errors.
                if (abs(input.texcoord2.w - 0.5) > 0.01)
                {
                    // UNBAKED MESH (Unity Primitive). Safely bypass and use hard normals.
                    smoothNormalOS = input.normalOS;
                }
                else
                {
                    // BAKED MESH! Safely decode the Tangent-Space data.
                    float3 bitangentOS = cross(input.normalOS, input.tangentOS.xyz) * input.tangentOS.w;
                    smoothNormalOS = input.tangentOS.xyz * input.texcoord2.x + 
                                     bitangentOS         * input.texcoord2.y + 
                                     input.normalOS      * input.texcoord2.z;
                    smoothNormalOS = normalize(smoothNormalOS);
                }

                // Convert to World Space
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float3 worldNormal = TransformObjectToWorldNormal(smoothNormalOS);

                // Distance Thickness Curve
                float dist = length(GetCameraPositionWS() - worldPos);
                float t = saturate((dist - _StartDist) / max(_EndDist - _StartDist, 0.001));
                float rawWidth = lerp(_MaxPixels, _MinPixels, t * t);
                float width = round(rawWidth * 2.0) * 0.5;

                // Two-Point Clip-Space Extrusion
                float4 clipPos = TransformWorldToHClip(worldPos);
                float4 clipPos2 = TransformWorldToHClip(worldPos + worldNormal);

                float2 ndcPos = clipPos.xy / clipPos.w;
                float2 ndcPos2 = clipPos2.xy / clipPos2.w;
                float2 dir = ndcPos2 - ndcPos;

                float len = length(dir);
                if (len > 1e-5) dir /= len;
                else dir = float2(0.0, 0.0);

                // Resolution Independent Extrusion
                float2 pixelOffset = (width * 2.0) / _ScreenParams.xy;
                clipPos.xy += dir * pixelOffset * clipPos.w;

                output.positionCS = clipPos;
                return output;
            }

            half4 frag(Varyings input) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}