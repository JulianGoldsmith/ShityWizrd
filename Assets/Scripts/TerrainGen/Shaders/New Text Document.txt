
inline float densityR(UnityTexture3D t, UnitySamplerState s, float3 uvw)
{
    return SAMPLE_TEXTURE3D(t, s, uvw).r; 
}

inline float idR01(UnityTexture3D t, UnitySamplerState s, float3 uvw)
{
    return SAMPLE_TEXTURE3D(t, s, uvw).r;
}

void PickMatFromCorners_float(
    float3 Pclamp,
    float3 GridDims,
    UnityTexture3D DensityTex, UnitySamplerState DensityTex_sampler,
    UnityTexture3D MatIdTex, UnitySamplerState MatIdTex_sampler,
    out float MatIndex01
) {
    float3 i0 = floor(Pclamp);
    float3 i1 = min(i0 + 1.0, GridDims - 1.0);

    float3 corners[8];
    corners[0] = float3(i0.x, i0.y, i0.z);
    corners[1] = float3(i1.x, i0.y, i0.z);
    corners[2] = float3(i0.x, i1.y, i0.z);
    corners[3] = float3(i1.x, i1.y, i0.z);
    corners[4] = float3(i0.x, i0.y, i1.z);
    corners[5] = float3(i1.x, i0.y, i1.z);
    corners[6] = float3(i0.x, i1.y, i1.z);
    corners[7] = float3(i1.x, i1.y, i1.z);

    float3 dimsMinus1 = max(GridDims - 1.0, 1.0);
    float bestScore = -1e20;
    float bestMat01 = 1.0; 

    [unroll]
        for (int c = 0; c < 8; ++c)
        {
            float3 corner = corners[c];
            float3 uvw = corner / dimsMinus1;

            float dn = densityR(DensityTex, DensityTex_sampler, uvw); 
            if (dn <= 0.0f) continue; 

            float dist = length(Pclamp - corner);
            dist = max(dist, 1e-4);

            float score = dn / dist;
            if (score > bestScore)
            {
                bestScore = score;
                bestMat01 = idR01(MatIdTex, MatIdTex_sampler, uvw); 
            }
        }

    MatIndex01 = bestMat01;
}