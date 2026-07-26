using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class RuneRigSpawnController : NetworkBehaviour
{
    private const int MaxPendingRuneRigSpawns = 16;

    private readonly Queue<uint> _pendingRuneRigSpawns = new Queue<uint>();
    private byte _nextRuneRigSpawnSequence;

    [Header("References")]
    public HybridCharacterController CharacterController;

    [Header("Spawn Settings")]
    public float SpawnDistance = 2f;
    public float SpawnHeight;

    [Networked]
    public byte LastProcessedSpawnSequence { get; set; }

    public override void Spawned()
    {
        if (CharacterController == null)
            CharacterController = GetComponent<HybridCharacterController>();
    }

    public bool QueueRuneDefinitionSpawn(ushort definitionId, byte bayCapacity)
    {
        if (definitionId == 0 || bayCapacity > RuneRigLimits.MaxBayCapacity)
            return false;

        if (_pendingRuneRigSpawns.Count >= MaxPendingRuneRigSpawns)
            return false;

        _nextRuneRigSpawnSequence++;

        if (_nextRuneRigSpawnSequence > 127)
            _nextRuneRigSpawnSequence = 1;

        uint command = RuneRigSpawnCommand.Pack(RuneRigSpawnSource.RuneDefinition, definitionId, bayCapacity, _nextRuneRigSpawnSequence);
        _pendingRuneRigSpawns.Enqueue(command);
        return true;
    }

    public bool TryGetNextSpawnCommand(out uint command)
    {
        if (_pendingRuneRigSpawns.Count == 0)
        {
            command = 0u;
            return false;
        }

        command = _pendingRuneRigSpawns.Dequeue();
        return true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out NetworkInputData input))
            return;

        uint command = input.runeRigSpawnCommand;

        if (!RuneRigSpawnCommand.IsValid(command))
            return;

        byte sequence = RuneRigSpawnCommand.GetSequence(command);

        if (sequence == LastProcessedSpawnSequence)
            return;

        LastProcessedSpawnSequence = sequence;

        if (RuneRigSpawnCommand.GetSource(command) != RuneRigSpawnSource.RuneDefinition)
            return;

        ushort definitionId = RuneRigSpawnCommand.GetReferenceId(command);
        byte bayCapacity = RuneRigSpawnCommand.GetVariant(command);

        if (!NodeRegistry.TryGetNodeTemplate(definitionId, out SpellNode definition))
        {
            if (HasStateAuthority)
                Debug.LogWarning($"Rune spawn failed: Definition {definitionId} was not found.", this);

            return;
        }

        if (definition.PhysicalRune == null || definition.PhysicalRune.PhysicalPrefab == null)
        {
            if (HasStateAuthority)
                Debug.LogWarning($"Rune spawn failed: '{definition.name}' has no physical rune prefab.", this);

            return;
        }

        if (!definition.PhysicalRune.IsCapacityAllowed(bayCapacity))
        {
            if (HasStateAuthority)
                Debug.LogWarning($"Rune spawn failed: Capacity {bayCapacity} is not allowed by '{definition.name}'.", this);

            return;
        }

        if (ObjectBufferAllocator.Instance == null || CharacterController == null)
            return;

        ObjectBuffer runeRigBuffer = ObjectBufferAllocator.Instance.GetRuneRigBuffer(Object.InputAuthority);

        if (runeRigBuffer == null)
            return;

        Vector3 eyePosition = CharacterController.GetEyePosSim(CharacterController.hipsRb.position, CharacterController.lookRot);
        Vector3 spawnPosition = eyePosition + CharacterController.lookRot * Vector3.forward * SpawnDistance + Vector3.up * SpawnHeight;

        NetworkObject networkObject = runeRigBuffer.GetBufferedObject(spawnPosition, Quaternion.identity, out _);

        if (networkObject == null || !networkObject.TryGetComponent(out RuneRigObject runeRig))
            return;

        runeRig.StopLevitation();

        RuneRigData rigData = new RuneRigData(new[] { RuneNodeData.CreateLooseRoot(definitionId, bayCapacity) });

        if (!runeRig.TryWriteRigData(rigData, out string error) && HasStateAuthority)
            Debug.LogError($"Rune spawn failed: {error}", runeRig);
    }
}