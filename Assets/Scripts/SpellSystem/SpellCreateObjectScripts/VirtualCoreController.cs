using Fusion;
using System.Collections.Generic;
using UnityEngine;


// 1. THE NETWORKED STATE

public struct VirtualCoreState : INetworkStruct
{
    public NetworkBool IsActive;
    public ActiveCastID CastID;
    public SpellGraphId BlueprintID;
    public int NodeArrayIndex;
    public int StartTick;
}

// 2.(The Wrapper)

public class VirtualCoreContext : ISpellExecutionCore
{
    public VirtualCoreController Controller;
    public int SlotIndex;
    public int SpawnTick { get; set; }

    public Dictionary<int, GameObject> ActiveVisuals { get; } = new Dictionary<int, GameObject>();

    public Vector3 Position => Controller.CAC.GetSpellCastPoint();
    public Quaternion Rotation => Quaternion.LookRotation(Controller.CAC.GetSpellCastDir());

    public NetworkRunner Runner => Controller.Runner;
    public PlayerRef InputAuthority => Controller.Object.InputAuthority;
    public GameObject SourceObject => Controller.gameObject;
    public NetworkId CoreNetworkId => default;
    public ActiveCastID ActiveCastID => Controller.ActiveStates[SlotIndex].CastID;

    private CoreContext _context;

    public CoreContext Context
    {
        get => _context;
        set => _context = value;
    }

    public float AliveTime => (Runner.Tick - Controller.ActiveStates[SlotIndex].StartTick) * Runner.DeltaTime;

    public bool TryGetCoreComponent<T>(out T component) where T : class
    {
        component = null;
        return false; 
    }

    public bool GetBool(int bitIndex) => (Controller.BoolMemory.Get(SlotIndex) & (1 << bitIndex)) != 0;
    public void SetBool(int bitIndex, bool value)
    {
        int current = Controller.BoolMemory.Get(SlotIndex);
        if (value) current |= (1 << bitIndex);
        else current &= ~(1 << bitIndex);
        Controller.BoolMemory.Set(SlotIndex, current);
    }

    public int GetInt(int index) => Controller.IntMemory.Get((SlotIndex * VirtualCoreController.INTS_PER_CORE) + index);
    public void SetInt(int index, int value) => Controller.IntMemory.Set((SlotIndex * VirtualCoreController.INTS_PER_CORE) + index, value);

    public float GetFloat(int index) => Controller.FloatMemory.Get((SlotIndex * VirtualCoreController.FLOATS_PER_CORE) + index);
    public void SetFloat(int index, float value) => Controller.FloatMemory.Set((SlotIndex * VirtualCoreController.FLOATS_PER_CORE) + index, value);

    public Vector3 GetVector(int index) => Controller.VectorMemory.Get((SlotIndex * VirtualCoreController.VECTORS_PER_CORE) + index);
    public void SetVector(int index, Vector3 value) => Controller.VectorMemory.Set((SlotIndex * VirtualCoreController.VECTORS_PER_CORE) + index, value);
}


[RequireComponent(typeof(CastActionController))]
public class VirtualCoreController : NetworkBehaviour
{
    public const int MAX_VIRTUAL_CORES = 8;
    public const int INTS_PER_CORE = 16;
    public const int FLOATS_PER_CORE = 16;
    public const int VECTORS_PER_CORE = 8;

    public CastActionController CAC { get; private set; }

    [Networked, Capacity(MAX_VIRTUAL_CORES)] public NetworkArray<VirtualCoreState> ActiveStates { get; }

    [Networked, Capacity(MAX_VIRTUAL_CORES * INTS_PER_CORE)] public NetworkArray<int> IntMemory { get; }      // 8 cores * 16 ints
    [Networked, Capacity(MAX_VIRTUAL_CORES * FLOATS_PER_CORE)] public NetworkArray<float> FloatMemory { get; }  // 8 cores * 16 floats
    [Networked, Capacity(MAX_VIRTUAL_CORES * VECTORS_PER_CORE)] public NetworkArray<Vector3> VectorMemory { get; }// 8 cores * 8 vectors
    [Networked, Capacity(MAX_VIRTUAL_CORES)] public NetworkArray<int> BoolMemory { get; }

    private VirtualCoreContext[] _contexts;

    private VirtualCoreSlot[] _slots;

    public override void Spawned()
    {
        CAC = GetComponent<CastActionController>();

        _slots = new VirtualCoreSlot[MAX_VIRTUAL_CORES];
        for (int i = 0; i < MAX_VIRTUAL_CORES; i++) _slots[i] = new VirtualCoreSlot();

        _contexts = new VirtualCoreContext[MAX_VIRTUAL_CORES];

        for (int i = 0; i < MAX_VIRTUAL_CORES; i++)
        {
            _contexts[i] = new VirtualCoreContext()
            {
                Controller = this,
                SlotIndex = i,
                Context = new CoreContext()
            };
        }
    }

