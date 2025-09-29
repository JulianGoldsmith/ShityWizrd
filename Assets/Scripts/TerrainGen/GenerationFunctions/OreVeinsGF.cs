using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

[CreateAssetMenu(fileName = "OreVeinsGF", menuName = "TerrainGen/GenerationFunctions/OreVeins")]
public class OreVeinsGF : GenerationFunctions
{

    public List<NoiseLayer> oreNoise = new List<NoiseLayer>();
    [Header("Use between two values to carve")]
    public float minNoise = -0.1f, maxNoise = 0.1f;

    public VoxelMat oreMaterial = VoxelMat.Crystal;

    public override GenerationFunctions InitAndClone(GeneratedRoom roomInstance)
    {

        OreVeinsGF fn = (OreVeinsGF)base.InitAndClone(roomInstance);

        return fn;
    }

    public override VoxelData GenerateVoxelData(VoxelData voxelData, Vector3 pos, GeneratedRoom roomInstance)
    {
        float cap = roomInstance.sdfCap;
        float smoothing = 0f;

        float density = voxelData.density;

        float noise = 0;
        foreach (var n in oreNoise)
        {
            if (n.enabled)
            {
                float influenceMultiplier = CalculateInfluenceMultiplier(n.influences, voxelData);
                float noiseValue = Noise.Generate(pos.x * n.frequency, pos.y * n.frequency, pos.z * n.frequency) * n.amplitude * influenceMultiplier;
                noise += noiseValue;
            }
                
        }

        float a = Mathf.Min(minNoise, maxNoise);
        float b = Mathf.Max(minNoise, maxNoise);
        float mid = 0.5f * (a + b);
        float half = Mathf.Max(1e-6f, 0.5f * (b - a));

        float bandT = 1f - (Mathf.Abs(noise - mid) / half);

        float oreSolid;
        if (bandT > 0f)
        {

            oreSolid = bandT * cap;
        }
        else
        {
            oreSolid = -cap; 
        }

        float before = density;
        density = SDFHelpers.Union(density, oreSolid, cap, softTruncate: true, k: smoothing);

        if (bandT > 0f && density > 0f)
        {
            voxelData.matId0 = (byte)oreMaterial;

        }

        voxelData.density = density;

        return voxelData;
    }


}

