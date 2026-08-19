using Fusion;
using System.Collections.Generic;
using UnityEngine;

// The struct that gets saved periodically over the network to prevent endless catch-up loops
public struct NetworkedMaterialState : INetworkStruct
{
    public int Tick;
    public MaterialState State;
}

public class PhysicsObjectProperties : NetworkBehaviour, IBufferableComponent
{
    private BufferedObject _bufferedObject;
    #region Base Networked Properties (Promotable)
    // By using { get; set; }, Fusion networks these automatically. 
    // Your updated SpellNode reflection will find them via GetProperties()!

    [MaterialID] 
    [Promotable("Material", DataTypeTag.Material)]
    [Networked, OnChangedRender(nameof(OnMaterialChanged))]
    public ushort Material_label { get; set; }

    [MaterialID]
    public ushort innateMaterial; //this is used so we can have custom materials return to their default' albedo/ color etc... 

    [Promotable("Size", DataTypeTag.Radius)]
    [Networked] public float Size { get; set; } = 1f;

    public Vector3 InitialEditorScale { get; private set; }
    private XPBDPosAndRotSolver _ragdollScaleProvider;
    public float RagdollScaleMultiplier => _ragdollScaleProvider != null ? _ragdollScaleProvider.CurrentScale : 1f;
    public float EffectiveSize => Size * RagdollScaleMultiplier;

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
        _ragdollScaleProvider = GetComponentInParent<XPBDPosAndRotSolver>();
    }

    public override void Spawned()
    {
        base.Spawned();
        if (_bufferedObject == null && TryGetComponent(out BufferedObject bufferedObject)) BindBufferedObject(bufferedObject);

        if (HasStateAuthority && CheckpointState.Tick == 0)
            ResetStateToTick(Runner.Tick);
        else
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
        if (_bufferedObject != null && !_bufferedObject.IsAwake) return;
        /*debugState.Clear();
        debugState.Add($"Temp  = {CachedNetworkState.State.Temperature}");
        debugState.Add($"Wetness  = {CachedNetworkState.State.Wetness}");
        //debugState.Add($"Charge  = {CachedNetworkState.State.Charge}");
        debugState.Add($"Scale  = {CachedNetworkState.State.ScaleMultiplier}");*/


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

        if (ticksToSimulate > 0)
        {
            for (int simTick = CachedNetworkState.Tick + 1; simTick <= runner.Tick; simTick++)
            {
                // Create strict previous and next buffers
                MaterialState prevState = CachedNetworkState.State;
                MaterialState nextState = prevState; // Start with a copy of previous

                if (effectManager != null)
                {
                    currentMaterial.ResolveTick(simTick, in prevState, ref nextState, effectManager.ActiveEffects, target, memory);
                }
                else
                {
                    currentMaterial.ResolveTick(simTick, in prevState, ref nextState, default, target, memory);
                }

                // Lock in the new state
                CachedNetworkState.State = nextState;
            }
            CachedNetworkState.Tick = runner.Tick;
        }

        CurrentSimData = currentMaterial.GetSimProperties(CachedNetworkState.State, EffectiveSize, Base_gravity_multiplier);

        // 3. PERIODIC CHECKPOINTING
        if (Object != null && Object.IsValid)
        {
            int staggerOffset = (int)Object.Id.Raw % 30;

            if (runner.Tick - CheckpointState.Tick >= 30 + staggerOffset)
            {
                ForceCheckpoint();
            }
        }
    }

    // NEW: Used by StatusEffectManager to bake history when effects are added/removed
    public void ForceCheckpoint()
    {
        
            //CachedNetworkState.Tick = currentTick;
            CheckpointState = CachedNetworkState;
        
    }

    public void ResetStateToTick(int currentTick)
    {
        MaterialState startingState = new MaterialState();
        startingState.Reset(); // Sets Scale and Density to 1f!

        CheckpointState = new NetworkedMaterialState
        {
            Tick = currentTick,
            State = startingState
        };
        CachedNetworkState = CheckpointState;
    }
    #endregion

    #region Material Lookup
    public PhysicsObjectMaterial physicsobjectmaterial
    {
        get { return MaterialRegistry.GetMaterial(Material_label); }
    }
    #endregion

    #region Legacy Getters (The API Bridge)
    public float density => physicsobjectmaterial != null && physicsobjectmaterial.baseData != null ? physicsobjectmaterial.baseData.baseProperties.Density : 1f;

    // Mass pulls directly from the newly calculated Layer 0 data
    public float mass => CurrentSimData.Mass > 0 ? CurrentSimData.Mass : 0.01f;

    public float hardness => CurrentSimData.Hardness;
    public float elasticity => CurrentSimData.Bounce;
    public float brittleness => CurrentSimData.Brittleness;
    public float stickiness => CurrentSimData.Stickiness;
    public float friction => CurrentSimData.Friction;

    public float moment_of_inertia => density * EffectiveSize;
    #endregion

    public void BindBufferedObject(BufferedObject bufferedObject)
    {
        _bufferedObject = bufferedObject;
    }

    public void OnBufferedWake(int wakeTick, bool isActivationTick)
    {
    }

    public void OnBufferedSleep(int sleepTick)
    {
    }
}
