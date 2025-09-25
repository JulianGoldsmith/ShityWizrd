using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

[CreateAssetMenu(fileName = "OreBlobs", menuName = "TerrainGen/GenerationFunctions/OreBlobs")]
public class OreBlobsGF : GenerationFunctions
{
    public List<SDFPrimitive> oreShapes = new List<SDFPrimitive>();
    public List<NoiseLayer> oreShapesNoise = new List<NoiseLayer>();

    public VoxelMat oreMaterial = VoxelMat.Moss;

    public override GenerationFunctions InitAndClone(GeneratedRoom roomInstance)
    {
        //give shape an offset(relative to the room center) that leaves it within the bounds of the room using its size,
        OreBlobsGF fn = (OreBlobsGF)base.InitAndClone(roomInstance);

        var newShapes = new List<SDFPrimitive>(fn.oreShapes.Count);

        float roomRadius = roomInstance.radius;
        foreach (SDFPrimitive shape in fn.oreShapes)
        {
            shape.offset.x = Random.Range(-(roomRadius - shape.size.x ), roomRadius - shape.size.x);
            shape.offset.y = Random.Range(-(roomRadius - shape.size.y ), roomRadius - shape.size.y );
            shape.offset.z = Random.Range(-(roomRadius - shape.size.z ), roomRadius - shape.size.z );
        }
        return fn;
    }

    public override VoxelData GenerateVoxelData(VoxelData voxelData, Vector3 pos, GeneratedRoom roomInstance)
    {
        float cap = roomInstance.sdfCap;
        float smoothing = 0f;

        float density = voxelData.density;

        float roomRadius = roomInstance.radius;

        float blobSolid = -cap; // start at eg -10

        foreach (SDFPrimitive shape in oreShapes)
        {
            //rooms
            Vector3 center = roomInstance.pos + shape.offset;

            float shapePosInside = shape.Generate(pos, roomInstance.pos); //generate a positive inside shape 
            
            foreach (var n in oreShapesNoise)
            {
                if (n.enabled)
                {
                    float influenceMultiplier = CalculateInfluenceMultiplier(n.influences, voxelData);
                    float noiseValue = Noise.Generate(pos.x * n.frequency, pos.y * n.frequency, pos.z * n.frequency) * n.amplitude * influenceMultiplier;
                    shapePosInside -= noiseValue;
                }
                
            }

            blobSolid = SDFHelpers.Union(blobSolid, shapePosInside, cap, softTruncate: true, k: smoothing); //union the positive shapes with smoothing and truncation

        }

        
        var before = density;
        density = SDFHelpers.Union(density, blobSolid, cap, softTruncate: true, k: smoothing);

        if (blobSolid > 0f && density > 0f)
        {
            voxelData.matId0 = (byte)VoxelMat.Moss;
            //MaterialFields.HardSetMaterial(voxelData, (int)VoxelMat.Moss);
            //voxelData.matId0 = (byte)VoxelMat.Moss;
        }

        voxelData.density = density;

        return voxelData;
    }


}

