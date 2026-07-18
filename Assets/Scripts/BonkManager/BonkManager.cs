using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;



public class BonkManager : NetworkBehaviour
{
    [Header("Live Debug Stats")]
    public BonkDebugState debugState = new BonkDebugState();

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

    // Networking State Variables
    [Networked] public NetworkedBonkState CheckpointState { get; set; }
    public NetworkedBonkState CachedNetworkState;

    public float CurrentTotalBonk { get; private set; }
    public bool IsBroken => CurrentTotalBonk >= MaxComposure;
    private bool _wasBrokenLastTick;

    [Header("In-Game BonkBar UI")]
    public bool showBonkBar = false;
    public GameObject bonkCanvas; 
    public float uiBarMaxWidth = 2f; 

    [Header("Bonk Source UI Elements")]
    public LayoutElement kineticUI;
    public LayoutElement hotUI;
    public LayoutElement coldUI;
    public LayoutElement burnUI;

    public override void Spawned()
    {
        base.Spawned();
        Runner.SetIsSimulated(this.Object, true);
        // 1. Inject the Manager downwards into the Servants
        foreach (var bw in bones)
        {
            if (bw.bone != null)
            {
                bw.bone.bonkManager = this;
            }
        }

        ResetStateToTick(Runner.Tick);
    }

    public override void Render()
    {
        base.Render();

        if (Object == null || !Object.IsValid) return;

        // 1. Copy the raw state for the inspector
        debugState.RawNetworkState = CachedNetworkState;

        UpdateBonkUI();
    }

    void UpdateBonkUI()
    {
        // 2. Calculate the individual Bonk Sources
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

        float kineticBonk = CachedNetworkState.KineticBonk;

        // 3. Populate the inspector debug visualizer
        debugState.ElementalFloor = hotBonk + coldBonk + burnBonk;
        debugState.KineticSpike = kineticBonk;
        debugState.TotalBonk = debugState.KineticSpike + debugState.ElementalFloor;
        debugState.IsBroken = debugState.TotalBonk >= MaxComposure;

        // 4. Update the In-Game UI
        if (bonkCanvas != null)
        {
            bonkCanvas.SetActive(showBonkBar);

            if (showBonkBar)
            {
                // By setting minWidth, we forbid Unity from squishing them when they exceed the max bar width!
                if (kineticUI != null)
                {
                    float w = (kineticBonk / MaxComposure) * uiBarMaxWidth;
                    kineticUI.preferredWidth = w;
                    kineticUI.minWidth = w;
                }
                if (hotUI != null)
                {
                    float w = (hotBonk / MaxComposure) * uiBarMaxWidth;
                    hotUI.preferredWidth = w;
                    hotUI.minWidth = w;
                }
                if (coldUI != null)
                {
                    float w = (coldBonk / MaxComposure) * uiBarMaxWidth;
                    coldUI.preferredWidth = w;
                    coldUI.minWidth = w;
                }
                if (burnUI != null)
                {
                    float w = (burnBonk / MaxComposure) * uiBarMaxWidth;
                    burnUI.preferredWidth = w;
                    burnUI.minWidth = w;
                }
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        // ==========================================
        // 1. ROLLBACK / LATE-JOIN DETECTION
        // ==========================================
        if (CachedNetworkState.Tick != Runner.Tick - 1)
        {
            CachedNetworkState = CheckpointState;
        }

        // ==========================================
        // 2. THE CATCH-UP LOOP (Simulation)
        // ==========================================
        int ticksToSimulate = Runner.Tick - CachedNetworkState.Tick;

        if (ticksToSimulate > 0)
        {
            for (int simTick = CachedNetworkState.Tick + 1; simTick <= Runner.Tick; simTick++)
            {
                // Decay the Spike (Kinetic Trauma) naturally over time
                CachedNetworkState.KineticBonk = Mathf.Max(0f, CachedNetworkState.KineticBonk - (kineticBonkDecayRate * Runner.DeltaTime));
            }
            CachedNetworkState.Tick = Runner.Tick;
        }

        // ==========================================
        // 3. PERIODIC CHECKPOINTING
        // ==========================================
        if (Object != null && Object.IsValid)
        {
            int staggerOffset = (int)Object.Id.Raw % 30;
            if (Runner.Tick - CheckpointState.Tick >= 30 + staggerOffset)
            {
                CheckpointState = CachedNetworkState;
            }
        }

        // ==========================================
        // 4. CALCULATE TOTAL BONK (Floor + Spike)
        // ==========================================
        CalculateTotalBonk();

        
    }

    private void CalculateTotalBonk()
    {
        float elementalFloor = 0f;

        // Sum up the persistent state conditions from the bones
        foreach (var bw in bones)
        {
            if (bw.bone == null || bw.bone.physicsObjectProperties == null) continue;

            // Read the rigid networked state of the material
            MaterialState matState = bw.bone.physicsObjectProperties.CachedNetworkState.State;

            // Evaluate the elemental conditions (0 to 1) multiplied by max scaling and bone weight
            float hotStress = matState.Heated * maxHotBonkPerBone * bw.weight;
            float burnStress = matState.Burning * maxHotBonkPerBone * bw.weight;
            float coldStress = matState.Frozen * maxColdBonkPerBone * bw.weight;

            elementalFloor += hotStress + coldStress + burnStress;
        }

        // Total Composure = The Spikes + The Floor
        CurrentTotalBonk = CachedNetworkState.KineticBonk + elementalFloor;

        // Temporary Break Logic Evaluation (For debugging/testing)
        bool isCurrentlyBroken = IsBroken;
        if (isCurrentlyBroken && !_wasBrokenLastTick)
        {
            Debug.Log($"[BonkManager] {gameObject.name} COMPOSURE BROKEN! (Bonk: {CurrentTotalBonk:F1} / {MaxComposure})");
        }
        _wasBrokenLastTick = isCurrentlyBroken;
    }

    #region Trauma Ingestion
    public void ReportCollision(PhysicsObject hitBone, Collision collision, NetworkObject instigator)
    {
        if (hitBone == null || hitBone.physicsObjectProperties == null) return;

        PhysicsObject otherPO = collision.gameObject.GetComponent<PhysicsObject>();
        PhysicsObjectProperties otherProps = otherPO != null ? otherPO.physicsObjectProperties : null;

        ProcessKineticBonk(hitBone, collision.impulse.magnitude, otherProps);
    }

    public void ReportImpulse(PhysicsObject hitBone, float impulseMagnitude, PhysicsObjectProperties otherProperties, NetworkObject instigator, Vector3 contactPoint)
    {
        if (hitBone == null || hitBone.physicsObjectProperties == null) return;
        ProcessKineticBonk(hitBone, impulseMagnitude, otherProperties);
    }

    private void ProcessKineticBonk(PhysicsObject hitBone, float impulse, PhysicsObjectProperties otherProps)
    {
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
        CachedNetworkState.KineticBonk += (rawBonk * weight);
        ForceCheckpoint();
    }

    public void ForceCheckpoint()
    {
        CheckpointState = CachedNetworkState;
    }
    #endregion

    private void ResetStateToTick(int currentTick)
    {
        CheckpointState = new NetworkedBonkState
        {
            Tick = currentTick,
            KineticBonk = 0f
        };
        CachedNetworkState = CheckpointState;
        _wasBrokenLastTick = false;
    }

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
    public bool IsBroken;

    // We can even expose the raw struct copy if you want to see the exact Tick!
    public NetworkedBonkState RawNetworkState;
}