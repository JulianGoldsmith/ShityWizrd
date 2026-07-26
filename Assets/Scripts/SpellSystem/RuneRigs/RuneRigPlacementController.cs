using Fusion;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class RuneRigPlacementController : MonoBehaviour
{
    [Header("References")]
    public NetworkedInventoryManager Inventory;
    public Material GhostMaterial;

    public NetworkInteractionTarget CurrentAttachmentTarget { get; private set; }

    private NetworkObject _playerObject;
    private RuneRigObject _ghostSourceRig;
    private GameObject _ghostObject;
    private int _ghostSourceHash = int.MinValue;
    private int _lastRefreshFrame = -1;

    private void Awake() {
        _playerObject = GetComponent<NetworkObject>();

        if (Inventory == null)
            Inventory = GetComponent<NetworkedInventoryManager>();
    }

    private void LateUpdate() {
        RefreshPlacement();
    }

    private void OnDisable() {
        CurrentAttachmentTarget = default;
        DestroyGhost();
    }

    public bool TryGetAttachmentTarget(out NetworkInteractionTarget target) {
        RefreshPlacement();
        target = CurrentAttachmentTarget;
        return target.IsValid && target.Type == InteractionTargetType.RuneBay;
    }

    private void RefreshPlacement() {
        if (_lastRefreshFrame == Time.frameCount)
            return;

        _lastRefreshFrame = Time.frameCount;
        CurrentAttachmentTarget = default;

        if (_playerObject == null || !_playerObject.IsValid || !_playerObject.HasInputAuthority || Inventory == null || Inventory.currentItemInHand == null) {
            HideGhost();
            return;
        }

        if (!Inventory.currentItemInHand.TryGetComponent(out RuneRigObject sourceRig)) {
            HideGhost();
            return;
        }

        if (!sourceRig.TryFindClosestAttachment(out NetworkId targetRigId, out byte targetNodeIndex, out byte targetBayIndex)) {
            HideGhost();
            return;
        }

        CurrentAttachmentTarget = NetworkInteractionTarget.CreateRuneBay(targetRigId,targetNodeIndex,targetBayIndex);

        if (GhostMaterial == null || sourceRig.GeneratedVisualRoot == null || !sourceRig.Runner.TryFindObject(targetRigId,out NetworkObject targetObject) || !targetObject.TryGetComponent(out RuneRigObject targetRig) || targetRig.VisualContainer == null) {
            HideGhost();
            return;
        }

        RuneObject sourceRoot = sourceRig.GetRuneObject(0);
        RuneObject targetRune = targetRig.GetRuneObject(targetNodeIndex);
        RuneBay targetBay = targetRune != null ? targetRune.GetBay(targetBayIndex) : null;

        if (sourceRoot == null || sourceRoot.RootConnectionTransform == null || targetBay == null || targetBay.BayTransform == null) {
            HideGhost();
            return;
        }

        EnsureGhost(sourceRig);

        if (_ghostObject == null)
            return;

        Vector3 sourceConnectionPosition = sourceRig.transform.InverseTransformPoint(sourceRoot.RootConnectionTransform.position);
        Quaternion sourceConnectionRotation = Quaternion.Inverse(sourceRig.transform.rotation) * sourceRoot.RootConnectionTransform.rotation;
        Vector3 targetConnectionPosition = targetRig.transform.InverseTransformPoint(targetBay.BayTransform.position);
        Quaternion targetConnectionRotation = Quaternion.Inverse(targetRig.transform.rotation) * targetBay.BayTransform.rotation;

        Quaternion ghostRotation = targetConnectionRotation * Quaternion.Inverse(sourceConnectionRotation);
        Vector3 ghostPosition = targetConnectionPosition - ghostRotation * sourceConnectionPosition;

        if (_ghostObject.transform.parent != targetRig.VisualContainer)
            _ghostObject.transform.SetParent(targetRig.VisualContainer,false);

        _ghostObject.transform.localPosition = ghostPosition;
        _ghostObject.transform.localRotation = ghostRotation;
        _ghostObject.transform.localScale = Vector3.one;
        _ghostObject.SetActive(true);
    }

    private void EnsureGhost(RuneRigObject sourceRig) {
        if (_ghostObject != null && _ghostSourceRig == sourceRig && _ghostSourceHash == sourceRig.RigDataHash)
            return;

        DestroyGhost();

        _ghostSourceRig = sourceRig;
        _ghostSourceHash = sourceRig.RigDataHash;
        _ghostObject = Instantiate(sourceRig.GeneratedVisualRoot.gameObject);
        _ghostObject.name = $"RuneRigPlacementGhost_{sourceRig.name}";
        _ghostObject.hideFlags = HideFlags.DontSave;

        foreach (Transform child in _ghostObject.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = 2;

        foreach (Collider ghostCollider in _ghostObject.GetComponentsInChildren<Collider>(true))
            ghostCollider.enabled = false;

        foreach (Rigidbody ghostBody in _ghostObject.GetComponentsInChildren<Rigidbody>(true)) {
            ghostBody.detectCollisions = false;
            ghostBody.isKinematic = true;
        }

        foreach (MonoBehaviour behaviour in _ghostObject.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;

        foreach (ParticleSystem particles in _ghostObject.GetComponentsInChildren<ParticleSystem>(true))
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);

        foreach (Renderer ghostRenderer in _ghostObject.GetComponentsInChildren<Renderer>(true)) {
            Material[] materials = new Material[ghostRenderer.sharedMaterials.Length];

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                materials[materialIndex] = GhostMaterial;

            ghostRenderer.sharedMaterials = materials;
            ghostRenderer.shadowCastingMode = ShadowCastingMode.Off;
            ghostRenderer.receiveShadows = false;
        }
    }

    private void HideGhost() {
        if (_ghostObject != null)
            _ghostObject.SetActive(false);
    }

    private void DestroyGhost() {
        if (_ghostObject != null) {
            _ghostObject.SetActive(false);
            Destroy(_ghostObject);
        }

        _ghostObject = null;
        _ghostSourceRig = null;
        _ghostSourceHash = int.MinValue;
    }
}
