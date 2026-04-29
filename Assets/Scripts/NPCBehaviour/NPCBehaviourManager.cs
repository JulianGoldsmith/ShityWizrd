using Fusion;
using System.Collections.Generic;
using Unity.Behavior;
using Unity.VisualScripting;
using UnityEngine;
using static GlobalNPCCommandRegistry;

public class NPCBehaviourManager : NetworkBehaviour
{

    [Networked, Capacity(6)]
    public NetworkArray<NPCCommandData> ActiveCommands { get; }
    [Header("Debug")]
    public bool showCommandDebug = true;
    [SerializeField] private List<string> _debugActiveCommands = new List<string>(4);

    [Networked] public int GlobalClearTick { get; set; }

    //Pthing
    //[Networked, Capacity(4)] public NetworkArray<Vector3> TargetWaypoints { get; }
    [Networked]public int CurrentWaypointIndex { get; set; }
    [Networked]public byte PathVersion { get; set; } 


    [Networked] public bool IsFrozen { get; set; }



    [Header("Engine References")]
    public NPCActiveRagdollController muscleController;

    public BehaviorGraphAgent behaviorAgent;

    private List<int> _executionBuffer = new List<int>(4);

    [Header("Global Defaults")]
    public GlobalNPCCommandRegistry globalRegistry; // Assign your 1 master asset here

    [Header("Local Overrides (Only add weird behaviors here!)")]
    public List<Mapping> commandOverrides;

    private Dictionary<CommandType, NPCCommand> _overrideDict = new Dictionary<CommandType, NPCCommand>();

    public NPCAggroController aggroController;

    public override void Spawned()
    {
        globalRegistry.Initialize();

        if(aggroController == null)
        {
            aggroController = this.GetComponent<NPCAggroController>();
        }

        foreach (var mapping in commandOverrides)
        {
            if (mapping.Command != null)
                _overrideDict[mapping.Type] = (NPCCommand)mapping.Command;
        }

        if (behaviorAgent != null && HasStateAuthority)
        {
            behaviorAgent.enabled = true;
            behaviorAgent.Init();
            behaviorAgent.Start();
        }
        else if(behaviorAgent != null)
        {
            behaviorAgent.enabled = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority && behaviorAgent != null)
        {
          //  behaviorAgent.Graph.Tick();
           
        }

        if (aggroController != null) aggroController.TickAggroSensors();

        _executionBuffer.Clear();

        for (int i = 0; i < ActiveCommands.Length; i++)
        {
            var cmd = ActiveCommands[i];
            if (cmd.CommandID == CommandType.None) continue;

            if (GlobalClearTick > 0 && Runner.Tick >= GlobalClearTick && cmd.SetTick < GlobalClearTick)
            {
                ActiveCommands.Set(i, default);
                continue;
            }

            if (Runner.Tick > cmd.EndTick)
            {
                ActiveCommands.Set(i, default);
                continue;
            }

            NPCCommand processor = GetProcessorForCommand(cmd.CommandID);
            if (processor != null)
            {
                if (Runner.Tick >= cmd.SetTick && Runner.Tick < cmd.StartTick)
                {
                    processor.PreTick(ref cmd, this, muscleController);
                    ActiveCommands.Set(i, cmd);
                }
                else if (Runner.Tick >= cmd.StartTick)
                {
                    _executionBuffer.Add(i);
                }
            }
        }

        // 2. SORT BY PRIORITY (Lowest to Highest)
        // _executionBuffer.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        // 3. EXECUTE
        foreach (int index in _executionBuffer)
        {
            // Pull the live command
            var cmd = ActiveCommands[index];
            NPCCommand command = GetProcessorForCommand(cmd.CommandID);

            if (command != null)
            {
                command.ActiveTick(ref cmd, this, muscleController);
                ActiveCommands.Set(index, cmd);
            }
        }
    }

    private NPCCommand GetProcessorForCommand(CommandType type)
    {
        // 1. Check if this specific NPC has a weird/custom way of doing this
        if (_overrideDict.TryGetValue(type, out NPCCommand overrideProcessor))
        {
            return overrideProcessor;
        }

        // 2. Otherwise, just use the universal math!
        return globalRegistry.GetUniversalCommand(type);
    }

    public bool TryAddCommand(NPCCommandData payload)
    {
        for (int i = 0; i < ActiveCommands.Length; i++)
        {
            if (ActiveCommands[i].CommandID == CommandType.None)
            {
                ActiveCommands.Set(i, payload);
                return true;
            }
        }

        Debug.LogWarning($"[NPCBehaviourManager] Command Array is full! Could not add {payload.CommandID}");
        return false;
    }

    public bool HasActiveCommand(CommandType type, NetworkId specificTarget = default)
    {
        for (int i = 0; i < ActiveCommands.Length; i++)
        {
            var cmd = ActiveCommands[i];

            if (cmd.CommandID == type)
            {
                if (specificTarget.IsValid)
                {
                    if (cmd.TargetID == specificTarget) return true;
                }
                else
                {
                    return true;
                }
            }
        }
        return false;
    }

    public void SetGlobalClearTick(int delayTicks = 0)
    {
        if (Runner == null)
        {
            Debug.LogWarning("[NPCBehaviourManager] Tried to set clear tick, but Runner is null!");
            return;
        }

        GlobalClearTick = Runner.Tick + Mathf.Max(0, delayTicks);
    }


    public override void Render()
    {
        if (showCommandDebug && ActiveCommands.Length > 0)
        {
            _debugActiveCommands.Clear();

            for (int i = 0; i < ActiveCommands.Length; i++)
            {
                var cmd = ActiveCommands[i];

                if (cmd.CommandID == CommandType.None)
                {
                    _debugActiveCommands.Add($"[{i}] --- EMPTY ---");
                }
                else
                {
                    // Format: [Slot] CommandType | Start: 100 | End: 500
                    string status = Runner.Tick < cmd.StartTick ? "(WAITING)" : "(ACTIVE)";
                    _debugActiveCommands.Add($"[{i}] {cmd.CommandID} {status} | Start: {cmd.StartTick} | End: {cmd.EndTick}");
                }
            }
        }
    }


    [ContextMenu("TEST: Move Forward")]
    public void InjectTestMove()
    {
        var testData = new NPCCommandData
        {
            CommandID = CommandType.Move_PathfindToID, // Make sure this matches your Enum
            Priority = 10,
            StartTick = Runner.Tick,
            EndTick = Runner.Tick + 300, // Move for 5 seconds
            VectorData = transform.forward, // Move straight ahead
            FloatData = 3f // Speed
        };
        ActiveCommands.Set(0, testData);
    }

    [ContextMenu("TEST: Stop")]
    public void InjectTestStop()
    {
        var testData = new NPCCommandData
        {
            CommandID = CommandType.Move_Stop,
            Priority = 20, // Higher priority overwrites movement!
            StartTick = Runner.Tick,
            EndTick = Runner.Tick + 300
        };
        ActiveCommands.Set(0, testData);
    }
}