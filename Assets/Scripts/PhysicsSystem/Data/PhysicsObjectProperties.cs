using Fusion;
using System.Collections.Generic;
using UnityEngine;

// The struct that gets saved periodically over the network to prevent endless catch-up loops
public struct NetworkedMaterialState : INetworkStruct
{
    public int Tick;
    public MaterialState State;
}

[RequireComponent(typeof(PhysicsObject))]
public class PhysicsObjectProperties : NetworkBehaviour
{
    #region Base Networked Properties (Promotable)
    // By using { get; set; }, Fusion networks these automatically. 
    // Your updated SpellNode reflection will find them via GetProperties()!

    [Promotable("Material", DataTypeTag.Material)]
    [Networked, OnChangedRender(nameof(OnMaterialChanged))]
    public PHYSICS_OBJECT_MATERIAL Material_label { get; set; }

    [Promotable("Size", DataTypeTag.Radius)]
    [Networked] public float Size { get; set; } = 1f;

    public Vector3 InitialEditorScale { get; private set; }

    [Promotable("Base Gravity", DataTypeTag.Generic)]
    [Networked] public float Base_gravity_multiplier { get; set; } = 1f;
    #endregion

    #region State Checkpoints & Caches
    [Networked] public NetworkedMaterialState CheckpointState { get; set; }

    // CHANGED: Unified Cache
    public NetworkedMaterialState CachedNetworkState;
    public SimProperties CurrentSimData;
    #endregion
    [SerializeField]
    public List<string> debugState = new List<string>();

    private void Awake()
    {
        // Grab the largest axis of the object's transform in the scene.
        InitialEditorScale = transform.localScale;
    }

    public override void Spawned()
    {
        base.Spawned();
        MaterialState startingState = new MaterialState();
        startingState.Reset(); // Sets Scale and Density to 1f!

        CheckpointState = new NetworkedMaterialState
        {
            Tick = Runner.Tick,
            State = startingState
        };
        CachedNetworkState = CheckpointState;

        GetComponent<PhysicsObject>().InitialisePhysicsObject();
    }

    public void OnMaterialChanged()
    {
        GetComponent<PhysicsObject>().InitialisePhysicsObject();
    }

    public override void Render()
    {
        base.Render();
        debugState.Clear();
        debugState.Add($"Temp  = {CachedNetworkState.State.Temperature}");
        debugState.Add($"Wetness  = {CachedNetworkState.State.Wetness}");
        debugState.Add($"Charge  = {CachedNetworkState.State.Charge}");
        debugState.Add($"Scale  = {CachedNetworkState.State.ScaleMultiplier}");


    }

    #region The Simulation Engine
    public void CalculateSimState(NetworkRunner runner, PhysicsObject target, NetworkedMemoryAllocator memory, StatusEffectManager effectManager)
    {
        if (physicsobjectmaterial == null) return;

        // 1. ROLLBACK / LATE-JOIN DETECTION
        if (CachedNetworkState.Tick != runner.Tick - 1)
        {
            CachedNetworkState = CheckpointState;
        }

        // 2. THE CATCH-UP LOOP
        int ticksToSimulate = runner.Tick - CachedNetworkState.Tick;

        PhysicsObjectMaterial currentMaterial = physicsobjectmaterial;

        // Replay only if needed
        if (ticksToSimulate > 0)
        {
            for (int simTick = CachedNetworkState.Tick + 1;simTick <= runner.Tick; simTick++)
            {
                if (effectManager != null)
                {
                    currentMaterial.ResolveTick(simTick,ref CachedNetworkState.State,effectManager.ActiveEffects,
                        target,memory);
                }
                else
                {
                    currentMaterial.ResolveTick(simTick,ref CachedNetworkState.State,
                        default,target,memory);
                }
            }

            CachedNetworkState.Tick = runner.Tick;
        }

        CurrentSimData = currentMaterial.GetSimProperties(CachedNetworkState.State,Size,Base_gravity_multiplier);

        // 3. PERIODIC CHECKPOINTING
        if ((runner.Tick - CheckpointState.Tick >= 30))
        {
            ForceCheckpoint();
        }
    }

    // NEW: Used by StatusEffectManager to bake history when effects are added/removed
    public void ForceCheckpoint()
    {
        
            //CachedNetworkState.Tick = currentTick;
            CheckpointState = CachedNetworkState;
        
    }
    #endregion

    #region Material Lookup
    public PhysicsObjectMaterial physicsobjectmaterial
    {
        get { return POMLookUp.Get(Material_label); }
    }
    #endregion

    #region Legacy Getters (The API Bridge)
    // External scripts (like VFX or UI) can read these without breaking, 
    // completely unaware that a rollback simulation is feeding them the numbers!

    public float density => physicsobjectmaterial != null ? physicsobjectmaterial.density :1f;

    // Mass pulls directly from the newly calculated Layer 0 data
    public float mass => CurrentSimData.Mass > 0 ? CurrentSimData.Mass : 0.01f;

    public float hardness => CurrentSimData.Hardness;
    public float elasticity => CurrentSimData.Restitution;
    public float brittleness => CurrentSimData.Brittleness;
    public float stickiness => CurrentSimData.Stickiness;
    public float friction => CurrentSimData.Friction;

    public float moment_of_inertia => density * Size;
    #endregion
}