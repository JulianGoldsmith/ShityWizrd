using System.Collections.Generic;
using UnityEngine;
using static SDFGenerator;

[CreateAssetMenu(fileName = "CaveRoomGenerationProfile", menuName = "TerrainGen/CaveRoomGenerationProfile")]
public class CaveRoomGenerationProfile : RoomGenerationProfile
{


    [Header("Floor")]
    public bool generateFloor = true;
    [Range(0, 1)]
    public float baseFloorHeightRatio = 0.7f;
    public NoiseLayer floorNoise;

 

}
