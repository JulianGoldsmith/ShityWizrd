using UnityEngine;

public static class MaterialFields
{
    public static void ClearIfAir(ref VoxelData v, float isoLevel = 0f, float keepBand = 0.5f)
    {
 
        if (v.density <= isoLevel - keepBand)
        {
            v.matId0 = 255;
        }
    }

    public static bool IsEmpty(ref VoxelData v)
    {
        return v.matId0 == 255 && v.matId1 == 255 && v.matId2 == 255 && v.matId3 == 255;
    }

    public static void HardSetMaterial(VoxelData voxel, int matID)
    {
        voxel.matId0 = (byte)matID;
    }


    public static class ChunkMaterialVolumeBuilder
    {
        public static void BuildAndAssignMaterialVolume(Chunk chunk)
        {
            int sizeX = chunk.grid.GetLength(0);
            int sizeY = chunk.grid.GetLength(1);
            int sizeZ = chunk.grid.GetLength(2);


            if (chunk.matIDs3D == null ||
                chunk.matIDs3D.width != sizeX ||
                chunk.matIDs3D.height != sizeY ||
                chunk.matIDs3D.depth != sizeZ)
            {
                chunk.matIDs3D = new Texture3D(sizeX, sizeY, sizeZ, TextureFormat.RGBA32, false);
                chunk.matIDs3D.wrapMode = TextureWrapMode.Clamp;
                chunk.matIDs3D.filterMode = FilterMode.Point; 
            }
            Color32[] idPixels = new Color32[sizeX * sizeY * sizeZ];

   
            if (chunk.density3D == null ||
                chunk.density3D.width != sizeX ||
                chunk.density3D.height != sizeY ||
                chunk.density3D.depth != sizeZ)
            {
                chunk.density3D = new Texture3D(sizeX, sizeY, sizeZ, TextureFormat.RHalf, false);
                chunk.density3D.wrapMode = TextureWrapMode.Clamp;
                chunk.density3D.filterMode = FilterMode.Trilinear; 
            }
            Color[] denPixels = new Color[sizeX * sizeY * sizeZ];



            float cap = Mathf.Max(0.0001f, chunk.sdfCap);
            int idx = 0;

            for (int z = 0; z < sizeZ; z++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int x = 0; x < sizeX; x++)
                    {
                        VoxelData voxel = chunk.grid[x, y, z];

                        byte id0 = voxel.matId0;                
                        idPixels[idx] = new Color32(id0, 0, 0, 0);

                        float dn = Mathf.Clamp01(Mathf.Max(0f, voxel.density) / cap);
                        denPixels[idx] = new Color(dn, 0, 0, 0);
                        idx++;
                        
                    }

                }
            }


            chunk.matIDs3D.SetPixels32(idPixels);
            chunk.matIDs3D.Apply(false, false);

            chunk.density3D.SetPixels(denPixels);
            chunk.density3D.Apply(false, false);


            var renderer = chunk.meshRenderer;
            var propBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propBlock);


            propBlock.SetFloat("_DensityCap", cap);                  
            propBlock.SetTexture("_MatIDs", chunk.matIDs3D);
            propBlock.SetTexture("_Density3D", chunk.density3D);
            propBlock.SetVector("_ChunkSize", new Vector3(sizeX - 1, sizeY - 1, sizeZ - 1));
            propBlock.SetVector("_TexDim", new Vector3(sizeX, sizeY, sizeZ));

            renderer.SetPropertyBlock(propBlock);
        }
    }
}

