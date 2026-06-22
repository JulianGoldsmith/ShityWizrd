// The function name MUST end in _float for Shader Graph to find it
void CalculateToonLighting_float(
    float3 Base_Color,
    float3 Normal,
    float Smoothness,
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
    // Base accumulations
    float3 diffuseAccumulation = float3(0, 0, 0);
    float3 specularAccumulation = float3(0, 0, 0);

    // Convert Unity's 0-1 Smoothness to a harsh Specular Power
    float specPower = exp2(10 * Smoothness + 1);
    float specMultiplier = step(0.001, Smoothness);

    // ----------------------------------------------------
    // 1. MAIN LIGHT (The Sun)
    // ----------------------------------------------------
#if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)            
    float4 shadowCoord = TransformWorldToShadowCoord(PositionWS);
    Light mainLight = GetMainLight(shadowCoord);
#else
    Light mainLight = GetMainLight();
#endif

    // METHOD B: Force the URP Soft Shadow into a razor-sharp mask
    float crispSunShadow = step(0.5, mainLight.shadowAttenuation);

    // Main Light Diffuse
    float mainNdotL = dot(Normal, mainLight.direction);
    float mainAngle = saturate(mainNdotL * 0.5 + 0.5);
    float mainBandedAngle = floor(mainAngle * Steps) / Steps;
        
    diffuseAccumulation += mainLight.color * (mainBandedAngle * crispSunShadow);

    // Main Light Specular
    float3 mainHalfVector = SafeNormalize(mainLight.direction + ViewDirWS);
    float mainNdotH = saturate(dot(Normal, mainHalfVector));
    float mainSpecRaw = pow(mainNdotH, specPower);
    float mainSpecBanded = step(0.5, mainSpecRaw * crispSunShadow) * specMultiplier;
        
    specularAccumulation += mainLight.color * mainSpecBanded;

    // ----------------------------------------------------
    // 2. ADDITIONAL LIGHTS (Spells, Torches)
    // ----------------------------------------------------
#if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)            
    uint pixelLightCount = GetAdditionalLightsCount();
            
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, PositionWS);
        light.shadowAttenuation = AdditionalLightRealtimeShadow(lightIndex, PositionWS, light.direction);
                
        // METHOD B: Force the point light soft shadow into a sharp mask
        float crispPointShadow = step(0.5, light.shadowAttenuation);
                
        // --- DISTANCE BANDING LOGIC ---
        // 1. Calculate the smooth distance falloff
        float smoothDist = light.distanceAttenuation * crispPointShadow;
        // 2. Calculate the posterized, chunky ring falloff
        float bandedDist = floor(smoothDist * Steps) / Steps;
        // 3. Blend them together based on the Material Slider
        float atten = lerp(smoothDist, bandedDist, DistanceBandingSlider);

        // Point Light Diffuse
        float NdotL = dot(Normal, light.direction);
        float angle = saturate(NdotL * 0.5 + 0.5);
        float bandedAngle = floor(angle * Steps) / Steps;

        diffuseAccumulation += light.color * (bandedAngle * atten);

        // Point Light Specular
        float3 halfVector = SafeNormalize(light.direction + ViewDirWS);
        float NdotH = saturate(dot(Normal, halfVector));
        float specRaw = pow(NdotH, specPower);
        float specBanded = step(0.5, specRaw * atten) * specMultiplier;

        specularAccumulation += light.color * specBanded;
    LIGHT_LOOP_END
#endif

    // FINAL COMPOSITION
    OutColor = (Base_Color * diffuseAccumulation * AmbientOcclusion) + specularAccumulation + Emission;

#else        
    // Fallback display for the Shader Graph preview window
    float previewNdotL = saturate(dot(Normal, float3(0.5, 0.7, 0)));
    float previewBanded = floor(previewNdotL * Steps) / Steps;
    OutColor = (Base_Color * previewBanded * AmbientOcclusion) + Emission;
#endif
}