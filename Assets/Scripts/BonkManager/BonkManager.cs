using Fusion;
using System.Collections.Generic;
using UnityEngine;



public class BonkManager : NetworkBehaviour
{
    [Header("Live Debug Stats")]
    public BonkDebugState debugState = new BonkDebugState();

    private int _bonkDebugCallCount;

    public bool debugProcessBonk = false;

    public bool debugCollisionStay = false;

    [Header("Collision Stay Bonk Filtering")]

    [Tooltip("Minimum solver impulse required before OnCollisionStay can contribute Bonk.")]
    [Min(0f)]
    public float minimumStayImpulse = 0.05f;

    [Tooltip("Minimum relative speed into or away from the contact normal before OnCollisionStay can contribute Bonk.")]
    [Min(0f)]
    public float minimumStayNormalSpeed = 0.5f;

    [Header("Composure Thresholds")]
    public float MaxComposure = 100f;

    [Tooltip("How much Kinetic Bonk decays per second naturally.")]
    public float kineticBonkDecayRate = 2f;

    [Header("Elemental Floor Scaling")]
    [Tooltip("How much Bonk a fully heated/burning bone contributes (scaled by bone weight).")]
    public float maxHotBonkPerBone = 30f;
    [Tooltip("How much Bonk a fully frozen bone contributes (scaled by bone weight).")]
    public float maxColdBonkPerBone = 30f;

    [Header("Skeleton Integration")]
    public List<BoneWeight> bones = new List<BoneWeight>();

    [Networked] public float KineticBonk { get; set; }

    public float CurrentElementalBonk => CalculateElementalBonk();
    public float CurrentTotalBonk => KineticBonk + CurrentElementalBonk;
    public float CurrentBonkNormalized => Mathf.Clamp01(CurrentTotalBonk / Mathf.Max(0.0001f, MaxComposure));
    public float CurrentBonkPercent => CurrentBonkNormalized * 100f;
    public bool IsAtCapacity => CurrentBonkNormalized >= 1f;

    [Header("In-Game BonkBar UI")]
    public bool showBonkBar = false;
    [SerializeField] private BonkCanvas bonkCanvasPrefab;
    [SerializeField] private Vector3 bonkBarOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private float bonkBarScale = 0.001f;
    [SerializeField] private float uiBarMaxWidth = 500f;
    [SerializeField] private Transform followTarget;

    private BonkCanvas spawnedBonkCanvas;

    public float baseSmoothingFactor = 20f;
    public bool scaleByPing = true;
    public float pingScalar = 5f;
    public bool smoothingEnabled = true;

    private float _smoothedKineticBonk;
    private float _smoothedHotBonk;
    private float _smoothedColdBonk;
    private float _smoothedBurnBonk;
    private bool _hasSmoothedBonkState;
    public override void Spawned()
    {
        base.Spawned();
        Runner.SetIsSimulated(this.Object, true);
        _hasSmoothedBonkState = false;
        foreach (var bw in bones)
        {
            if (bw.bone != null)
            {
                bw.bone.bonkManager = this;
            }
        }

        if (HasStateAuthority)
        {
            KineticBonk = 0f;
        }
    }

    public override void Render()
    {
        base.Render();

        if (Object == null || !Object.IsValid) return;

        debugState.RawNetworkState = new NetworkedBonkState
        {
            Tick = Runner.Tick,
            KineticBonk = KineticBonk
        };

        UpdateBonkUI();
    }

