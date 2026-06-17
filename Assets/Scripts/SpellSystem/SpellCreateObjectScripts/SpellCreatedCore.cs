using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SpellCreatedCore : NetworkBehaviour
{

    //References to SpellGraph 
    [Networked] public ActiveCastID ActiveCastID { get; set; }
    [Networked] public SpellGraphId BlueprintID { get; set; }
    [Networked] public NetworkBool IsActiveInBuffer { get; set; }
    [Networked] public NetworkString<_64> NodeInstanceGuid { get; set; }
    [Networked] public int NodeArrayIndex { get; set; }


    // current context / active variables
    [Networked] public CoreContext Context { get; set; }
    [Networked] public Vector3 NetworkVelocity { get; set; }



    //networked varibale sketchpad - added to by behaviours and triggers for roll back friendly data
    [Networked, Capacity(16)] public NetworkArray<int> IntMemory { get; }
    [Networked, Capacity(16)] public NetworkArray<float> FloatMemory { get; }
    [Networked, Capacity(8)] public NetworkArray<Vector3> VectorMemory { get; }
    [Networked] public int BoolMemory { get; set; }




    public List<PendingContact> TickContacts = new List<PendingContact>();
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

        if (IsActiveInBuffer && !_isInitialized)
        {
            WakeUp();
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
                if (trigger is RuntimeTriggerBase runtimeTrigger)
                {
                    foreach (var outcome in runtimeTrigger.Outcomes)
                    {
                        // If the outcome is an effect, fire it instantly!
                        if (outcome is IEffect effect)
                        {
                            effect.Execute(this, hitInfos);
                        }
                        // (We will add the logic to spawn downstream Cores here later!)
                    }
                }

                
            }
        }

        TickContacts.Clear();

    }

    public void Initialize(ActiveCastID castId, SpellGraphId blueprintId, string nodeGuid, CoreContext initialContext, int arrayIndex)
    {
        // 1. If we were somehow already active (buffer overlap), clean up the old spell first!
        if (IsActiveInBuffer)
        {
            DeactivateCore();
        }

        NodeArrayIndex = arrayIndex;
        ActiveCastID = castId;
        BlueprintID = blueprintId;
        NodeInstanceGuid = nodeGuid;
        Context = initialContext;
        IsActiveInBuffer = true;
        NetworkVelocity = Vector3.zero;

        ActiveSpell activeSpell = SpellStateManager.instance.GetActiveSpell(ActiveCastID);
        if (activeSpell != null) activeSpell.AddToken();

        // Host / Caster instantly initializes and predicts
        SetupFromRAM();
    }

    private void SetupFromRAM()
    {
        if (SpellStateManager.instance.hydratedSpells.TryGetValue(BlueprintID, out RuntimeSpell runtimeSpell))
        {
            IRuntimeNode myLogic = runtimeSpell.HydratedNodes[NodeArrayIndex];

            if (myLogic is RuntimeObjectCore runtimeCore)
            {
                _myPlan = new CoreExecutionPlan();
                _myPlan.Behaviours = new List<IBehaviour>(runtimeCore.Behaviours);
                _myPlan.Triggers = new List<ITrigger>(runtimeCore.Triggers);

                SpellState myState = SpellStateManager.instance.GetActiveSpell(ActiveCastID)?.State;
                SpellTriggerInfo evaluationInfo = new SpellTriggerInfo(
                    isCast: false, source: gameObject, state: myState, position: Context.SpawnPosition,
                    rotation: transform.rotation, triggerVector: Context.TriggerVector, hitObject: null
                );

                // 1. Initialise the base template defaults (Legacy bridge)
                runtimeCore.Template.InitialisePhysicsObjectOnSpawn(Object, evaluationInfo);

                // 2. DETERMINISTIC MATH (Host and Proxy both run this instantly!)
                float finalSize = runtimeCore.size.GetValue(evaluationInfo);
                float finalLifetime = runtimeCore.lifetime.GetValue(evaluationInfo);
                PHYSICS_OBJECT_MATERIAL finalMat = runtimeCore.material.GetValue(evaluationInfo);

                // 3. Write directly to [Networked] Variables! 
                // (This is perfectly safe and predicts instantly thanks to Runner.SetIsSimulated)
                var pop = GetComponent<PhysicsObjectProperties>();
                if (pop != null)
                {
                    pop.Size = finalSize;
                    pop.Material_label = finalMat;
                    pop.GetComponent<PhysicsObject>().InitialisePhysicsObject();
                }

                var scpo = GetComponent<SpellCreatedPhysicsObject>();
                if (scpo != null) scpo.lifetime_timer = TickTimer.CreateFromSeconds(Runner, finalLifetime);

                foreach (var behaviour in _myPlan.Behaviours) behaviour.InitTick(this);
                foreach (var trigger in _myPlan.Triggers) trigger.InitTick(this);
            }
            _isInitialized = true;
        }
    }

    #region CollisionHandleing
    private void OnCollisionEnter(Collision collision)
    {
        if (!_isInitialized || _myPlan == null) return;
        TickContacts.Add(new PendingContact
        {
            Target = collision.gameObject,
            Point = collision.contacts[0].point,
            Normal = collision.contacts[0].normal
        });
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isInitialized || _myPlan == null) return;

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        Vector3 hitNormal = (transform.position - other.transform.position).normalized;
        if (hitNormal == Vector3.zero) hitNormal = Vector3.up;

        TickContacts.Add(new PendingContact
        {
            Target = other.gameObject,
            Point = hitPoint,
            Normal = hitNormal
        });
    }

    #endregion

    #region Wake / Sleep
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

        // 1. REBUILD THE PROXY STATE (If it doesn't exist)
        if (activeSpell == null)
        {
            if (SpellStateManager.instance.active_spellblueprints.TryGetValue(BlueprintID, out SpellGraph blueprint))
            {
                if (Runner.TryFindObject(ActiveCastID.CasterId, out NetworkObject casterObj))
                {
                    if (casterObj.TryGetComponent<ActiveCastTracker>(out var tracker))
                    {
                        NetworkCastData syncedData = tracker.GetCastData(ActiveCastID);

                        if (syncedData.CastID.IsValid)
                        {
                            SpellState dummyProxyState = new SpellState(Runner, syncedData, blueprint);

                            activeSpell = new ActiveSpell(ActiveCastID, blueprint, dummyProxyState);
                            SpellStateManager.instance.RegisterNewCast(ActiveCastID, activeSpell);

                            Debug.Log($"[Proxy Sync] Rehydrated Cast {ActiveCastID.CastNumber} from Player {casterObj.name}!");
                            if (activeSpell != null) activeSpell.AddToken();
                        }
                        else
                        {
                            Debug.LogWarning($"[Proxy Sync] Tracker had no data for Cast {ActiveCastID.CastNumber}.");
                            return; // Try again next frame!
                        }
                    }
                }
            }
            else
            {
                // The broadcast hasn't arrived yet! Try again next frame.
                return;
            }
        }

        // 2. THE FIX: Pass off entirely to the Universal Setup!
        // We delete the old manual _myPlan extraction block completely.
        if (_myPlan == null)
        {
            SetupFromRAM();
        }
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

    #endregion

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
    public int CurrentNodeIndex { get; set; }

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

public struct PendingContact
{
    public GameObject Target;
    public Vector3 Point;
    public Vector3 Normal;
}