// Schlick's Bias Function
float SchlickBias(float x, float bias)
{
    bias = clamp(bias, 0.0001, 0.9999);
    return x / ((1.0 / bias - 2.0) * (1.0 - x) + 1.0);
}

void CalculateToonLighting_float(
    float3 Base_Color,
    float3 Normal,
    float Smoothness,
    float Metallic,
    float3 Emission,
    float AmbientOcclusion,
    UnitySamplerState RampSampler,
    UnityTexture2D ColorRamp,
    float DistanceBandingSlider,
    float ConeBandingSlider,
    float AngleCurve,
    float DistanceCurve,
    float ConeCurve,
    float ShadowStrength,
    float BandCount,
    float CombineLights, // 0.0 = Individual (Mixing), 1.0 = Combined (Metaballs)
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
    
    // Protect against division by zero if slider hits 0
    float safeBands = max(BandCount, 1.0);

#ifndef SHADERGRAPH_PREVIEW
#ifdef SHADER_STAGE_FRAGMENT
    
    float3 N = SafeNormalize(Normal);
    float3 V = SafeNormalize(ViewDirWS);

#if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)            
    float3 ambientLight = SampleSH(N);
#else    
    float3 ambientLight = float3(0.1, 0.1, 0.15);
#endif
    
    // Setup Diffuse accumulators
    float3 baseDiffuse = ambientLight * AmbientOcclusion;
    float3 individualDiffuse = 0;
    // Change this to start at the ambient intensity!
    float combinedIntensity = max(ambientLight.r, max(ambientLight.g, ambientLight.b)) * AmbientOcclusion; 
    
    // Change this to start with the ambient color!
    float3 combinedColor = ambientLight * AmbientOcclusion;
    
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

    float mainNdotL = saturate(dot(N, mainLight.direction) * 0.5 + 0.5);
    float mainVisualAngle = SchlickBias(mainNdotL, AngleCurve);
    float mainVisualShadow = lerp(1.0, mainLight.shadowAttenuation, ShadowStrength);
    float mainShape = mainVisualAngle * mainVisualShadow;
    
    float mainIntensity = max(mainLight.color.r, max(mainLight.color.g, mainLight.color.b));
    float3 mainHue = mainLight.color.rgb / max(mainIntensity, 0.00001);
    
    // --- BRANCH A: COMBINED LIGHTS ---
    if (CombineLights > 0.5)
    {
        combinedIntensity += mainShape * mainIntensity;
        combinedColor += mainHue * mainShape * mainIntensity;
    }
    // --- BRANCH B: INDIVIDUAL LIGHTS ---
    else
    {
        float mainHdrPush = max(1.0, mainIntensity);
        float mainHdrShape = mainShape * mainHdrPush;
        
        float mainStepped = floor(mainHdrShape * safeBands) / safeBands;
        float mainSafeUV = (floor(saturate(mainHdrShape) * 0.999 * safeBands) + 0.5) / safeBands;
        float3 mainPainted = SAMPLE_TEXTURE2D_LOD(ColorRamp.tex, RampSampler.samplerstate, float2(mainSafeUV, 0.5), 0).rgb;
        
        float maxMainStep = (safeBands - 1.0) / safeBands;
        float mainExtra = max(0.0, mainStepped - maxMainStep);
        float mainHdrMult = min(mainIntensity, 1.0 + mainExtra);
        
        float mainFinalScalar = lerp(mainIntensity, mainHdrMult, step(1.0, mainIntensity));
        individualDiffuse += mainHue * mainPainted * mainFinalScalar;
    }

    // Specular
    float3 halfVector = SafeNormalize(mainLight.direction + V);
    float specRaw = pow(saturate(dot(N, halfVector)), specPower);
    specularAccumulation += mainLight.color.rgb * specTint * (step(0.5, specRaw * mainLight.shadowAttenuation) * specOpacity);

   // ----------------------------------------------------
    // 2. RIM LIGHTING (Physical Toon Rim)
    // ----------------------------------------------------
    // 1. Pure, un-crushed Fresnel
    float fresnel = saturate(1.0 - dot(N, V));
    
    // 2. Thickness: RimEdge now acts as a perfect 0-1 thickness slider
    float rimThickness = 1.0 - saturate(RimEdge);
    
    // 3. Softness: RimPower now controls the feathering of the toon edge. 
    // (A power of 0.01 = razor sharp, 3.0 = soft and fuzzy)
    float softness = max(RimPower * 0.05, 0.001);
    float rimStep = smoothstep(rimThickness - softness, rimThickness + softness, fresnel) * step(0.001, RimEdge);
    
    // 4. Physical Masking: Rim light MUST have a light source!
    // This allows a tiny wrap around the terminator line, but goes pitch black in the dark.
    float litSideMask = smoothstep(-0.1, 0.1, dot(N, mainLight.direction));
    
    // It also strictly turns off if the object is inside a cast shadow!
    float physicalMask = litSideMask * mainLight.shadowAttenuation;
    
    float3 rimAccumulation = RimColor * rimStep * physicalMask * AmbientOcclusion;
    // ----------------------------------------------------
    // 3. ADDITIONAL LIGHTS
    // ----------------------------------------------------
#if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)    
    uint pixelLightCount = GetAdditionalLightsCount();

#if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)    
    InputData inputData = (InputData)0;
    inputData.normalizedScreenSpaceUV = ScreenPosition.xy; 
    inputData.positionWS = PositionWS;
#endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, PositionWS);
        light.shadowAttenuation = AdditionalLightRealtimeShadow(lightIndex, PositionWS, light.direction);
        
        // ==========================================================
        // BUG FIXED: SAFE FORWARD+ BUFFER EXTRACTION
        // We now ask the LightList for the TRUE global index of the light!
        // ==========================================================
        float3 lightPosWS;
        float4 attenuation;
        float3 spotDirection;

#if defined(USE_FORWARD_PLUS)
            int realIndex = urp_LightList[lightIndex];
            lightPosWS = urp_LightBuffer[realIndex].position.xyz;
            attenuation = urp_LightBuffer[realIndex].attenuation;
            spotDirection = urp_LightBuffer[realIndex].spotDirection.xyz;
#else
            lightPosWS = _AdditionalLightsPosition[lightIndex].xyz;
            attenuation = _AdditionalLightsAttenuation[lightIndex];
            spotDirection = _AdditionalLightsSpotDir[lightIndex].xyz;
#endif

        float NdotL_Add = saturate(dot(N, light.direction) * 0.5 + 0.5);
        float visualAngle = SchlickBias(NdotL_Add, AngleCurve);

        // AXIS 2: DISTANCE
        float visualDist = 1.0;
        float linearDist = 1.0; 
        
        // Safeguard to prevent division by zero on corrupt buffer data
        float safeAttenX = max(attenuation.x, 0.0000001); 
        float trueDist = length(lightPosWS - PositionWS);
        float lightRange = rsqrt(safeAttenX);
        linearDist = saturate(1.0 - (trueDist / lightRange));
        visualDist = pow(linearDist, DistanceCurve);

        // AXIS 3: CONE
        float SdotL = dot(spotDirection, light.direction);
        float rawCone = saturate(SdotL * attenuation.z + attenuation.w);
        float pureCone = rawCone * rawCone;
        float visualCone = pow(pureCone, ConeCurve);

        float visualShadow = lerp(1.0, light.shadowAttenuation, ShadowStrength);
        float distBlend = lerp(1.0, visualDist, DistanceBandingSlider);
        float coneBlend = lerp(1.0, visualCone, ConeBandingSlider);
        
        float rawShape = visualAngle * distBlend * coneBlend * visualShadow;
        
        float intensity = max(light.color.r, max(light.color.g, light.color.b));
        float3 hue = light.color.rgb / max(intensity, 0.00001);
        
        // THE ANTI-ALIASING MASKS (Using pure local bounds, not Unity's gradient)
        float distMask = smoothstep(0.0, 0.05, linearDist);
        float coneMask = smoothstep(0.0, 0.05, rawCone);
        float edgeMask = distMask * coneMask;

        // --- BRANCH A: COMBINED LIGHTS ---
        if (CombineLights > 0.5)
        {
            combinedIntensity += rawShape * intensity * edgeMask;
            combinedColor += hue * rawShape * intensity * edgeMask;
        }
        // --- BRANCH B: INDIVIDUAL LIGHTS ---
        else
        {
            float hdrPush = max(1.0, intensity);
            float hdrShape = rawShape * hdrPush;
            
            float steppedBands = floor(hdrShape * safeBands) / safeBands;
            float safeUV = (floor(saturate(hdrShape) * 0.999 * safeBands) + 0.5) / safeBands;
            float3 colorAdd = SAMPLE_TEXTURE2D_LOD(ColorRamp.tex, RampSampler.samplerstate, float2(safeUV, 0.5), 0).rgb;
            
            float maxTexStep = (safeBands - 1.0) / safeBands;
            float extraSteps = max(0.0, steppedBands - maxTexStep);
            float hdrMult = min(intensity, 1.0 + extraSteps);
            
            float finalScalar = lerp(intensity, hdrMult, step(1.0, intensity));
            
            individualDiffuse += hue * colorAdd * finalScalar * edgeMask;
        }
        
    LIGHT_LOOP_END
#endif

    // ----------------------------------------------------
    // 4. COMBINED POST-PROCESS
    // ----------------------------------------------------
    float3 finalDiffuseAccumulation = baseDiffuse + individualDiffuse;

    if (CombineLights > 0.5)
    {
        float steppedBands = floor(combinedIntensity * safeBands) / safeBands;
        float safeUV = (floor(saturate(combinedIntensity) * 0.999 * safeBands) + 0.5) / safeBands;
        float3 colorAdd = SAMPLE_TEXTURE2D_LOD(ColorRamp.tex, RampSampler.samplerstate, float2(safeUV, 0.5), 0).rgb;
        
        float maxTexStep = (safeBands - 1.0) / safeBands;
        float extraSteps = max(0.0, steppedBands - maxTexStep);
        float hdrMult = min(combinedIntensity, 1.0 + extraSteps);
        
        float finalScalar = lerp(combinedIntensity, hdrMult, step(1.0, combinedIntensity));
        
        float3 averageHue = combinedColor / max(combinedIntensity, 0.00001);
        
        finalDiffuseAccumulation = baseDiffuse + (averageHue * colorAdd * finalScalar);
    }

    // --- FINAL COMPOSITION ---
    OutColor = (diffuseColor * finalDiffuseAccumulation) + specularAccumulation + rimAccumulation;
    OutEmission = Emission;

#endif
#else    
    float3 N_preview = SafeNormalize(Normal);
    float3 NdotL_preview = saturate(dot(N_preview, normalize(float3(0.5, 0.5, 0.2))) * 0.5 + 0.5);
    OutColor = Base_Color * NdotL_preview;
    OutEmission = Emission;
#endif
}