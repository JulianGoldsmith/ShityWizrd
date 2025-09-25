using UnityEngine;
using System.Collections.Generic;

//not used currently
[CreateAssetMenu(fileName = "New Biome Profile", menuName = "TerrainGen/Biome Profile")]
public class BiomeProfile : ScriptableObject
{
    public byte biomeID;

    [Header("Biome Selection Rules")]
    public List<BiomeCondition> conditions;

    [Header("Material Application Rules")]
    public byte baseMaterialID;
    public List<MaterialRule> materialRules;
}

[System.Serializable]
public class BiomeCondition
{
    public VoxelDataType sourceType;
    [Range(0, 255)]
    public byte minThreshold = 0;
    [Range(0, 255)]
    public byte maxThreshold = 255;
}

[System.Serializable]
public class MaterialRule
{
    public string ruleName; 
    public byte materialID;
    public List<BiomeCondition> conditions; 
}
