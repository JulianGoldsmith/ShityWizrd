using Fusion;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using static GlobalNPCCommandRegistry;

public class NPCBehaviourManager : NetworkBehaviour, ISubduable
{
    private const int COMMAND_CHANNEL_COUNT = 4;

    [Networked, Capacity(COMMAND_CHANNEL_COUNT)]
    public NetworkArray<NPCCommandChannelState> CommandChannels { get; }

    [Header("Debug")]
    public bool showCommandDebug = true;
    [SerializeField] private List<string> _debugCommandChannels = new List<string>(COMMAND_CHANNEL_COUNT);
    [SerializeField] private string _debugActionChannel;

    [Networked] public int CommandRevision { get; set; }
    public int CurrentIntentStartTick { get; private set; }

    //Pthing
    //[Networked, Capacity(4)] public NetworkArray<Vector3> TargetWaypoints { get; }
    [Networked]public int CurrentWaypointIndex { get; set; }
    [Networked]public byte PathVersion { get; set; } 


    [Networked] public bool IsFrozen { get; set; }



    [Header("Engine References")]
    public NPCActiveRagdollController muscleController;
    public BehaviorGraphAgent behaviorAgent;

    [Header("Bonk State")]
    [SerializeField] private BonkManager bonkManager;
    [Range(0f, 1f)][SerializeField] private float rattledThreshold = 0.25f;
    [Range(0f, 1f)][SerializeField] private float overwhelmedThreshold = 0.45f;
    [Range(0f, 1f)][SerializeField] private float subduedThreshold = 0.70f;
    [Range(0f, 1f)][SerializeField] private float unconsciousThreshold = 0.95f;

    public float CurrentBonkNormalized => bonkManager != null ? bonkManager.CurrentBonkNormalized : 0f;

    public NPCBonkState CurrentBonkState
    {
        get
        {
            float bonk = CurrentBonkNormalized;
            if (bonk >= unconsciousThreshold) return NPCBonkState.Unconscious;
            if (bonk >= subduedThreshold) return NPCBonkState.Subdued;
            if (bonk >= overwhelmedThreshold) return NPCBonkState.Overwhelmed;
            if (bonk >= rattledThreshold) return NPCBonkState.Rattled;
            return NPCBonkState.Composed;
        }
    }

    public bool IsSubdued => CurrentBonkState == NPCBonkState.Subdued || CurrentBonkState == NPCBonkState.Unconscious;

    public bool CanDeliberatelyAct => !IsSubdued;

    [Header("Global Defaults")]
    public GlobalNPCCommandRegistry globalRegistry;

    [Header("Local Overrides (Only add weird behaviors here!)")]
    public List<Mapping> commandOverrides;

    private Dictionary<CommandType, NPCCommand> _overrideDict = new Dictionary<CommandType, NPCCommand>();

    public NPCAggroController aggroController;

    public NPCActionManager actionManager;

    public override void Spawned()
    {
        globalRegistry.Initialize();
        CurrentIntentStartTick = Runner.Tick;

        if(aggroController == null)
        {
            aggroController = this.GetComponent<NPCAggroController>();
        }
        if(actionManager == null)
        {
            actionManager = this.GetComponent<NPCActionManager>();
        }

        if (bonkManager == null) bonkManager = GetComponent<BonkManager>();

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
        TickPerception();

        TickCommandChannels();

        TickActionChannel();

        TickRagDollController();
    }

    public void TickPerception()
    {
        if (aggroController != null) aggroController.TickAggroSensors();
    }

    public void TickActionChannel()
    {
        if (actionManager != null) actionManager.TickActionChannel();
    }

