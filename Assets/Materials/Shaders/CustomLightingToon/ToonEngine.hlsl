// The function name MUST end in _float for Shader Graph to find it
void CalculateToonLighting_float(
    float3 Base_Color,
    float3 Normal,
    float Smoothness,
    float Metallic,
    float3 Emission,
    float AmbientOcclusion,
    float Steps,
    float DistanceBandingSlider,
    float3 PositionWS,
    float3 ViewDirWS,
    out float3 OutColor)
{
    
    OutColor = float3(0, 0, 0);

#ifndef SHADERGRAPH_PREVIEW
    // --- FIX 3: Safe Normalization ---
    float3 N = SafeNormalize(Normal);
    float3 V = SafeNormalize(ViewDirWS);

    // --- FIX 2: Divide by Zero Safety ---
    float safeSteps = max(Steps.x, 1.0);

    // NATIVE URP AMBIENT LIGHTING
#if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)            
    float3 ambientLight = SampleSH(N);
#else
    float3 ambientLight = float3(0.1, 0.1, 0.15);
#endif

    // --- FIX 1: AO only affects Ambient Light ---
    float3 diffuseAccumulation = ambientLight * AmbientOcclusion.x;
    float3 specularAccumulation = float3(0, 0, 0);

    // --- FIX 6: Metallic Energy Conservation ---
    // Metals dim the diffuse and strictly color the specular
    float3 diffuseColor = Base_Color.rgb * lerp(1.0, 0.2, Metallic.x);
    float3 specTint = lerp(float3(1, 1, 1), Base_Color.rgb, Metallic.x);

    float specPower = exp2(10 * Smoothness.x + 1);
    float specOpacity = Smoothness.x;

    // ----------------------------------------------------
    // 1. MAIN LIGHT (The Sun)
    // ----------------------------------------------------
#if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)            
    float4 shadowCoord = TransformWorldToShadowCoord(PositionWS);
    Light mainLight = GetMainLight(shadowCoord);
#else
    Light mainLight = GetMainLight();
#endif

    float sunShadowDelta = fwidth(mainLight.shadowAttenuation);
    float crispSunShadow = smoothstep(0.5 - sunShadowDelta, 0.5 + sunShadowDelta, mainLight.shadowAttenuation);
    float mainNdotL = dot(N, mainLight.direction);
    float mainAngle = saturate(mainNdotL * 0.5 + 0.5);
    
    // --- FIX 4: Biased Banding (+0.5 trick) ---
    float mainBandedAngle = floor(mainAngle * safeSteps + 0.5) / safeSteps;
        
    diffuseAccumulation += mainLight.color.rgb * (mainBandedAngle * crispSunShadow);

    float3 mainHalfVector = SafeNormalize(mainLight.direction + V);
    float mainNdotH = saturate(dot(N, mainHalfVector));
    float mainSpecRaw = pow(mainNdotH, specPower);
    float mainSpecBanded = step(0.5, mainSpecRaw * crispSunShadow) * specOpacity;
        
    specularAccumulation += mainLight.color.rgb * specTint * mainSpecBanded;

    // ----------------------------------------------------
    // 2. ADDITIONAL LIGHTS (Spells, Torches)
    // ----------------------------------------------------
#if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)            
    uint pixelLightCount = GetAdditionalLightsCount();
            
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, PositionWS);
        light.shadowAttenuation = AdditionalLightRealtimeShadow(lightIndex, PositionWS, light.direction);
                
        float NdotL = dot(N, light.direction);
        float angle = saturate(NdotL * 0.5 + 0.5);
        float dist = light.distanceAttenuation;
        float shadow = light.shadowAttenuation;

        float pointShadowDelta = fwidth(shadow);
        float crispPointShadow = smoothstep(0.5 - pointShadowDelta, 0.5 + pointShadowDelta, shadow);

        // STATE A: DECOUPLED (Biased)
        float stateA_Diffuse = (floor(angle * safeSteps + 0.5) / safeSteps) * dist * crispPointShadow;

        // STATE B: UNIFIED (Biased)
        float totalIntensity = angle * dist * shadow; 
        float stateB_Diffuse = floor(totalIntensity * safeSteps + 0.5) / safeSteps;

        // DIFFUSE BLEND
        float finalDiffuse = lerp(stateA_Diffuse, stateB_Diffuse, DistanceBandingSlider.x);
        diffuseAccumulation += light.color.rgb * finalDiffuse;

        // --- FIX 5: Pre-Threshold Specular Blending ---
        float3 halfVector = SafeNormalize(light.direction + V);
        float NdotH = saturate(dot(N, halfVector));
        float specRaw = pow(NdotH, specPower);
        
        float specIntensityA = specRaw * dist * crispPointShadow;
        float specIntensityB = specRaw * dist * shadow;
        float blendedSpecIntensity = lerp(specIntensityA, specIntensityB, DistanceBandingSlider.x);

        float finalSpec = step(0.5, blendedSpecIntensity) * specOpacity;

        specularAccumulation += light.color.rgb * specTint * finalSpec;
        
    LIGHT_LOOP_END
#endif

    // ----------------------------------------------------
    // 3. FINAL COMPOSITION
    // ----------------------------------------------------
    OutColor = (diffuseColor * diffuseAccumulation) + specularAccumulation + Emission.rgb;

#else        
    // Fallback display for the Shader Graph preview window
    float3 N = SafeNormalize(Normal);
    float safeSteps = max(Steps.x, 1.0);
    float previewNdotL = saturate(dot(N, float3(0.5, 0.7, 0)));
    float previewBanded = floor(previewNdotL * safeSteps + 0.5) / safeSteps;
    float3 diffuseColor = Base_Color.rgb * lerp(1.0, 0.2, Metallic.x);
    OutColor = (diffuseColor * previewBanded * AmbientOcclusion.x) + Emission.rgb;
#endif
}