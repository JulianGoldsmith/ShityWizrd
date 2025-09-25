using UnityEngine;
using System.Collections.Generic;

public abstract class GenerationFunctions : ScriptableObject
{
    public string functionName = "GenerationFunctions";
    public abstract VoxelData GenerateVoxelData(VoxelData voxelData, Vector3 pos, GeneratedRoom roomInstance);

    //initiation called once prior to the looping through each voxel process. 
    public virtual GenerationFunctions InitAndClone(GeneratedRoom roomInstance)
    {
        return Instantiate(this);
    }

    public virtual float CalculateInfluenceMultiplier(List<EnvironmentInfluence> influences, VoxelData voxel)
    {
        float finalMultiplier = 1.0f;

        foreach (var influence in influences)
        {
            if (!influence.enabled) continue;

            // 1. Get the raw environmental value (0-255) from the voxel
            byte rawValue = 0;
            switch (influence.sourceType)
            {
                case VoxelDataType.Humidity: rawValue = voxel.humidity; break;
                case VoxelDataType.Temperature: rawValue = voxel.temperature; break;
                case VoxelDataType.Slope: rawValue = voxel.slope; break;
                case VoxelDataType.Verticality: rawValue = voxel.verticality; break;
            }

            // 2. Normalize it to a 0-1 float
            float envValue = rawValue / 255f;


            float singleMultiplier = Mathf.Lerp(1f, envValue, Mathf.Abs(influence.influence));
            if (influence.influence < 0)
            {
                singleMultiplier = 1f - (singleMultiplier - 1f); // Invert the effect for negative influence
            }
            finalMultiplier *= singleMultiplier;
        }

        return finalMultiplier;
    }
}