    public void TickRagDollController()
    {
        muscleController.Tick();
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

    private static bool TryGetCommandChannelIndex(NPCCommandChannel channel, out int index)
    {
        index = (int)channel - 1;
        return index >= 0 && index < COMMAND_CHANNEL_COUNT;
    }

    public int BeginCommandRevision()
    {
        if (!HasStateAuthority || Runner == null) return 0;

        CommandRevision++;
        return CommandRevision;
    }

    public void SetCurrentIntentStartTick(int startTick)
    {
        if (!HasStateAuthority || Runner == null) return;
        CurrentIntentStartTick = Mathf.Max(Runner.Tick, startTick);
    }

    public int GetCurrentIntentStartTick()
    {
        if (Runner == null) return 0;
        return Mathf.Max(Runner.Tick, CurrentIntentStartTick);
    }

    public bool TryScheduleChannelCommand(NPCCommandData command, int startTick, int revision)
    {
        if (!HasStateAuthority || Runner == null) return false;
        if (command.CommandID == CommandType.None) return false;
        if (revision <= 0 || revision != CommandRevision) return false;

        NPCCommand processor = GetProcessorForCommand(command.CommandID);
        if (processor == null) return false;

        if (!TryGetCommandChannelIndex(processor.Channel, out int channelIndex))
        {
            Debug.LogWarning($"[NPCBehaviourManager] {command.CommandID} is not a sustained channel command.");
            return false;
        }

        int scheduledStartTick = Mathf.Max(Runner.Tick, startTick);
        if (command.EndTick < scheduledStartTick)
        {
            Debug.LogWarning($"[NPCBehaviourManager] {command.CommandID} ends before it starts.");
            return false;
        }

        NPCCommandChannelState channelState = CommandChannels[channelIndex];
        if (channelState.PendingOperation != NPCCommandChannelOperation.None && revision <= channelState.PendingRevision)
        {
            Debug.LogWarning($"[NPCBehaviourManager] {processor.Channel} already has an equal or newer pending decision are you calling this multiple times by accident?.");
            return false;
        }

        command.SetTick = Runner.Tick;
        command.StartTick = scheduledStartTick;

        channelState.PendingCommand = command;
        channelState.PendingOperation = NPCCommandChannelOperation.Set;
        channelState.PendingStartTick = scheduledStartTick;
        channelState.PendingRevision = revision;

        CommandChannels.Set(channelIndex, channelState);
        return true;
    }

    public bool TryScheduleChannelClear(NPCCommandChannel channel, int startTick, int revision)
    {
        if (!HasStateAuthority || Runner == null) return false;
        if (revision <= 0 || revision != CommandRevision) return false;
        if (!TryGetCommandChannelIndex(channel, out int channelIndex)) return false;

        NPCCommandChannelState channelState = CommandChannels[channelIndex];
        if (channelState.PendingOperation != NPCCommandChannelOperation.None && revision <= channelState.PendingRevision)
        {
            Debug.LogWarning($"[NPCBehaviourManager] {channel} already has an equal or newer pending decision.");
            return false;
        }

        channelState.PendingCommand = default;
        channelState.PendingOperation = NPCCommandChannelOperation.Clear;
        channelState.PendingStartTick = Mathf.Max(Runner.Tick, startTick);
        channelState.PendingRevision = revision;

        CommandChannels.Set(channelIndex, channelState);
        return true;
    }

    public bool TryScheduleAllChannelClears(int startTick, int revision)
    {
        if (!HasStateAuthority || Runner == null) return false;

        bool success = true;

        for (int channelIndex = 0; channelIndex < COMMAND_CHANNEL_COUNT; channelIndex++)
        {
            NPCCommandChannel channel = (NPCCommandChannel)(channelIndex + 1);
            if (!TryScheduleChannelClear(channel, startTick, revision)) success = false;
        }

        return success;
    }

    private void PreparePendingChannel(int channelIndex, ref NPCCommandChannelState channelState)
    {
        if (!HasStateAuthority) return;
        if (channelState.PendingOperation != NPCCommandChannelOperation.Set) return;
        if (Runner.Tick > channelState.PendingStartTick) return;

        NPCCommandData pendingCommand = channelState.PendingCommand;
        NPCCommand processor = GetProcessorForCommand(pendingCommand.CommandID);
        if (processor == null) return;

        if (!TryGetCommandChannelIndex(processor.Channel, out int processorChannelIndex) || processorChannelIndex != channelIndex)
        {
            Debug.LogError($"[NPCBehaviourManager] {pendingCommand.CommandID} was stored in the wrong channel.");
            return;
        }

        processor.PreTick(ref pendingCommand, this, muscleController);

        channelState.PendingCommand = pendingCommand;
        CommandChannels.Set(channelIndex, channelState);
    }

    private void CommitPendingChannel(int channelIndex, ref NPCCommandChannelState channelState)
    {
        if (!HasStateAuthority) return;
        if (channelState.PendingOperation == NPCCommandChannelOperation.None) return;

        if (channelState.PendingOperation == NPCCommandChannelOperation.Set)
        {
            channelState.ActiveCommand = channelState.PendingCommand;
            channelState.ActiveRevision = channelState.PendingRevision;
        }
        else if (channelState.PendingOperation == NPCCommandChannelOperation.Clear)
        {
            channelState.ActiveCommand = default;
            channelState.ActiveRevision = channelState.PendingRevision;
        }

        channelState.PendingCommand = default;
        channelState.PendingOperation = NPCCommandChannelOperation.None;
        channelState.PendingStartTick = 0;
        channelState.PendingRevision = 0;

        CommandChannels.Set(channelIndex, channelState);
    }

    public void TickCommandChannels()
    {
        for (int channelIndex = 0; channelIndex < COMMAND_CHANNEL_COUNT; channelIndex++)
        {
            NPCCommandChannelState channelState = CommandChannels[channelIndex];

            if (channelState.PendingOperation == NPCCommandChannelOperation.Set && Runner.Tick <= channelState.PendingStartTick)
            {
                PreparePendingChannel(channelIndex, ref channelState);
            }

            bool pendingIsDue = channelState.PendingOperation != NPCCommandChannelOperation.None && Runner.Tick >= channelState.PendingStartTick;
            NPCCommandData effectiveCommand = channelState.ActiveCommand;
            int effectiveRevision = channelState.ActiveRevision;

            if (pendingIsDue)
            {
                if (channelState.PendingOperation == NPCCommandChannelOperation.Set)
                {
                    effectiveCommand = channelState.PendingCommand;
                    effectiveRevision = channelState.PendingRevision;
                }
                else
                {
                    effectiveCommand = default;
                    effectiveRevision = channelState.PendingRevision;
                }

                if (HasStateAuthority)
                {
                    CommitPendingChannel(channelIndex, ref channelState);
                    effectiveCommand = channelState.ActiveCommand;
                    effectiveRevision = channelState.ActiveRevision;
                }
            }

            if (effectiveCommand.CommandID == CommandType.None) continue;
            if (Runner.Tick < effectiveCommand.StartTick) continue;

            if (Runner.Tick > effectiveCommand.EndTick)
            {
                if (HasStateAuthority)
                {
                    NPCCommandChannelState liveState = CommandChannels[channelIndex];
                    if (liveState.ActiveRevision == effectiveRevision)
                    {
                        liveState.ActiveCommand = default;
                        liveState.ActiveRevision = 0;
                        CommandChannels.Set(channelIndex, liveState);
                    }
                }

                continue;
            }

            NPCCommand processor = GetProcessorForCommand(effectiveCommand.CommandID);
            if (processor == null) continue;

            if (!TryGetCommandChannelIndex(processor.Channel, out int processorChannelIndex) || processorChannelIndex != channelIndex)
            {
                if (HasStateAuthority)
                {
                    Debug.LogError($"[NPCBehaviourManager] {effectiveCommand.CommandID} is executing from the wrong channel.");

                    NPCCommandChannelState liveState = CommandChannels[channelIndex];
                    if (liveState.ActiveRevision == effectiveRevision)
                    {
                        liveState.ActiveCommand = default;
                        liveState.ActiveRevision = 0;
                        CommandChannels.Set(channelIndex, liveState);
                    }
                }

                continue;
            }

            processor.ActiveTick(ref effectiveCommand, this, muscleController);

            if (HasStateAuthority)
            {
                NPCCommandChannelState liveState = CommandChannels[channelIndex];
                if (liveState.ActiveRevision == effectiveRevision && liveState.ActiveCommand.CommandID == effectiveCommand.CommandID)
                {
                    liveState.ActiveCommand = effectiveCommand;
                    CommandChannels.Set(channelIndex, liveState);
                }
            }
        }
    }

    public bool HasActiveCommand(CommandType type, NetworkId specificTarget = default)
    {
        if (type == CommandType.Action_Request && actionManager != null)
        {
            NetworkNPCActionData action = actionManager.ActionData;
            if (!action.isActive) return false;
            return !specificTarget.IsValid || action.targetID == specificTarget;
        }

        for (int channelIndex = 0; channelIndex < COMMAND_CHANNEL_COUNT; channelIndex++)
        {
            NPCCommandData command = CommandChannels[channelIndex].ActiveCommand;
            if (command.CommandID != type) continue;
            if (!specificTarget.IsValid || command.TargetID == specificTarget) return true;
        }

        return false;
    }

    public bool IsRequestQueuedAndWaiting(CommandType type, NetworkId specificTarget = default)
    {
        if (type == CommandType.Action_Request && actionManager != null)
        {
            NetworkNPCActionRequest request = actionManager.ActionChannel.pendingAction;
            if (!request.isValid) return false;
            return !specificTarget.IsValid || request.targetID == specificTarget;
        }

        for (int channelIndex = 0; channelIndex < COMMAND_CHANNEL_COUNT; channelIndex++)
        {
            NPCCommandChannelState channel = CommandChannels[channelIndex];
            if (channel.PendingOperation != NPCCommandChannelOperation.Set) continue;
            if (channel.PendingCommand.CommandID != type || Runner.Tick >= channel.PendingStartTick) continue;
            if (!specificTarget.IsValid || channel.PendingCommand.TargetID == specificTarget) return true;
        }

        return false;
    }

    public override void Render()
    {
        if (!showCommandDebug) return;

        _debugCommandChannels.Clear();

        for (int channelIndex = 0; channelIndex < COMMAND_CHANNEL_COUNT; channelIndex++)
        {
            NPCCommandChannel channelName = (NPCCommandChannel)(channelIndex + 1);
            NPCCommandChannelState channel = CommandChannels[channelIndex];
            string active = channel.ActiveCommand.CommandID == CommandType.None ? "None" : channel.ActiveCommand.CommandID.ToString();
            string pending = channel.PendingOperation == NPCCommandChannelOperation.None ? "None" : $"{channel.PendingOperation} {channel.PendingCommand.CommandID} @ {channel.PendingStartTick}";
            _debugCommandChannels.Add($"{channelName}: Active={active} | Pending={pending}");
        }

        if (actionManager == null)
        {
            _debugActionChannel = "Action: Missing Manager";
            return;
        }

        NetworkNPCActionChannelState actionChannel = actionManager.ActionChannel;
        string activeAction = actionChannel.activeAction.isActive ? $"{actionChannel.activeAction.actionID} rev {actionChannel.activeAction.revision}" : "None";
        string pendingAction = actionChannel.pendingAction.isValid ? $"{actionChannel.pendingAction.actionID} rev {actionChannel.pendingAction.revision} @ {actionChannel.pendingAction.earliestStartTick}" : "None";
        _debugActionChannel = $"Action: Active={activeAction} | Pending={pendingAction}";
    }
}


[BlackboardEnum]
public enum NPCBonkState
{
    Composed,
    Rattled,
    Overwhelmed,
    Subdued,
    Unconscious
}

public interface ISubduable
{
    bool IsSubdued { get; }
}