    void UpdateBonkUI()
    {
        float hotBonk = 0f;
        float coldBonk = 0f;
        float burnBonk = 0f;

        foreach (var bw in bones)
        {
            if (bw.bone == null || bw.bone.physicsObjectProperties == null) continue;

            MaterialState matState = bw.bone.physicsObjectProperties.CachedNetworkState.State;
            hotBonk += matState.Heated * maxHotBonkPerBone * bw.weight;
            burnBonk += matState.Burning * maxHotBonkPerBone * bw.weight;
            coldBonk += matState.Frozen * maxColdBonkPerBone * bw.weight;
        }

        float kineticBonk = KineticBonk;

        if (!_hasSmoothedBonkState)
        {
            _smoothedKineticBonk = kineticBonk;
            _smoothedHotBonk = hotBonk;
            _smoothedColdBonk = coldBonk;
            _smoothedBurnBonk = burnBonk;

            _hasSmoothedBonkState = true;
        }
        else
        {
            _smoothedKineticBonk = SmoothBonkValue(_smoothedKineticBonk, kineticBonk);
            _smoothedHotBonk = SmoothBonkValue(_smoothedHotBonk, hotBonk);
            _smoothedColdBonk = SmoothBonkValue(_smoothedColdBonk, coldBonk);
            _smoothedBurnBonk = SmoothBonkValue(_smoothedBurnBonk, burnBonk);
        }

        // 3. Populate the inspector debug visualizer
        debugState.ElementalFloor = hotBonk + coldBonk + burnBonk;
        debugState.KineticSpike = kineticBonk;
        debugState.TotalBonk = debugState.KineticSpike + debugState.ElementalFloor;
        debugState.IsAtCapacity = debugState.TotalBonk >= MaxComposure;

        Transform target = followTarget != null ? followTarget : transform;

        if (showBonkBar && spawnedBonkCanvas == null && bonkCanvasPrefab != null)
        {
            spawnedBonkCanvas = Instantiate(bonkCanvasPrefab);
        }

        if (spawnedBonkCanvas == null) return;

        spawnedBonkCanvas.gameObject.SetActive(showBonkBar);
        if (!showBonkBar) return;

        spawnedBonkCanvas.transform.position = target.position + bonkBarOffset;
        spawnedBonkCanvas.transform.localScale = Vector3.one * bonkBarScale;

        spawnedBonkCanvas.SetBonkValues(_smoothedKineticBonk,_smoothedHotBonk,_smoothedColdBonk,_smoothedBurnBonk,MaxComposure,uiBarMaxWidth);
    }

    public override void FixedUpdateNetwork()
    {
        KineticBonk = Mathf.Max(0f, KineticBonk - (kineticBonkDecayRate * Runner.DeltaTime));
    }

    private float CalculateElementalBonk()
    {
        float elementalBonk = 0f;

        foreach (BoneWeight bw in bones)
        {
            if (bw.bone == null || bw.bone.physicsObjectProperties == null) continue;

            MaterialState state = bw.bone.physicsObjectProperties.CachedNetworkState.State;
            float hotBonk = state.Heated * maxHotBonkPerBone * bw.weight;
            float burningBonk = state.Burning * maxHotBonkPerBone * bw.weight;
            float coldBonk = state.Frozen * maxColdBonkPerBone * bw.weight;

            elementalBonk += hotBonk + burningBonk + coldBonk;
        }

        return elementalBonk;
    }

    #region Trauma Ingestion


    public void ReportCollisionStay(PhysicsObject hitBone,Collision collision,NetworkObject instigator)
    {
        if (hitBone == null || hitBone.physicsObjectProperties == null)
            return;

        if (Runner == null || Object == null || !Object.IsValid)
            return;

        PhysicsObject otherPO =
            collision.gameObject.GetComponent<PhysicsObject>();

        // Preserve your existing self-collision filtering.
        if (otherPO != null && otherPO.bonkManager == this)
            return;

        float collisionImpulse = collision.impulse.magnitude;

        // Reject tiny solver impulses.
        if (collisionImpulse < minimumStayImpulse)
            return;

        float normalImpactSpeed = 0f;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);

            float contactNormalSpeed = Mathf.Abs(
                Vector3.Dot(
                    collision.relativeVelocity,
                    contact.normal
                )
            );

