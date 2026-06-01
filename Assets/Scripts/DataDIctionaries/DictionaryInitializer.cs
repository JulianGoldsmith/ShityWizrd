using UnityEngine;

[DefaultExecutionOrder(-500)]
public class DictionaryInitializer : MonoBehaviour
{
    [Header("Master Data Assets")]
    public MasterNodeDictionary nodeDictionary;
    // public MasterEffectDictionary effectDictionary; // We will migrate this later!

    private void Awake()
    {
        if (nodeDictionary != null)
        {
            NodeRegistry.Initialize(nodeDictionary);
        }
        else
        {
            Debug.LogError("[DictionaryInitializer] Missing MasterNodeDictionary asset!");
        }

        // (We will add the Effect/Item registries here in the future to keep boot order perfect)
    }
}