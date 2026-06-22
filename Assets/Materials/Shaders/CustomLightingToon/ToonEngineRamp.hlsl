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
    float3 PositionWS,
    float3 ViewDirWS,
    out float3 OutColor)
{
    
    OutColor = float3(0, 0, 0);

#ifndef SHADERGRAPH_PREVIEW
    float3 N = SafeNormalize(Normal);
    float3 V = SafeNormalize(ViewDirWS);

#if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)            
    float3 ambientLight = SampleSH(N);
#else
    float3 ambientLight = float3(0.1, 0.1, 0.15);
#endif

    float3 diffuseAccumulation = ambientLight * AmbientOcclusion.x;
    float3 specularAccumulation = float3(0, 0, 0);

    float3 diffuseColor = Base_Color.rgb * lerp(1.0, 0.2, Metallic.x);
    float3 specTint = lerp(float3(1, 1, 1), Base_Color.rgb, Metallic.x);

    float specPower = exp2(10 * Smoothness.x + 1);
    float specOpacity = Smoothness.x;

    // ----------------------------------------------------
    // LIGHTING LOOP
    // ----------------------------------------------------
#if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)
    float4 shadowCoord = TransformWorldToShadowCoord(PositionWS);
    Light mainLight = GetMainLight(shadowCoord);
#else
    Light mainLight = GetMainLight();
#endif

    // Calculate light intensity
    float NdotL = saturate(dot(N, mainLight.direction) * 0.5 + 0.5);
    float shadowDelta = fwidth(mainLight.shadowAttenuation) * 1.5;
    float shadow = smoothstep(0.5 - shadowDelta, 0.5 + shadowDelta, mainLight.shadowAttenuation);

    // Get the "Banding Shape" from the BandRamp
    float intensity = NdotL * shadow;
    float banding = SAMPLE_TEXTURE2D(BandRamp.tex, RampSampler.samplerstate, float2(intensity, 0.5)).r;

    // Map the banding to the actual Colors
    float3 finalDiffuse = SAMPLE_TEXTURE2D(ColorRamp.tex, RampSampler.samplerstate, float2(banding, 0.5)).rgb;
    diffuseAccumulation += mainLight.color.rgb * finalDiffuse;

    // Specular remains physics-based for that clean look
    float3 halfVector = SafeNormalize(mainLight.direction + V);
    float specRaw = pow(saturate(dot(N, halfVector)), specPower);
    specularAccumulation += mainLight.color.rgb * specTint * (step(0.5, specRaw * shadow) * specOpacity);

    // --- ADDITIONAL LIGHTS (Simplified loop logic for the Dual-Ramp) ---
#if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, PositionWS);
        
        float rawDist = light.distanceAttenuation;
        float warpedDist = saturate(pow(rawDist, FalloffPower));
        
        // Atten contains both the warped distance and the shadows
        float atten = warpedDist * AdditionalLightRealtimeShadow(lightIndex, PositionWS, light.direction);
        float NdotL_Add = saturate(dot(N, light.direction) * 0.5 + 0.5);
        
        // The Intensity Lerp controls WHERE the bands go
        float intensityA = NdotL_Add; 
        float intensityB = NdotL_Add * atten;
        float finalIntensity = lerp(intensityA, intensityB, DistanceBandingSlider);
        
        float bandingAdd = SAMPLE_TEXTURE2D(BandRamp.tex, RampSampler.samplerstate, float2(finalIntensity, 0.5)).r;
        float3 colorAdd = SAMPLE_TEXTURE2D(ColorRamp.tex, RampSampler.samplerstate, float2(bandingAdd, 0.5)).rgb;
        
        // THE FIX: We always multiply by atten. 
        // This guarantees the light respects the Unity 'Range' slider and eventually fades to pure black.
        diffuseAccumulation += light.color.rgb * colorAdd * atten;
    LIGHT_LOOP_END
#endif

    OutColor = (diffuseColor * diffuseAccumulation) + specularAccumulation + Emission.rgb;
#else
    OutColor = Base_Color.rgb;
#endif
}