    #region The Lifecycle API
    public void StartVirtualCore(ActiveCastID castId, SpellGraphId blueprintId, int rootNodeIndex, CoreContext initialContext)
    {
        //if (!Object.HasStateAuthority && !Object.HasInputAuthority) return;

        for (int i = 0; i < MAX_VIRTUAL_CORES; i++)
        {
            if (!ActiveStates[i].IsActive)
            {
                VirtualCoreState newState = new VirtualCoreState()
                {
                    IsActive = true,
                    CastID = castId,
                    BlueprintID = blueprintId,
                    NodeArrayIndex = rootNodeIndex,
                    StartTick = Runner.Tick
                };

                ActiveStates.Set(i, newState);
                _contexts[i].Context = initialContext;

                IRuntimeNode rootNode = GetLogicNode(i);
                _slots[i].Logic = rootNode;
                _slots[i].WasActive = true;
                
                if (_slots[i].Logic is ITrigger t) t.InitTick(_contexts[i]);
                else if (_slots[i].Logic is IBehaviour b) b.InitTick(_contexts[i]);

                return;
            }
        }
        Debug.LogWarning($"[Virtual Core] No slots available on {gameObject.name}!");
    }

    public void StopVirtualCore(ActiveCastID castId)
    {
        for (int i = 0; i < MAX_VIRTUAL_CORES; i++)
        {
            if (ActiveStates[i].IsActive && ActiveStates[i].CastID.Equals(castId))
            {
                // 1. Wipe the state
                ActiveStates.Set(i, default);

                // 2. Explicitly scrub memory
                ClearMemory(i);
            }
        }
    }

    private void ClearMemory(int slotIndex)
    {
        for (int j = 0; j < INTS_PER_CORE; j++) IntMemory.Set((slotIndex * INTS_PER_CORE) + j, 0);
        for (int j = 0; j < FLOATS_PER_CORE; j++) FloatMemory.Set((slotIndex * FLOATS_PER_CORE) + j, 0f);
        for (int j = 0; j < VECTORS_PER_CORE; j++) VectorMemory.Set((slotIndex * VECTORS_PER_CORE) + j, Vector3.zero);
        BoolMemory.Set(slotIndex, 0);
    }
    public void StopAllCores()
    {
        for (int i = 0; i < MAX_VIRTUAL_CORES; i++) ActiveStates.Set(i, default);
    }
    #endregion

    #region The Execution Loops
    public override void FixedUpdateNetwork()
    {
        for (int i = 0; i < MAX_VIRTUAL_CORES; i++)
        {
            if (ActiveStates[i].IsActive)
            {
                IRuntimeNode rootNode = GetLogicNode(i);

                if (rootNode is ITrigger trigger)
                {
                    if (trigger.Tick(_contexts[i], Runner.DeltaTime, out List<SpellTriggerInfo> hitInfos))
                    {
                        /*if (Object.HasStateAuthority || (Object.HasInputAuthority && Runner.IsForward))
                        {*/
                            if (trigger is RuntimeTriggerBase baseTrigger)
                            {
                                foreach (var outcome in baseTrigger.Outcomes)
                                {
                                    if (outcome is IEffect effect) effect.Execute(_contexts[i], hitInfos);
                                }
                            }
                        //}
                    }
                }
                else if (rootNode is IBehaviour behaviour)
                {
                    // For things like continuous self-healing or levitation auras
                    behaviour.Tick(_contexts[i], Runner.DeltaTime);
                }
            }
        }
    }

    public override void Render()
    {
        // Proxies perfectly simulate the beam visuals here using local edge-detection!
        for (int i = 0; i < MAX_VIRTUAL_CORES; i++)
        {
            VirtualCoreState state = ActiveStates[i];
            VirtualCoreSlot slot = _slots[i];

            // 1. TRANSITION: Running -> Stopped (The Cleanup)
            if (!state.IsActive && slot.WasActive)
            {
                // We don't need to look up anything, the Logic is cached!
                if (slot.Logic is ITrigger t) t.CleanupVFX(_contexts[i]);
                else if (slot.Logic is IBehaviour b) b.CleanupVFX(_contexts[i]);

                slot.ClearSlot();
            }

            // 2. TRANSITION: Stopped -> Running (The Init)
            else if (state.IsActive && !slot.WasActive)
            {
                slot.Logic = GetLogicNode(i);
                slot.WasActive = true;
            }

            // 3. CONTINUOUS: Running
            if (state.IsActive && slot.Logic != null)
            {
                if (slot.Logic is ITrigger t) t.TickVFX(_contexts[i]);
                else if (slot.Logic is IBehaviour b) b.TickVFX(_contexts[i]);
            }
        }
    }

    private IRuntimeNode GetLogicNode(int slotIndex)
    {
        SpellGraphId blueprintID = ActiveStates[slotIndex].BlueprintID;
        RuntimeSpell runtimeSpell = SpellBlueprintLibrary.Get(blueprintID);

        if (runtimeSpell == null) return null;

        IRuntimeNode node = runtimeSpell.GetNode(ActiveStates[slotIndex].NodeArrayIndex);

        if (node is RuntimeEntryPoint entryPoint)
            return entryPoint.ConnectedLogic;

        return node;
    }
    #endregion
}

public class VirtualCoreSlot
{
    public IRuntimeNode Logic;      
    public bool WasActive;          
    public Dictionary<int, GameObject> ActiveVisuals = new Dictionary<int, GameObject>();

    // Explicit clean method
    public void ClearSlot()
    {
        Logic = null;
        WasActive = false;

        // Clean visuals
        foreach (var visual in ActiveVisuals.Values)
        {
            if (visual != null)
            {
                if (visual.TryGetComponent<SpellVFX>(out var vfx)) vfx.StopAndCleanup();
                else Object.Destroy(visual);
            }
        }
        ActiveVisuals.Clear();
    }
}