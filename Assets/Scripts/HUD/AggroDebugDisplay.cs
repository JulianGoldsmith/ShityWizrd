using UnityEngine;
using Fusion;
using TMPro; 

public class AggroDebugDisplay : NetworkBehaviour
{
    [Header("Components")]
    [Tooltip("The TextMeshPro object that will display the threat value.")]
    [SerializeField] private TextMeshProUGUI threatText;

    [Tooltip("The camera to billboard towards. Will find Main Camera if null.")]
    [SerializeField] private Camera mainCamera;
    private Transform core;
    public float height = 1.5f;

    [Networked, OnChangedRender(nameof(OnThreatChanged))] public float CurrentDebugThreat { get; set; }

    public override void Spawned()
    {
        if (threatText == null)
        {
            Debug.LogWarning("AggroDebugDisplay: No 'threatText' assigned.", this);
            enabled = false;
            return;
        }

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (TryGetComponent<HybridCharacterController>(out HybridCharacterController cc))
            core = cc.smoothedNetworkedRenderRoot.transform;
        else if (TryGetComponent<NPCActiveRagdollController>(out NPCActiveRagdollController arc))
            core = arc.smoothedNetworkRoot.transform;
        else
            core = transform;

        threatText.enabled = false;
    }

    private void OnThreatChanged()
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (CurrentDebugThreat > 0.1f)
        {
            threatText.enabled = true;
            threatText.text = CurrentDebugThreat.ToString("F0");
        }
        else
        {
            threatText.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (threatText.enabled && mainCamera != null)
        {
            
            threatText.transform.position = core.position + Vector3.up * height;
            threatText.transform.rotation = Quaternion.LookRotation(threatText.transform.position - mainCamera.transform.position);
        }
    }
}