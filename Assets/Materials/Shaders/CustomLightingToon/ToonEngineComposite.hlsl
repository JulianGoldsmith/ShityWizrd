void CalculateToonLighting_float(
    float3 Base_Color,
    float3 Normal,
    float Smoothness,
    float Metallic,
    float3 Emission,
    float AmbientOcclusion,
    float BandCount,
    float BandFeather,
    UnitySamplerState RampSampler, // ADDED BACK
    UnityTexture2D ColorRamp, // ADDED BACK
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
    OutColor = Base_Color;
    OutEmission = Emission;

#ifndef SHADERGRAPH_PREVIEW

    float3 N = SafeNormalize(Normal);
    float3 V = SafeNormalize(ViewDirWS);

#if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)            
    float3 ambientLight = SampleSH(N);
#else
    float3 ambientLight = float3(0.1, 0.1, 0.15);
#endif

    float3 rawDiffuseAccumulation = ambientLight * AmbientOcclusion;
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
    float shadow = smoothstep(0.5 - 0.05, 0.5 + 0.05, mainLight.shadowAttenuation);
    rawDiffuseAccumulation += mainLight.color.rgb * (NdotL * shadow);

    float3 halfVector = SafeNormalize(mainLight.direction + V);
    float specRaw = pow(saturate(dot(N, halfVector)), specPower);
    specularAccumulation += mainLight.color.rgb * specTint * (step(0.5, specRaw * shadow) * specOpacity);

    // ----------------------------------------------------
    // 2. ADDITIONAL LIGHTS
    // ----------------------------------------------------
#if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
    
    uint pixelLightCount = GetAdditionalLightsCount();

#if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)
    InputData inputData = (InputData)0;
    inputData.normalizedScreenSpaceUV = ScreenPosition.xy; 
    inputData.positionWS = PositionWS;
    half4 shadowMask = half4(1, 1, 1, 1);
#endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
        light.shadowAttenuation = AdditionalLightRealtimeShadow(lightIndex, inputData.positionWS, light.direction);
        
        float rawDist = light.distanceAttenuation;
        float warpedDist = saturate(pow(abs(rawDist), FalloffPower)); 
        float atten = warpedDist * light.shadowAttenuation;
        
        float NdotL_Add = saturate(dot(N, light.direction) * 0.5 + 0.5);
        float intensityA = NdotL_Add; 
        float intensityB = NdotL_Add * atten;
        float finalIntensity = lerp(intensityA, intensityB, DistanceBandingSlider);
        
        rawDiffuseAccumulation += light.color.rgb * (finalIntensity * atten);
    LIGHT_LOOP_END
#endif

    // ----------------------------------------------------
    // 3. HYBRID BANDING MATH + TEXTURE COLOR
    // ----------------------------------------------------
    
    // A. The Shape: Calculate the feathered mathematical bands
    float rawIntensity = length(rawDiffuseAccumulation);
    float bands = rawIntensity * BandCount;
    float bandedIntensity = (floor(bands) + smoothstep(0.0, BandFeather, frac(bands))) / BandCount;
    
    // B. The Color: Pipe the math shape into your Texture Ramp to get your painted colors
    // We use saturate() so we don't read past the 1.0 edge of the texture
    float3 rampColor = SAMPLE_TEXTURE2D(ColorRamp.tex, RampSampler.samplerstate, float2(saturate(bandedIntensity), 0.5)).rgb;
    
    // C. The Composition: 
    // We figure out the tint of the overlapping Forward+ lights (e.g., Red light + Blue light = Purple tint)
    float3 lightTint = rawDiffuseAccumulation / max(rawIntensity, 0.0001);
    
    // If the light is incredibly bright (HDR > 1.0), we force it to glow even though the texture stopped at 1.0
    float hdrBoost = max(1.0, bandedIntensity);
    
    float3 compositeDiffuse = (rawIntensity > 0.0001) ? (lightTint * rampColor * hdrBoost) : float3(0, 0, 0);


    // ----------------------------------------------------
    // 4. RIM LIGHTING
    // ----------------------------------------------------
    float fresnel = saturate(1.0 - dot(N, V));
    float rawRim = pow(fresnel, RimPower);
    float rimStep = smoothstep(RimEdge - 0.05, RimEdge + 0.05, rawRim);
    float rimDirectionalMask = saturate(dot(N, mainLight.direction) + 0.5);
    float3 rimAccumulation = RimColor * rimStep * rimDirectionalMask * shadow * AmbientOcclusion;

    // --- FINAL COMPOSITION ---
    OutColor = (diffuseColor * compositeDiffuse) + specularAccumulation + rimAccumulation;
    OutEmission = Emission;

#else
    float3 N_preview = SafeNormalize(Normal);
    float3 NdotL_preview = saturate(dot(N_preview, normalize(float3(0.5, 0.5, 0.2))) * 0.5 + 0.5);
    OutColor = Base_Color * NdotL_preview;
    OutEmission = Emission;
#endif
}