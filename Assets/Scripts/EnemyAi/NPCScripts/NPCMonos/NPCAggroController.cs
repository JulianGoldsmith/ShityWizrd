using UnityEngine;
using Fusion;
using Unity.Behavior;
using System.Collections.Generic;

public class NPCAggroController : NetworkBehaviour
{
    [Header("Behavior Tree Variables")]
    [SerializeField] private BehaviorGraphAgent agent;
    [SerializeField] private string targetVariableName = "Target";
    [SerializeField] private string chasePositionVariableName = "ChasePosition";
    [SerializeField] private string investigatePositionVariableName = "InvestigatePosition";
    [SerializeField] private string aggroStateVariableName = "AggroState";
    [SerializeField] private string canSeeCurrentTargetVariableName = "CanSeeCurrentTarget";
    [SerializeField] private string generalAggroValueVariableName = "GeneralAggro";

    [SerializeField] private string interestPointThreatVariableName = "InterestPointThreat";

    [SerializeField] private string unattributedThreatVariableName = "UnattributedThreat";
    [SerializeField] private string unattributedThreatDirectionVariableName = "UnattributedThreatDirection";

    [SerializeField] private string currentTargetThreatVariableName = "CurrentTargetThreat";

    [Header("NPC variables")]
    private NPCActiveRagdollController activeRagdollController;
    private Transform core;

    [Header("Tuing")]
    [SerializeField] private float targetStickinessMultiplier = 1.2f;
    [SerializeField] private float aggroActivationThreshold = 1.0f;

    [SerializeField] private float persistentThreatRampUpRate = 20f; // How fast to build focus-threat
    [SerializeField] private float persistentThreatDecayRate = 5f;  // How fast focus decays
    [SerializeField] private float spikeThreatDecayRate = 30f;      // How fast pain decays

    [Header("Bonk detection")]
    [SerializeField] private float bonkToThreatMultiplier = 1.5f;
    [SerializeField] private float lineOfSightMultiplier = 3.0f;

    [Header("Hearing detection")]
    [SerializeField] private float hearingRadius = 25f;
    [SerializeField] private float hearingSensitivity = 1.0f;
    [SerializeField] private float minHearingThreshold = 10f;

    [Header("visoon detection")]
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private float visionRadius = 20f;
    [Range(0, 360)][SerializeField] private float visionAngle = 180f;
    [SerializeField] private float sightThreatPerSecond = 5f;
    [SerializeField] private LayerMask visionBlockers;
    [SerializeField] private Vector3 eyeOffset = new Vector3(0, 1.5f, 0);

    public AggroState CurrentAggroState { get; set; }
    public NetworkObject CurrentTarget { get; set; }
    public InterestPoint CurrentInterestPoint { get; set; }
    private float GeneralAggro { get; set; }
    public float UnattributedThreat { get; set; }
    public Vector3 UnattributedThreatDirection { get; set; }


    private Dictionary<NetworkObject, ThreatInfo> _threatTable = new Dictionary<NetworkObject, ThreatInfo>();
    [SerializeField] private List<ThreatDebugEntry> _inspectorDebugThreatTable = new List<ThreatDebugEntry>();
    
    private List<NetworkObject> _targetsToRemove = new List<NetworkObject>();
    private Vector3 _eyePosition => core.transform.TransformPoint(eyeOffset);

    [SerializeField] private bool showThreatDebug = false;

    private void Awake()
    {
        activeRagdollController = this.GetComponent<NPCActiveRagdollController>();
        core = activeRagdollController.coreRB.transform;
        if (agent == null)
            agent = GetComponent<BehaviorGraphAgent>();
        agent.enabled = false;
        eyeOffset *= activeRagdollController.sizeMult;
    }


    public void TickAggroSensors()
    {
        if (!HasStateAuthority)
            return;

        LookForTargets();
        DecayCurrentTargetsThreat();
        FindHighestThreatTarget();
        DeterminState();
        UpdateBlackboardVariables();

        if (showThreatDebug && Application.isPlaying)
        {
            UpdateInspectorDebug();
        }
    }

