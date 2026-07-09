void CalculateToonLighting_float(
    float3 Base_Color,
    float3 Normal,
    float Smoothness,
    float Metallic,
    float3 Emission,
    float AmbientOcclusion,
    UnityTexture2D BandRamp,
    UnitySamplerState RampSampler,
    UnityTexture2D ColorRamp,
    float DistanceBandingSlider,
    float FalloffPower,
    float3 RimColor, 
    float RimPower, 
    float RimEdge, 
    float3 PositionWS,
    float3 ViewDirWS,
    float4 ScreenPosition, 
    out float3 OutColor,
    out float3 OutEmission)
{
    // 1. Initialize outputs immediately for the Vertex Shader fallback
    OutColor = Base_Color;
    OutEmission = Emission;

#ifndef SHADERGRAPH_PREVIEW

    // --- PROTECT THE VERTEX SHADER ---
    // Forces the expensive URP lighting loops to strictly run per-pixel.
    #ifdef SHADER_STAGE_FRAGMENT

    float3 N = SafeNormalize(Normal);
    float3 V = SafeNormalize(ViewDirWS);

#if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)            
    float3 ambientLight = SampleSH(N);
#else
    float3 ambientLight = float3(0.1, 0.1, 0.15);
#endif

    float3 diffuseAccumulation = ambientLight * AmbientOcclusion;
    float3 specularAccumulation = float3(0, 0, 0);
    float3 diffuseColor = Base_Color * lerp(1.0, 0.2, Metallic);
    float3 specTint = lerp(float3(1, 1, 1), Base_Color, Metallic);

    float specPower = exp2(10 * Smoothness + 1);
    float specOpacity = Smoothness;

    // ----------------------------------------------------
    // 1. MAIN LIGHT
    // ----------------------------------------------------
#if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)    
    float4 shadowCoord = TransformWorldToShadowCoord(PositionWS);
    Light mainLight = GetMainLight(shadowCoord);
#else
    Light mainLight = GetMainLight();
#endif

    float NdotL = saturate(dot(N, mainLight.direction) * 0.5 + 0.5);
    float shadowDelta = fwidth(mainLight.shadowAttenuation) * 1.5;
    float shadow = smoothstep(0.5 - shadowDelta, 0.5 + shadowDelta, mainLight.shadowAttenuation);

    float intensity = NdotL * shadow;
    float banding = SAMPLE_TEXTURE2D(BandRamp.tex, RampSampler.samplerstate, float2(intensity, 0.5)).r;
    float3 finalDiffuse = SAMPLE_TEXTURE2D(ColorRamp.tex, RampSampler.samplerstate, float2(banding, 0.5)).rgb;
    diffuseAccumulation += mainLight.color.rgb * finalDiffuse;

    float3 halfVector = SafeNormalize(mainLight.direction + V);
    float specRaw = pow(saturate(dot(N, halfVector)), specPower);
    specularAccumulation += mainLight.color.rgb * specTint * (step(0.5, specRaw * shadow) * specOpacity);

    // ----------------------------------------------------
    // 2. RIM LIGHTING
    // ----------------------------------------------------
    float fresnel = saturate(1.0 - dot(N, V));
    float rawRim = pow(fresnel, RimPower);
    float rimDelta = fwidth(rawRim) * 1.5;
    float rimStep = smoothstep(RimEdge - rimDelta, RimEdge + rimDelta, rawRim);
    float rimDirectionalMask = saturate(dot(N, mainLight.direction) + 0.5);
    
    float3 rimAccumulation = RimColor * rimStep * rimDirectionalMask * shadow * AmbientOcclusion;

    // ----------------------------------------------------
    // 3. ADDITIONAL LIGHTS
    // ----------------------------------------------------
#if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
    
    uint pixelLightCount = GetAdditionalLightsCount();

    // Safely declare InputData whenever additional lights exist
    #if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)
    InputData inputData = (InputData)0;
    inputData.normalizedScreenSpaceUV = ScreenPosition.xy; 
    inputData.positionWS = PositionWS;
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, PositionWS);
        light.shadowAttenuation = AdditionalLightRealtimeShadow(lightIndex, PositionWS, light.direction);
        
        float rawDist = light.distanceAttenuation;
        float warpedDist = saturate(pow(abs(rawDist), FalloffPower)); 
        float atten = warpedDist * light.shadowAttenuation;
        
        float NdotL_Add = saturate(dot(N, light.direction) * 0.5 + 0.5);
        float intensityA = NdotL_Add; 
        float intensityB = NdotL_Add * atten;
        float finalIntensity = lerp(intensityA, intensityB, DistanceBandingSlider);
        
        float bandingAdd = SAMPLE_TEXTURE2D(BandRamp.tex, RampSampler.samplerstate, float2(finalIntensity, 0.5)).r;
        float3 colorAdd = SAMPLE_TEXTURE2D(ColorRamp.tex, RampSampler.samplerstate, float2(bandingAdd, 0.5)).rgb;
        
        diffuseAccumulation += light.color.rgb * colorAdd * atten;
    LIGHT_LOOP_END
#endif

    // --- FINAL COMPOSITION ---
    OutColor = (diffuseColor * diffuseAccumulation) + specularAccumulation + rimAccumulation;
    OutEmission = Emission;

    #endif // END SHADER_STAGE_FRAGMENT

#else
    // PREVIEW FALLBACK
    float3 N_preview = SafeNormalize(Normal);
    float3 NdotL_preview = saturate(dot(N_preview, normalize(float3(0.5, 0.5, 0.2))) * 0.5 + 0.5);
    OutColor = Base_Color * NdotL_preview;
    OutEmission = Emission;
#endif
}