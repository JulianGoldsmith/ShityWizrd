using UnityEngine;

[DefaultExecutionOrder(-500)]
public class DictionaryInitializer : MonoBehaviour
{
    [Header("Master Data Assets")]
    public MasterNodeDictionary nodeDictionary;
    public MasterStatusDictionary statusDictionary; 
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
    }
}