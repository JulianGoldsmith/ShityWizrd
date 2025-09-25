using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "New World Profile", menuName = "TerrainGen/World Generation Profile")]
public class WorldGenerationProfile : ScriptableObject
{
    public Vector3Int boundsSize = new Vector3Int(150, 80, 150);
    public Vector3Int gridSize = new Vector3Int(150, 80, 150);

    public VoxelMat defaultMaterial = VoxelMat.Stone;

    [Header("abstract room gen properties")]
    [Header("Must be atlease 2x roomRadius")]
    public float minRoomDistance = 35;
    public float roomRadius = 24f;
    public int rejectionSamples = 34;

    [Header("Room profiles")]
    public RoomGenerationProfile startRoom, endRoom, defaultRoom;

    [Header("Extra connections to add in after MST finds single route")]
    public float extraConnectionPercentage = 0.35f;

    [Header("Connection Tunnels")]
    public float tunnelRadius = 3f;

    [Header("Macro Environmental Noise")]
    public NoiseLayer humidityNoise;
    public NoiseLayer temperatureNoise;

    [Header("Biome Definitions")]
    public List<BiomeProfile> biomes;

    public BiomeProfile FindBiome(byte id)
    {
        return biomes.FirstOrDefault(b => b.biomeID == id);
    }
}