using UnityEngine;

public class AttatchedSpellComponent : MonoBehaviour
{
    public SpellCreatedCore parentSpellCore;

    [Header("Visual Configuration")]
    public Transform VisualRoot;

    [Tooltip("If true, the core will overwrite the main Material. If false, the original Material is kept.")]
    public bool allowBaseMaterialOverride = true;

    [Header("Authored Attachment Points")]
    public Transform[] AttachmentPoints;
    public Transform[] VisualAttachmentPoints;

    public Renderer[] GetAllRenderers()
    {
        if (VisualRoot != null)
            return VisualRoot.GetComponentsInChildren<Renderer>(true);

        return GetComponentsInChildren<Renderer>(true);
    }

    private void OnCollisionEnter(Collision collision)
    {
        parentSpellCore.OnCollisionEnter(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        parentSpellCore.OnCollisionStay(collision);
    }

    private void OnTriggerEnter(Collider other)
    {
        parentSpellCore.OnTriggerEnter(other);
    }

    private void OnTriggerStay(Collider other)
    {
        parentSpellCore.OnTriggerStay(other);
    }
}