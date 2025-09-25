using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "New Room Profile", menuName = "TerrainGen/RoomProfile")]
public class RoomGenerationProfile : ScriptableObject
{
    public int minRadius, maxRadius;

    public List<GenerationFunctions> generationFunctions;

    public VoxelData GenerateVoxel(Vector3 point, GeneratedRoom roomInstance, VoxelData voxel)
    {
        foreach (GenerationFunctions function in generationFunctions)
        {
            voxel = function.GenerateVoxelData(voxel, point, roomInstance);
        }

        return voxel;
    }

}

public class GeneratedRoom
{
    public Vector3 pos;
    public float radius; 
    public RoomGenerationProfile profileClone;
    public float sdfCap;

    public GeneratedRoom(Vector3 _pos, float _radius, RoomGenerationProfile _profile, float sdfCap)
    {
        pos = _pos;
        profileClone = ScriptableObject.Instantiate(_profile);
        radius = Random.Range(profileClone.minRadius, profileClone.maxRadius);

        var cloned = new List<GenerationFunctions>(profileClone.generationFunctions.Count);
        this.sdfCap = sdfCap;

        foreach (GenerationFunctions gf in profileClone.generationFunctions)
        {
            var gfClone = gf.InitAndClone(this);
            cloned.Add(gfClone);
        }
        profileClone.generationFunctions = cloned;
       
    }

}

