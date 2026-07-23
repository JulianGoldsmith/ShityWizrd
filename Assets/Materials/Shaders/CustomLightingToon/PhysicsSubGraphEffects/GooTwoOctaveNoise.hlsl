#ifndef SOULSPELL_GOO_TWO_OCTAVE_NOISE_INCLUDED
#define SOULSPELL_GOO_TWO_OCTAVE_NOISE_INCLUDED
float GooHash31(float3 value)
{
    uint3 hashValue = (uint3) (int3) value;
    hashValue.x ^= 1103515245U;
    hashValue.y ^= hashValue.x + hashValue.z;
    hashValue.y *= 134775813U;
    hashValue.z += hashValue.x ^ hashValue.y;
    hashValue.y += hashValue.x ^ hashValue.z;
    hashValue.x += hashValue.y * hashValue.z;
    hashValue.x *= 0x27d4eb2dU;
    return (hashValue.x >> 8) * (1.0 / 16777215.0);
}

float GooValueNoise3D(float3 position)
{
    
    float3 cell = floor(position);
    float3 cellPosition = frac(position);
    float3 blend = cellPosition * cellPosition * (3.0 - 2.0 * cellPosition);

    float value000 = GooHash31(cell + float3(0.0, 0.0, 0.0));
    float value100 = GooHash31(cell + float3(1.0, 0.0, 0.0));
    float value010 = GooHash31(cell + float3(0.0, 1.0, 0.0));
    float value110 = GooHash31(cell + float3(1.0, 1.0, 0.0));
    float value001 = GooHash31(cell + float3(0.0, 0.0, 1.0));
    float value101 = GooHash31(cell + float3(1.0, 0.0, 1.0));
    float value011 = GooHash31(cell + float3(0.0, 1.0, 1.0));
    float value111 = GooHash31(cell + float3(1.0, 1.0, 1.0));

    float value00 = lerp(value000, value100, blend.x);
    float value10 = lerp(value010, value110, blend.x);
    float value01 = lerp(value001, value101, blend.x);
    float value11 = lerp(value011, value111, blend.x);
    float value0 = lerp(value00, value10, blend.y);
    float value1 = lerp(value01, value11, blend.y);

    return lerp(value0, value1, blend.z);
}

void GooTwoOctaveNoise_float(float3 Position, float Scale, float DetailStrength, out float Out)
{
    float safeScale = max(abs(Scale), 0.0001);
    float safeDetailStrength = max(DetailStrength, 0.0);
    float3 noisePosition = Position * safeScale;

    float mainNoise = GooValueNoise3D(noisePosition);
    float detailNoise = GooValueNoise3D(noisePosition * 2.03 + float3(17.13, 31.71, 11.47));

    Out = (mainNoise + detailNoise * safeDetailStrength) / (1.0 + safeDetailStrength);
}

void GooTwoOctaveNoise_half(half3 Position, half Scale, half DetailStrength, out half Out)
{
    float noise;
    GooTwoOctaveNoise_float(Position, Scale, DetailStrength, noise);
    Out = (half) noise;
}

#endif