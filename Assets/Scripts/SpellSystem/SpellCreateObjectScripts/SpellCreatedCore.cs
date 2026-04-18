using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class SpellCreatedCore : NetworkBehaviour
{

    //References to SpellGraph 
    [Networked] public ActiveCastID ActiveCastID { get; set; }
    [Networked] public SpellGraphId BlueprintID { get; set; }
    [Networked] public NetworkBool IsActiveInBuffer { get; set; }
    [Networked] public NetworkString<_64> NodeInstanceGuid { get; set; }

    // current context / active variables
    [Networked] public CoreContext Context { get; set; }
    [Networked] public Vector3 NetworkVelocity { get; set; }

    //networked varibale sketchpad - added to by behaviours and triggers for roll back friendly data
    [Networked, Capacity(16)] public NetworkArray<int> IntMemory { get; }
    [Networked, Capacity(16)] public NetworkArray<float> FloatMemory { get; }
    [Networked, Capacity(8)] public NetworkArray<Vector3> VectorMemory { get; }

    [Networked] public int BoolMemory { get; set; }


    public Dictionary<int, GameObject> ActiveVisuals { get; private set; } = new Dictionary<int, GameObject>();


    private ChangeDetector _changes;

    private CoreExecutionPlan _myPlan;

    private bool _isInitialized;

    public override void Spawned()
    {
        _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    public override void Render()
    {
        base.Render();

        if (!_isInitialized || _myPlan == null) return;

        foreach (var behaviour in _myPlan.Behaviours) behaviour.TickVFX(this);
        foreach (var trigger in _myPlan.Triggers) trigger.TickVFX(this);
    }

    public override void FixedUpdateNetwork()
    {
        // Proxies monitor this. When the Host sets IsActiveInBuffer = true, the Proxy wakes up.
        foreach (var change in _changes.DetectChanges(this))
        {
            if (change == nameof(IsActiveInBuffer))
            {
                if (IsActiveInBuffer) WakeUp();
                else GoToSleep();
            }
        }

        if (!_isInitialized || _myPlan == null) return;

        CoreContext tempContext = Context;
        tempContext.AliveTime += Runner.DeltaTime;
        Context = tempContext;

        foreach (var behaviour in _myPlan.Behaviours)
        {
            behaviour.Tick(this, Runner.DeltaTime);
        }

        for (int i = _myPlan.Triggers.Count - 1; i >= 0; i--)
        {
            ITrigger trigger = _myPlan.Triggers[i];

            // 1. FIX: Expect a List of hits!
            if (trigger.Tick(this, Runner.DeltaTime, out List<SpellTriggerInfo> hitInfos))
            {
                // 2. Execute every effect, passing the WHOLE LIST so the effect can do group math!
                foreach (IEffect effect in trigger.Plan.EffectsToRun)
                {
                    effect.Execute(this, hitInfos);
                }

                // Did hitting this trigger destroy the core? 
                if (trigger.Plan.DestroysCore)
                {
                    DeactivateCore();
                    break; // Stop evaluating further triggers, we are dead!
                }
            }
        }

    }

    public void Initialize(ActiveCastID castId, SpellGraphId blueprintId, string nodeGuid, CoreExecutionPlan compliedExecutionPlan, CoreContext initialContext)
    {
        // 1. If we were somehow already active (buffer overlap), clean up the old spell first!
        if (IsActiveInBuffer)
        {
            DeactivateCore();
        }


        ActiveCastID = castId;
        BlueprintID = blueprintId;

        NodeInstanceGuid = nodeGuid;

        IsActiveInBuffer = true;
        _myPlan = compliedExecutionPlan;
        Context = initialContext;

        NetworkVelocity = Vector3.zero;
        _isInitialized = true;

        // 3. Claim the Token for the NEW spell!
        ActiveSpell activeSpell = SpellStateManager.instance.GetActiveSpell(ActiveCastID);
        if (activeSpell != null)
        {
            activeSpell.AddToken();
        }

        if (_myPlan != null)
        {
            foreach (var behaviour in _myPlan.Behaviours)
            {
                behaviour.InitTick(this);
            }
            foreach (var trigger in _myPlan.Triggers)
            {
                trigger.InitTick(this);
            }
        }

    }





    // Call this when the fireball hits a wall, runs out of lifetime, or is destroyed
    public void DeactivateCore()
    {
        _isInitialized = false;

        if (!IsActiveInBuffer) return;

        IsActiveInBuffer = false; // Replicates to proxies, triggering GoToSleep!
        GoToSleep();
    }

    private void WakeUp()
    {
        ActiveSpell activeSpell = SpellStateManager.instance.GetActiveSpell(ActiveCastID);

        if (activeSpell == null)
        {
            // We are a proxy! We didn't get the input, so we build it right now.
            if (SpellStateManager.instance.active_spellblueprints.TryGetValue(BlueprintID, out SpellGraph blueprint))
            {
                SpellState dummyProxyState = new SpellState(ActiveCastID, null, null, blueprint, null, null);
                activeSpell = new ActiveSpell(ActiveCastID, blueprint, dummyProxyState);

                SpellStateManager.instance.RegisterNewCast(ActiveCastID, activeSpell);
                Debug.Log($"[Proxy Sync] Lazy-loaded Cast {ActiveCastID.CastNumber} from physical core!");
                if (activeSpell != null) activeSpell.AddToken();
            }
            else
            {
                Debug.LogWarning($"[Proxy Sync] Failed to load. Blueprint {BlueprintID.BlueprintNumber} not found on Proxy!");
            }
        }
        if (_myPlan == null)
        {
            if (SpellStateManager.instance.active_spellblueprints.TryGetValue(BlueprintID, out SpellGraph blueprint))
            {
                SpellNode node = blueprint.entryPointControllerNode.GetNodeInChain(NodeInstanceGuid.ToString());
                if (node is CoreNode coreNode)
                {
                    if (coreNode.CompiledPlan == null) coreNode.Compile();
                    _myPlan = coreNode.CompiledPlan;
                }
            }
        }

        _isInitialized = true;
    }

    private void GoToSleep()
    {
        if (_myPlan != null)
        {
            foreach (var behaviour in _myPlan.Behaviours) behaviour.CleanupVFX(this);
            foreach (var trigger in _myPlan.Triggers) trigger.CleanupVFX(this);
        }

        ActiveVisuals.Clear();

        ActiveSpell activeSpell = SpellStateManager.instance.GetActiveSpell(ActiveCastID);
        if (activeSpell != null) activeSpell.RemoveToken();

        ActiveCastID = default;
        BlueprintID = default;
    }

    #region sketchpad memory
 
    public bool GetBool(int bitIndex)
    {
        return (BoolMemory & (1 << bitIndex)) != 0;
    }

    public void SetBool(int bitIndex, bool value)
    {
        if (value)
            BoolMemory |= (1 << bitIndex); 
        else
            BoolMemory &= ~(1 << bitIndex); 
    }

    public int GetInt(int index) => IntMemory.Get(index);
    public void SetInt(int index, int value) => IntMemory.Set(index, value);

    public float GetFloat(int index) => FloatMemory.Get(index);
    public void SetFloat(int index, float value) => FloatMemory.Set(index, value);

    public Vector3 GetVector(int index) => VectorMemory.Get(index);
    public void SetVector(int index, Vector3 value) => VectorMemory.Set(index, value);

    #endregion
}

public class SpellCompilationContext
{
    private int _nextIntSlot = 0;
    private int _nextFloatSlot = 0;
    private int _nextVectorSlot = 0;

    private int _nextBoolBit = 0;

    public int ClaimIntSlot() => _nextIntSlot++;
    public int ClaimFloatSlot() => _nextFloatSlot++;
    public int ClaimVectorSlot() => _nextVectorSlot++;

    public int ClaimBoolBit()
    {
        if (_nextBoolBit >= 32) Debug.LogError("Too many booleans on this core!");
        return _nextBoolBit++;
    }

    private int _nextVfxId = 0;
    public int ClaimVFXId() => _nextVfxId++;
}