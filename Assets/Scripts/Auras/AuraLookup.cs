using System.Collections.Generic;
using System;
using UnityEngine;

public class AuraLookUp : MonoBehaviour
{
    // Allows looking up of auras by their AURA_ID.
    // Only allows one aura per AURA_ID (must be unique).

    [SerializeField] Aura[] all_auras;
    private static Dictionary<AURA_ID, Aura> dict = null;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        // Initialise the dictionary
        if (all_auras == null)
        {
            Debug.LogError("No Auras defined for lookup");
            return;
        }
        Transfer();
        Verify();
    }
    void Transfer()
    {
        // Transfer array into dictionary and clear.
        dict = new Dictionary<AURA_ID, Aura>();

        for (int i = 0; i < all_auras.Length; i++)
        {
            if (all_auras[i] == null)
                continue;
            dict.Add(all_auras[i].unique_label, all_auras[i]);
        }
        // Clear the array to open up data.
        all_auras = null;
    }

    bool Verify()
    {
        // Check we have all materials accounted for.
        bool verified = true;

        AURA_ID[] values = (AURA_ID[])Enum.GetValues(typeof(AURA_ID));
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == AURA_ID.NULL || values[i] == AURA_ID.MAX_N)
                continue;
            if (!dict.ContainsKey(values[i]))
            {
                Debug.LogError($"Aura lookup does not contain {values[i]}");
                verified = false;
            }
        }
        return verified;
    }

    public static Aura Get(AURA_ID label)
    {
        Aura aura;
        if (dict.TryGetValue(label, out aura))
            return aura;
        return dict[AURA_ID.NULL];
    }
}
