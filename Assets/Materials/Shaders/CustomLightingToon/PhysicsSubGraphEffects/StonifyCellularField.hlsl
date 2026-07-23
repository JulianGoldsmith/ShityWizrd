#ifndef SOULSPELL_STONIFY_CELLULAR_FIELD_INCLUDED
#define SOULSPELL_STONIFY_CELLULAR_FIELD_INCLUDED

float StonifyHash13(float3 value)
{
    value = frac(value * 0.1031);
    value += dot(value, value.yzx + 33.33);
    return frac((value.x + value.y) * value.z);
}

float3 StonifyHash33(float3 value)
{
    value = float3(dot(value, float3(127.1, 311.7, 74.7)), dot(value, float3(269.5, 183.3, 246.1)), dot(value, float3(113.5, 271.9, 124.6)));
    return frac(sin(value) * 43758.5453);
}

float StonifyValueNoise(float3 position)
{
    float3 cell = floor(position);
    float3 localPosition = frac(position);
    float3 blend = localPosition * localPosition * (3.0 - 2.0 * localPosition);

    float value000 = StonifyHash13(cell + float3(0.0, 0.0, 0.0));
    float value100 = StonifyHash13(cell + float3(1.0, 0.0, 0.0));
    float value010 = StonifyHash13(cell + float3(0.0, 1.0, 0.0));
    float value110 = StonifyHash13(cell + float3(1.0, 1.0, 0.0));
    float value001 = StonifyHash13(cell + float3(0.0, 0.0, 1.0));
    float value101 = StonifyHash13(cell + float3(1.0, 0.0, 1.0));
    float value011 = StonifyHash13(cell + float3(0.0, 1.0, 1.0));
    float value111 = StonifyHash13(cell + float3(1.0, 1.0, 1.0));

    float value00 = lerp(value000, value100, blend.x);
    float value10 = lerp(value010, value110, blend.x);
    float value01 = lerp(value001, value101, blend.x);
    float value11 = lerp(value011, value111, blend.x);
    float value0 = lerp(value00, value10, blend.y);
    float value1 = lerp(value01, value11, blend.y);
    return lerp(value0, value1, blend.z);
}

void StonifyCellularField_float(float3 Position, float Stoneified, float3 Seed, float RegionScale, float BlendWidth, float BreakupStrength, float DetailScale, out float MacroField, out float StoneMask)
{
    float amount = saturate(Stoneified);

    if (amount <= 0.0)
    {
        MacroField = 0.0;
        StoneMask = 0.0;
        return;
    }

    if (amount >= 1.0)
    {
        MacroField = 1.0;
        StoneMask = 1.0;
        return;
    }

    float safeRegionScale = max(abs(RegionScale), 0.0001);
    float safeDetailScale = max(abs(DetailScale), 0.0001);
    float3 seededPosition = Position * safeRegionScale + Seed;
    float3 baseCell = floor(seededPosition);
    float nearestCellDistance = 100.0;
    float nearestActiveDistance = 100.0;
    float nearestInactiveDistance = 100.0;
    float closestActivationValue = 0.0;

    [unroll]
    for (int z = -1; z <= 1; z++)
    {
        [unroll]
        for (int y = -1; y <= 1; y++)
        {
            [unroll]
            for (int x = -1; x <= 1; x++)
            {
                float3 cell = baseCell + float3(x, y, z);
                float3 cellHashPosition = cell + Seed * float3(1.37, 2.11, 2.73);
                float3 cellPoint = cell + lerp(0.15, 0.85, StonifyHash33(cellHashPosition));
                float cellDistance = distance(seededPosition, cellPoint);
                float activationValue = StonifyHash13(cellHashPosition + 19.19);

                if (cellDistance < nearestCellDistance)
                {
                    nearestCellDistance = cellDistance;
                    closestActivationValue = activationValue;
                }

                if (activationValue <= amount)
                {
                    nearestActiveDistance = min(nearestActiveDistance, cellDistance);
                }
                else
                {
                    nearestInactiveDistance = min(nearestInactiveDistance, cellDistance);
                }
            }
        }
    }

    MacroField = closestActivationValue;

    if (nearestActiveDistance >= 99.0)
    {
        StoneMask = 0.0;
        return;
    }

    if (nearestInactiveDistance >= 99.0)
    {
        StoneMask = 1.0;
        return;
    }

    float detailNoise = StonifyValueNoise(seededPosition * safeDetailScale + Seed * 0.73);
    float boundaryDistance = nearestInactiveDistance - nearestActiveDistance;
    boundaryDistance += (detailNoise * 2.0 - 1.0) * max(BreakupStrength, 0.0);
    float safeBlendWidth = max(abs(BlendWidth), 0.0001);
    StoneMask = smoothstep(-safeBlendWidth, safeBlendWidth, boundaryDistance);
}

void StonifyCellularField_half(half3 Position, half Stoneified, half3 Seed, half RegionScale, half BlendWidth, half BreakupStrength, half DetailScale, out half MacroField, out half StoneMask)
{
    float macroField;
    float stoneMask;
    StonifyCellularField_float(Position, Stoneified, Seed, RegionScale, BlendWidth, BreakupStrength, DetailScale, macroField, stoneMask);
    MacroField = (half)macroField;
    StoneMask = (half)stoneMask;
}

#endif
