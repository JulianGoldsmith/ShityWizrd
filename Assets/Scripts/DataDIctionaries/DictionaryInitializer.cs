using UnityEngine;

[DefaultExecutionOrder(-500)]
public class DictionaryInitializer : MonoBehaviour
{
    [Header("Master Data Assets")]
    public MasterNodeDictionary nodeDictionary;
    public MasterStatusDictionary statusDictionary;
    public MasterMaterialDictionary materialDictionary;
    public MasterVFXDictionary vfxDictionary;
    private void Awake()
    {
        if (nodeDictionary != null)
        {
            NodeRegistry.Initialize(nodeDictionary);
        }
        else Debug.LogError("[DictionaryInitializer] Missing MasterNodeDictionary asset!");

        if (statusDictionary != null)
        {
            StatusEffectRegistry.Initialize(statusDictionary);
        }
        else Debug.LogError("[DictionaryInitializer] Missing MasterStatusDictionary asset!");

        if (materialDictionary != null) MaterialRegistry.Initialize(materialDictionary);
        else Debug.LogError("[DictionaryInitializer] Missing MasterMaterialDictionary asset!");

        if (vfxDictionary != null) VFXRegistry.Initialize(vfxDictionary);
        else Debug.LogError("[DictionaryInitializer] Missing MasterVFXDictionary asset!");
    }
}