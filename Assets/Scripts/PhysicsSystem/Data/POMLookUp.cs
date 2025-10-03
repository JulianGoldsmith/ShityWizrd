using System;
using System.Collections.Generic;
using UnityEngine;

public class POMLookUp : MonoBehaviour
{
    // A class to allow looking up of materials based on enum values.
    // This should make networking materials easier, since
    // only the name (enum) needs to be communicated.
    // Add new materials to the array
    // At runtime, this is cached in a dictionary for fast lookup.

    [SerializeField] PhysicsObjectMaterial fallback_material;
    [SerializeField] PhysicsObjectMaterial[] all_materials;
    private static Dictionary<PHYSICS_OBJECT_MATERIAL, PhysicsObjectMaterial> dict = null;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        // Initialise the dictionary
        if (all_materials == null)
        {
            Debug.LogError("No PhysicsObjectMaterials defined for lookup");
            return;
        }
        Transfer();
        Verify();
    }
    void Transfer()
    {
        // Transfer array into dictionary and clear.
        dict = new Dictionary<PHYSICS_OBJECT_MATERIAL, PhysicsObjectMaterial>();
        
        dict.Add(PHYSICS_OBJECT_MATERIAL.NULL, fallback_material);

        for (int i = 0; i < all_materials.Length; i++)
        {
            if (all_materials[i] == null)
                continue;
            dict.Add(all_materials[i].label, all_materials[i]);
        }
        // Clear the array to open up data.
        all_materials = null;
    }

    bool Verify()
    {
        // Check we have all materials accounted for.
        bool verified = true;

        PHYSICS_OBJECT_MATERIAL[] values = (PHYSICS_OBJECT_MATERIAL[])Enum.GetValues(typeof(PHYSICS_OBJECT_MATERIAL));
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == PHYSICS_OBJECT_MATERIAL.NULL || values[i] == PHYSICS_OBJECT_MATERIAL.MAX_N)
                continue;
            if (!dict.ContainsKey(values[i]))
            {
                Debug.LogError($"PhysicsObjectMaterial lookup does not contain {values[i]}");
                verified = false;
            }
        }
        return verified;
    }

    public static PhysicsObjectMaterial Get(PHYSICS_OBJECT_MATERIAL label)
    {
        PhysicsObjectMaterial pom;
        if (dict.TryGetValue(label, out pom))
            return pom;
        return dict[PHYSICS_OBJECT_MATERIAL.NULL];
    }
}