            normalImpactSpeed = Mathf.Max(
                normalImpactSpeed,
                contactNormalSpeed
            );
        }

        /*
         * Resting contacts can still have a support impulse because the
         * solver counteracts gravity every tick. Their normal relative
         * speed should remain very small.
         *
         * Sliding contacts can have high tangential velocity, but their
         * velocity along the contact normal should also remain small.
         */
        if (normalImpactSpeed < minimumStayNormalSpeed)
            return;

        if (debugCollisionStay)
        {
            string otherObjectId =
                otherPO != null &&
                otherPO.Object != null &&
                otherPO.Object.IsValid
                    ? otherPO.Object.Id.ToString()
                    : "None";

            Debug.Log(
                $"[BONK STAY ACCEPTED] " +
                $"Peer={(Runner.IsServer ? "HOST" : "CLIENT")} " +
                $"Frame={Time.frameCount} " +
                $"Tick={Runner.Tick} " +
                $"Forward={Runner.IsForward} " +
                $"Resimulation={Runner.IsResimulation} " +
                $"Target={gameObject.name} ({Object.Id}) " +
                $"Other={collision.gameObject.name} ({otherObjectId}) " +
                $"Contacts={collision.contactCount} " +
                $"Impulse={collisionImpulse:F4} " +
                $"NormalImpactSpeed={normalImpactSpeed:F4} " +
                $"RelativeVelocity={collision.relativeVelocity.magnitude:F4}",
                this
            );
        }

        ReportCollision(
            hitBone,
            collision,
            instigator
        );
    }

    public void ReportCollision(PhysicsObject hitBone, Collision collision, NetworkObject instigator)
    {
        if (hitBone == null || hitBone.physicsObjectProperties == null) return;
        if (Runner == null || Object == null || !Object.IsValid) return;



        _bonkDebugCallCount++;

        PhysicsObject otherPO = collision.gameObject.GetComponent<PhysicsObject>();

        string targetObjectId = Object.Id.ToString();
        string otherObjectId =
            otherPO != null && otherPO.Object != null && otherPO.Object.IsValid
                ? otherPO.Object.Id.ToString()
                : "None";

        Vector3 contactPoint =
            collision.contactCount > 0
                ? collision.GetContact(0).point
                : Vector3.zero;

        Vector3 otherPosition =
            otherPO != null && otherPO.rb != null
                ? otherPO.rb.position
                : collision.transform.position;

        Vector3 otherVelocity =
            otherPO != null && otherPO.rb != null
                ? otherPO.rb.linearVelocity
                : Vector3.zero;

        if (otherPO != null && otherPO.bonkManager == this)
        {
            if (debugProcessBonk)
            {
                Debug.Log(
                    $"[BONK FILTERED #{_bonkDebugCallCount}] " +
                    $"Peer={(Runner.IsServer ? "HOST" : "CLIENT")} " +
                    $"Frame={Time.frameCount} " +
                    $"Tick={Runner.Tick} " +
                    $"Forward={Runner.IsForward} " +
                    $"Resimulation={Runner.IsResimulation} " +
                    $"Target={gameObject.name} ({targetObjectId}) " +
                    $"Other={collision.gameObject.name} ({otherObjectId}) " +
                    $"Reason=SelfCollision",
                    this
                );
            }

            return;
        }

        PhysicsObjectProperties otherProps =
            otherPO != null ? otherPO.physicsObjectProperties : null;

        float kineticBonkBefore = KineticBonk;
        float collisionImpulse = collision.impulse.magnitude;
        float relativeVelocity = collision.relativeVelocity.magnitude;

        if (collisionImpulse <= 0.01f)
        {
            if (debugProcessBonk)
            {
                Debug.Log(
                $"[BONK IGNORED] " +
                $"Tick={Runner.Tick} " +
                $"Forward={Runner.IsForward} " +
                $"Resimulation={Runner.IsResimulation} " +
                $"Impulse={collisionImpulse:F8} " +
                $"Other={collision.gameObject.name} ({otherObjectId})",
                this);
            }

            return;
        }

        ProcessKineticBonk(hitBone, collisionImpulse, otherProps);
        if (debugProcessBonk)
        {
            Debug.Log(
            $"[BONK APPLIED #{_bonkDebugCallCount}] " +
            $"Peer={(Runner.IsServer ? "HOST" : "CLIENT")} " +
            $"Frame={Time.frameCount} " +
            $"Tick={Runner.Tick} " +
            $"Forward={Runner.IsForward} " +
            $"Resimulation={Runner.IsResimulation} " +
            $"StateAuthority={HasStateAuthority} " +
            $"InputAuthority={HasInputAuthority} " +
            $"Target={gameObject.name} ({targetObjectId}) " +
            $"Other={collision.gameObject.name} ({otherObjectId}) " +
            $"Contacts={collision.contactCount} " +
            $"Impulse={collisionImpulse:F4} " +
            $"RelativeVelocity={relativeVelocity:F4} " +
            $"ContactPoint={contactPoint} " +
            $"OtherPosition={otherPosition} " +
            $"OtherVelocity={otherVelocity} " +
            $"KineticBefore={kineticBonkBefore:F3} " +
            $"KineticAfter={KineticBonk:F3}",
            this);
        }
    }

    public void ReportImpulse(PhysicsObject hitBone, float impulseMagnitude, PhysicsObjectProperties otherProperties, NetworkObject instigator, Vector3 contactPoint)
    {
        if (hitBone == null || hitBone.physicsObjectProperties == null) return;
        ProcessKineticBonk(hitBone, impulseMagnitude, otherProperties);
    }

    private void ProcessKineticBonk(PhysicsObject hitBone, float impulse, PhysicsObjectProperties otherProps)
    {
        //if (!HasStateAuthority) return;

        // 1. Let the material do the specific physical math
        PhysicsObjectMaterial mat = hitBone.physicsObjectProperties.physicsobjectmaterial;
        float rawBonk = 0f;

        if (mat != null)
        {
            rawBonk = mat.CalculateKineticBonk(impulse, hitBone.physicsObjectProperties, otherProps);
        }

        // 2. Fetch the corresponding bone weight
        float weight = 1f;
        foreach (var bw in bones)
        {
            if (bw.bone == hitBone)
            {
                weight = bw.weight;
                break;
            }
        }

        // 3. Inject the weighted spike into the active trauma bucket
        KineticBonk += rawBonk * weight;
        //ForceCheckpoint();
    }
    #endregion
    /* public void ForceCheckpoint()
     {
         if (!HasStateAuthority) return;
         CheckpointState = CachedNetworkState;
     }


     private void ResetStateToTick(int currentTick)
     {
         CheckpointState = new NetworkedBonkState
         {
             Tick = currentTick,
             KineticBonk = 0f
         };
         CachedNetworkState = CheckpointState;
         _wasBrokenLastTick = false;
     }*/

    // ==========================================
    // EDITOR AUTOMATION
    // ==========================================
    [ContextMenu("Auto-Find Bones")]
    public void AutoFindBones()
    {
        // Clears the list and grabs all child PhysicsObjects
        bones.Clear();
        PhysicsObject[] foundBones = GetComponentsInChildren<PhysicsObject>();

        foreach (var po in foundBones)
        {
            bones.Add(new BoneWeight
            {
                bone = po,
                weight = 1.0f // Defaults to 1.0 for easy tweaking
            });
        }
        Debug.Log($"[BonkManager] Found and assigned {bones.Count} bones on {gameObject.name}.");
    }

    private float SmoothBonkValue(float currentValue, float targetValue)
    {
        if (!smoothingEnabled) return targetValue;

        float dt = Time.deltaTime;
        if (dt <= 1e-6f) return currentValue;

        float currentSmoothing = baseSmoothingFactor;

        if (scaleByPing && Runner != null && Runner.IsRunning)
        {
            double pingInSeconds = Runner.GetPlayerRtt(Runner.LocalPlayer);

            float pingScale = 1.0f + ((float)pingInSeconds * pingScalar);
            currentSmoothing = baseSmoothingFactor / pingScale;
        }

        return Mathf.Lerp(currentValue, targetValue, dt * currentSmoothing);
    }

    [ContextMenu("ClearBonk")]
    public void ClearBonk()
    {
        // Predict the clear immediately on the requesting client.
        ClearBonkLocal();

        // The host has already changed the authoritative value.
        if (HasStateAuthority) return;

        RPC_RequestClearBonk();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (spawnedBonkCanvas != null) Destroy(spawnedBonkCanvas.gameObject);

        base.Despawned(runner, hasState);
    }

    private void ClearBonkLocal()
    {
        KineticBonk = 0f;
        _smoothedKineticBonk = 0f;
    }

    [Rpc(RpcSources.Proxies | RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestClearBonk(RpcInfo info = default)
    {
        ClearBonkLocal();

        Debug.Log($"[BonkManager] ClearBonk requested by {info.Source}.");
    }
}

public struct NetworkedBonkState : INetworkStruct
{
    public int Tick;
    public float KineticBonk;

    public void Reset(int tick)
    {
        Tick = tick;
        KineticBonk = 0;
    }
    // Note: If we want elemental "spikes" (like an instant lightning blast) later, 
    // we just add float ShockBonk here. The elemental "floor" is calculated dynamically.
}

[System.Serializable]
public struct BoneWeight
{
    public PhysicsObject bone;
    public float weight;
}

[System.Serializable]
public class BonkDebugState
{
    public float TotalBonk;
    public float KineticSpike;
    public float ElementalFloor;
    public bool IsAtCapacity;

    // We can even expose the raw struct copy if you want to see the exact Tick!
    public NetworkedBonkState RawNetworkState;
}
