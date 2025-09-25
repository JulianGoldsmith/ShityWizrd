using UnityEngine;
using System.Collections.Generic;

public static class EnvironmentGenerator
{
    public static void CalculateMacroEnvironment(Dictionary<Vector3Int, Chunk> chunks, WorldGenerationProfile profile, Vector3Int chunkSize, Vector3Int gridSize, Vector3 boundsSize)
    {
        Vector3 scale = new Vector3(
            (float)boundsSize.x / gridSize.x,
            (float)boundsSize.y / gridSize.y,
            (float)boundsSize.z / gridSize.z
        );

        foreach (var chunk in chunks.Values)
        {
            Vector3 chunkWorldPos = new Vector3(
                chunk.coord.x * chunkSize.x * scale.x,
                chunk.coord.y * chunkSize.y * scale.y,
                chunk.coord.z * chunkSize.z * scale.z
            );

            for (int x = 0; x < chunk.grid.GetLength(0); x++)
            {
                for (int y = 0; y < chunk.grid.GetLength(1); y++)
                {
                    for (int z = 0; z < chunk.grid.GetLength(2); z++)
                    {
                        Vector3 currentPoint = chunkWorldPos + new Vector3(x * scale.x, y * scale.y, z * scale.z);

                        if (profile.humidityNoise.enabled)
                        {
                            float noiseValue = Noise.Generate(
                                currentPoint.x * profile.humidityNoise.frequency,
                                currentPoint.y * profile.humidityNoise.frequency,
                                currentPoint.z * profile.humidityNoise.frequency
                            );
                            chunk.grid[x, y, z].humidity = (byte)((noiseValue * 0.5f + 0.5f) * 255);
                        }

                        if (profile.temperatureNoise.enabled)
                        {
                            float noiseValue = Noise.Generate(
                                currentPoint.x * profile.temperatureNoise.frequency,
                                currentPoint.y * profile.temperatureNoise.frequency,
                                currentPoint.z * profile.temperatureNoise.frequency
                            );
                            chunk.grid[x, y, z].temperature = (byte)((noiseValue * 0.5f + 0.5f) * 255);
                        }
                    }
                }
            }
        }
    }


    public static void CalculateMicroEnvironment(Dictionary<Vector3Int, Chunk> chunks, Vector3Int chunkSize)
    {
        int sizeX = chunkSize.x;
        int sizeY = chunkSize.y;
        int sizeZ = chunkSize.z;

        foreach (var chunk in chunks.Values)
        {
            for (int x = 0; x <= sizeX; x++)
            {
                for (int y = 0; y <= sizeY; y++)
                {
                    for (int z = 0; z <= sizeZ; z++)
                    {
                        if (Mathf.Abs(chunk.grid[x, y, z].density) < 2.0f)
                        {
                            Vector3 normal;

                            bool isInterior = (x > 0 && x < sizeX && y > 0 && y < sizeY && z > 0 && z < sizeZ);

                            if (isInterior)
                            {

                                float gradX = chunk.grid[x + 1, y, z].density - chunk.grid[x - 1, y, z].density;
                                float gradY = chunk.grid[x, y + 1, z].density - chunk.grid[x, y - 1, z].density;
                                float gradZ = chunk.grid[x, y, z + 1].density - chunk.grid[x, y, z - 1].density;
                                normal = new Vector3(gradX, gradY, gradZ).normalized;
                            }
                            else
                            {

                                int worldX = chunk.coord.x * chunkSize.x + x;
                                int worldY = chunk.coord.y * chunkSize.y + y;
                                int worldZ = chunk.coord.z * chunkSize.z + z;

                                float gradX = GetVoxelAt(worldX + 1, worldY, worldZ, chunks, chunkSize).density - GetVoxelAt(worldX - 1, worldY, worldZ, chunks, chunkSize).density;
                                float gradY = GetVoxelAt(worldX, worldY + 1, worldZ, chunks, chunkSize).density - GetVoxelAt(worldX, worldY - 1, worldZ, chunks, chunkSize).density;
                                float gradZ = GetVoxelAt(worldX, worldY, worldZ + 1, chunks, chunkSize).density - GetVoxelAt(worldX, worldY, worldZ - 1, chunks, chunkSize).density;
                                normal = new Vector3(gradX, gradY, gradZ).normalized;
                            }

                            float slopeAngle = Vector3.Angle(Vector3.up, normal);
                            chunk.grid[x, y, z].slope = (byte)Mathf.Clamp((slopeAngle / 90f) * 255, 0, 255);
                            chunk.grid[x, y, z].verticality = (byte)((normal.y * 0.5f + 0.5f) * 255);
                        }
                    }
                }
            }
        }
    }