    public override void FixedUpdateNetwork()
    {
        TickAggroSensors();
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            if (agent != null)
            {
                agent.enabled = true;
            }
            else
            {
                Debug.LogError("AI Agent is null, cannot enable!");
            }
        }
    }


    //For bonks
    public void ReportBonk(NetworkObject instigator, float bonkAmmount, Vector3? hitPosition)
    {
        if (!HasStateAuthority) return;
        if(instigator!=null)
            Debug.Log($"Recieved bonk report - {instigator.name} ammount - {bonkAmmount}");
        float baseThreat = bonkAmmount * bonkToThreatMultiplier;
        AssessNewThreat(instigator, baseThreat, hitPosition);
    }

    //for general undefined sensations
    public void ReportSensation(NetworkObject instigator, float flatThreatAmount, Vector3 sensationPosition)
    {
        if (!HasStateAuthority) return;

        AssessNewThreat(instigator, flatThreatAmount, sensationPosition);
    }

    //for sound inputs
    public void ReportSound(Vector3 position, float baseVolume)
    {
        if (!HasStateAuthority) return;

        float distance = Vector3.Distance(_eyePosition, position);
        if (distance > hearingRadius)
            return;

        float perceivedThreat = (1f - (distance / hearingRadius)) * baseVolume * hearingSensitivity;

        if (perceivedThreat >= minHearingThreshold)
        {
            AssessNewThreat(null, perceivedThreat, position);
        }
    }

    //for reporting threats without a position
    public void ReportAnonymousSensation(float flatThreatAmount, Vector3 sensationPosition)
    {
        if (!HasStateAuthority) return;

        AssessNewThreat(null, flatThreatAmount, sensationPosition);
    }


 
    private void AssessNewThreat(NetworkObject instigator, float threatAmount, Vector3? sensationPosition)
    {
        if (instigator != null) //if we know who has instigated this threat 
        {
            if (!_threatTable.TryGetValue(instigator, out ThreatInfo info))
            {
                info = new ThreatInfo();
                _threatTable[instigator] = info;
                info.debugDisplay = instigator.GetComponent<AggroDebugDisplay>();
            }

            bool hasLOS = CheckLineOfSight(instigator);
            info.hasLineOfSight = hasLOS; 

            if (hasLOS)
            {
                threatAmount *= lineOfSightMultiplier;

                info.spikeThreat += threatAmount;
                info.spikeThreat += CurrentInterestPoint.Threat;
                info.spikeThreat += UnattributedThreat;

                CurrentInterestPoint = default;
                UnattributedThreat = 0;
                
                UnattributedThreatDirection = Vector3.zero;
                info.lastKnownPosition = GetTargetPosition(instigator);
            }
            else
            {
                UnattributedThreat += threatAmount;
                if (sensationPosition.HasValue)
                    UnattributedThreatDirection = (sensationPosition.Value - _eyePosition).normalized;
               // UpdateInterestPoint(threatAmount, sensationPosition);
            }
        }
        else //otherwise update an interest point where the "sound/ sensation etc.. was felt"
        {
            UpdateInterestPoint(threatAmount, sensationPosition);
        }
    }

    private void UpdateInterestPoint(float newThreat, Vector3? newPosition)
    {
        if(newPosition == null)
        {
            return;
        }
        else if (newThreat > CurrentInterestPoint.Threat)
        {
            CurrentInterestPoint = new InterestPoint { Position = (Vector3)newPosition, Threat = newThreat };
        }
    }

    private bool CheckLineOfSight(NetworkObject target)
    {
        Vector3 direction = (GetTargetPosition(target) - _eyePosition);
        if (Physics.Raycast(_eyePosition, direction.normalized, out RaycastHit hit, direction.magnitude, visionBlockers))
        {
            if (hit.transform.root != target.transform.root)
            {
                return false; 
            }
        }
        return true;
    }


   

    private void LookForTargets()
    {
       // Debug.Log("Looking for targets");
        HashSet<NetworkObject> targetsSeenThisFrame = new HashSet<NetworkObject>();

        Collider[] targetsInRadius = Physics.OverlapSphere(core.transform.position, visionRadius, targetLayer);

        foreach (var targetCollider in targetsInRadius)
        {
            //Debug.Log($"Found targt");
            NetworkObject target = targetCollider.transform.root.GetComponent<NetworkObject>();
            //Debug.Log($"Found targt {target.name}");
            if (target == null) continue;
            Vector3 directionToTarget = (GetTargetPosition(target) - _eyePosition).normalized;
            if (directionToTarget.sqrMagnitude < 0.01f) continue;

            float angle = Vector3.Angle(core.transform.forward, directionToTarget);
            if (angle > visionAngle / 2)
                continue;
            //Debug.Log($"Found targt in vision cone {target.name}");
            if (CheckLineOfSight(target))
            {
                //Debug.Log($"Found targt in line of sight {target.name}");
                targetsSeenThisFrame.Add(target);

                if (!_threatTable.TryGetValue(target, out ThreatInfo info))
                {
                    info = new ThreatInfo();
                    _threatTable[target] = info;
                    info.debugDisplay = target.GetComponent<AggroDebugDisplay>();
                }


                if (UnattributedThreat > 0 && target != CurrentTarget) // this needs to be updated as could target someone else theyve seen isnt the instigator
                {
                    info.spikeThreat += UnattributedThreat;
                    UnattributedThreat = 0;
                    UnattributedThreatDirection = Vector3.zero;
                }


                float distance = Vector3.Distance(_eyePosition, GetTargetPosition(target));
                float targetThreatCap = 100f / (distance + 1f);

                float currentPersistentThreat = info.persistentThreat;
                float newPersistentThreat;

                if (targetThreatCap > currentPersistentThreat)
                {
                    newPersistentThreat = Mathf.MoveTowards(currentPersistentThreat,targetThreatCap,persistentThreatRampUpRate * Runner.DeltaTime);
                }
                else
                {
                    newPersistentThreat = Mathf.MoveTowards(currentPersistentThreat,targetThreatCap, persistentThreatDecayRate * Runner.DeltaTime);
                }

                info.persistentThreat = newPersistentThreat;

                info.hasLineOfSight = true;
                info.lastKnownPosition = GetTargetPosition(target);
            }
        }


        foreach (var pair in _threatTable)
        {
            if (pair.Key == null) 
            {
                _targetsToRemove.Add(pair.Key);
                continue;
            }

            if (!targetsSeenThisFrame.Contains(pair.Key)) //no sight of this threat this frame so decay!!
            {
                pair.Value.hasLineOfSight = false;
                pair.Value.persistentThreat = Mathf.MoveTowards(pair.Value.persistentThreat, 0f, persistentThreatDecayRate * Runner.DeltaTime);
            }
        }

        foreach (var target in _targetsToRemove)
            _threatTable.Remove(target);
        _targetsToRemove.Clear();
    }

    private void DecayCurrentTargetsThreat()
    {
        float ipDecay = spikeThreatDecayRate * Runner.DeltaTime; //maybe we want to use a different decay rate for interest? spike decay for now
        if (CurrentInterestPoint.Threat > 0)
        {
            var ip = CurrentInterestPoint; 
            ip.Threat = Mathf.Max(0, ip.Threat - ipDecay);
            CurrentInterestPoint = ip;
        }

        float frustrationDecay = persistentThreatDecayRate * Runner.DeltaTime; 
        if (UnattributedThreat > 0)
        {
            UnattributedThreat = Mathf.Max(0, UnattributedThreat - frustrationDecay);
            if (UnattributedThreat == 0)
                UnattributedThreatDirection = Vector3.zero;
        }

        float spikeDecay = spikeThreatDecayRate * Runner.DeltaTime;
        foreach (var pair in _threatTable)
        {
            pair.Value.spikeThreat = Mathf.Max(0, pair.Value.spikeThreat - spikeDecay);
        }
    }

    private void FindHighestThreatTarget()
    {
        NetworkObject bestTarget = null;
        float maxThreat = 0;

        foreach (var pair in _threatTable)
        {
            float targetTotalThreat = pair.Value.TotalThreat;
            if (targetTotalThreat > maxThreat)
            {
                maxThreat = targetTotalThreat;
                bestTarget = pair.Key;
            }
        }

        if (bestTarget == null || maxThreat <= 0)
        {
            CurrentTarget = null;
            return;
        }

        if (CurrentTarget == null)
        {
            CurrentTarget = bestTarget;
        }
        else if (bestTarget != CurrentTarget)
        {
            if (_threatTable.TryGetValue(CurrentTarget, out ThreatInfo currentTargetInfo))
            {
                if (maxThreat > currentTargetInfo.TotalThreat * targetStickinessMultiplier)
                {
                    CurrentTarget = bestTarget;
                }
            }
            else
            {
                CurrentTarget = bestTarget;
            }
        }
    }

    private void DeterminState()
    {
        float totalThreat = CurrentInterestPoint.Threat + UnattributedThreat;
        foreach (var pair in _threatTable)
        {
            totalThreat += pair.Value.TotalThreat;
        }
        GeneralAggro = totalThreat;

        if (GeneralAggro > aggroActivationThreshold)
            CurrentAggroState = AggroState.Aggro;
        else
            CurrentAggroState = AggroState.Passive;
    }

    private void UpdateBlackboardVariables()
    {
        if (agent == null) return;

        agent.SetVariableValue(unattributedThreatVariableName, UnattributedThreat);
        agent.SetVariableValue(unattributedThreatDirectionVariableName, UnattributedThreatDirection);

        if (CurrentTarget != null)
            agent.SetVariableValue(targetVariableName, CurrentTarget.gameObject);

        agent.SetVariableValue(aggroStateVariableName, CurrentAggroState);
        agent.SetVariableValue(investigatePositionVariableName, CurrentInterestPoint.Position);

        agent.SetVariableValue(interestPointThreatVariableName, CurrentInterestPoint.Threat);

        float currentTargetThreat = 0f;
        bool canSeeTarget = false;

        if (CurrentTarget != null && _threatTable.TryGetValue(CurrentTarget, out ThreatInfo info))
        {
            agent.SetVariableValue(chasePositionVariableName, info.lastKnownPosition);
            canSeeTarget = info.hasLineOfSight;
            currentTargetThreat = info.TotalThreat;
        }

        agent.SetVariableValue(canSeeCurrentTargetVariableName, canSeeTarget);
        agent.SetVariableValue(currentTargetThreatVariableName, currentTargetThreat);
    }

    private void UpdateInspectorDebug()
    {
        _inspectorDebugThreatTable.Clear();

        foreach (var pair in _threatTable)
        {
            if (pair.Key == null) continue;

            _inspectorDebugThreatTable.Add(new ThreatDebugEntry
            {
                target = pair.Key,
                currentThreat = pair.Value.TotalThreat,
                hasLineOfSight = pair.Value.hasLineOfSight,
                lastKnownPosition = pair.Value.lastKnownPosition
            });
        }
    }

    private Vector3 GetTargetPosition(NetworkObject target)
    {
        if (target == null) return Vector3.zero;

        if (target.TryGetComponent<HybridCharacterController>(out var hcc))
        {
            if (hcc.hipsRb != null)
                return hcc.hipsRb.transform.position;
        }

        if (target.TryGetComponent<NPCActiveRagdollController>(out var npc))
        {
            if (npc.coreRB != null)
                return npc.coreRB.transform.position;
        }

        return target.transform.position;
    }


    public Vector3 GetKnownPositionForTarget(NetworkObject target)
    {
        if (target != null && _threatTable.TryGetValue(target, out ThreatInfo info))
        {
            return info.lastKnownPosition;
        }

        return core.position;
    }

    public bool CanSeeTarget(NetworkObject target)
    {
        if (target != null && _threatTable.TryGetValue(target, out ThreatInfo info))
        {
            return info.hasLineOfSight;
        }

        return false;
    }
}




public struct InterestPoint : INetworkStruct
{
    public Vector3 Position;
    public float Threat;
}

public class ThreatInfo
{
   public float persistentThreat = 0f; // focus based threat - ramps up and down for line of sight etc...
    public float spikeThreat = 0f;      //  sudden threat - instantly spikes and decays quickly - for things like sound...
    public float TotalThreat => persistentThreat + spikeThreat;

    public Vector3 lastKnownPosition = Vector3.zero;
    public bool hasLineOfSight = false;
    public AggroDebugDisplay debugDisplay;
}

[System.Serializable]
public struct ThreatDebugEntry
{
    public NetworkObject target;
    public float currentThreat;
    public bool hasLineOfSight;
    public Vector3 lastKnownPosition;
}

