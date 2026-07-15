using UnityEngine;

public class AttatchedSpellComponent: MonoBehaviour
{
    public SpellCreatedCore parentSpellCore;

    [Header("Visual Configuration")]
    [Tooltip("If true, the core will overwrite the main Material (e.g. turning a blank Egg into Ice). If false, the original Material is kept (e.g. a Wooden Crate).")]
    public bool allowBaseMaterialOverride = true;

    public Renderer[] GetAllRenderers()
    {
        return GetComponentsInChildren<Renderer>(true);
    }

    private void OnCollisionEnter(Collision collision)
    {
        parentSpellCore.OnCollisionEnter(collision);
    }

    private void OnTriggerEnter(Collider other)
    {
        parentSpellCore.OnTriggerEnter(other);
    }
}