    public static void SelectBiomes(Dictionary<Vector3Int, Chunk> chunks, WorldGenerationProfile profile)
    {
        foreach (var chunk in chunks.Values)
        {
            for (int x = 0; x < chunk.grid.GetLength(0); x++)
                for (int y = 0; y < chunk.grid.GetLength(1); y++)
                    for (int z = 0; z < chunk.grid.GetLength(2); z++)
                    {
         
                        ref VoxelData voxel = ref chunk.grid[x, y, z];

                        foreach (var biome in profile.biomes)
                        {
                            bool conditionsMet = true;
                            foreach (var condition in biome.conditions)
                            {
                                byte valueToCheck = 0;
                                switch (condition.sourceType)
                                {
                                    case VoxelDataType.Humidity: valueToCheck = voxel.humidity; break;
                                    case VoxelDataType.Temperature: valueToCheck = voxel.temperature; break;
                                    case VoxelDataType.Slope: valueToCheck = voxel.slope; break;
                                    case VoxelDataType.Verticality: valueToCheck = voxel.verticality; break;
                                }

                                if (valueToCheck < condition.minThreshold || valueToCheck > condition.maxThreshold)
                                {
                                    conditionsMet = false;
                                    break; 
                                }
                            }

                            if (conditionsMet)
                            {
                                voxel.biomeID = biome.biomeID;
                                break; 
                            }
                        }
                    }
        }
    }

    public static void ApplyMaterials(Dictionary<Vector3Int, Chunk> chunks, WorldGenerationProfile profile)
    {
        foreach (var chunk in chunks.Values)
        {
            for (int x = 0; x < chunk.grid.GetLength(0); x++)
                for (int y = 0; y < chunk.grid.GetLength(1); y++)
                    for (int z = 0; z < chunk.grid.GetLength(2); z++)
                    {
                        ref VoxelData voxel = ref chunk.grid[x, y, z];

                        if (Mathf.Abs(voxel.density) > 2.0f) continue;

                        BiomeProfile biome = profile.FindBiome(voxel.biomeID);
                        if (biome == null) continue;

                        foreach (var rule in biome.materialRules)
                        {
                            bool conditionsMet = true;
                            foreach (var condition in rule.conditions)
                            {
                                byte valueToCheck = 0;
                                switch (condition.sourceType)
                                {
                                    case VoxelDataType.Humidity: valueToCheck = voxel.humidity; break;
                                    case VoxelDataType.Temperature: valueToCheck = voxel.temperature; break;
                                    case VoxelDataType.Slope: valueToCheck = voxel.slope; break;
                                    case VoxelDataType.Verticality: valueToCheck = voxel.verticality; break;
                                }

                                if (valueToCheck < condition.minThreshold || valueToCheck > condition.maxThreshold)
                                {
                                    conditionsMet = false;
                                    break;
                                }
                            }

                            if (conditionsMet)
                            {

                            }
                        }
                    }
        }
    }

    

    private static VoxelData GetVoxelAt(int worldX, int worldY, int worldZ, Dictionary<Vector3Int, Chunk> chunks, Vector3Int chunkSize)
    {

        int chunkX = Mathf.FloorToInt((float)worldX / chunkSize.x);
        int chunkY = Mathf.FloorToInt((float)worldY / chunkSize.y);
        int chunkZ = Mathf.FloorToInt((float)worldZ / chunkSize.z);
        Vector3Int chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

        if (chunks.TryGetValue(chunkCoord, out Chunk targetChunk))
        {

            int localX = worldX - (chunkX * chunkSize.x);
            int localY = worldY - (chunkY * chunkSize.y);
            int localZ = worldZ - (chunkZ * chunkSize.z);

            return targetChunk.grid[localX, localY, localZ];
        }

        return VoxelData.Solid;
    }


}

