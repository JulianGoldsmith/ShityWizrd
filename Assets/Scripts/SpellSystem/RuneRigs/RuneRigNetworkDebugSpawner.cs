using Fusion;
using UnityEngine;

public class RuneRigNetworkDebugSpawner : MonoBehaviour
{
    public NetworkRunner Runner;

    public SpellNode Egg;
    public SpellNode Impact;
    public SpellNode Freeze;

    public Vector3 EggSpawnOffset = new Vector3(-1f, 1f, 2f);
    public Vector3 AssemblySpawnOffset = new Vector3(1f, 1f, 2f);

    [ContextMenu("Spawn Networked Test Rigs")]
    public void SpawnNetworkedTestRigs()
    {
        if (Runner == null)
            Runner = FindFirstObjectByType<NetworkRunner>();

        if (Runner == null || !Runner.IsRunning)
        {
            Debug.LogError("[RuneRigSpawner] No running NetworkRunner was found.", this);
            return;
        }

        if (!Runner.IsServer)
        {
            Debug.LogWarning("[RuneRigSpawner] Rune spawning is host-only for now.", this);
            return;
        }

        if (Egg == null || Impact == null || Freeze == null)
        {
            Debug.LogError("[RuneRigSpawner] Assign Egg, Impact and Freeze definitions.", this);
            return;
        }

        RuneRigData eggData = new RuneRigData
        {
            Nodes = new[]
            {
                RuneNodeData.CreateLooseRoot(Egg.NetworkNodeID, 5)
            }
        };

        RuneRigData assemblyData = new RuneRigData
        {
            Nodes = new[]
            {
                RuneNodeData.CreateLooseRoot(Impact.NetworkNodeID, 2),
                RuneNodeData.CreateChild(Freeze.NetworkNodeID, 0, 0, true, 0)
            }
        };

        SpawnRig(eggData, transform.position + EggSpawnOffset);
        SpawnRig(assemblyData, transform.position + AssemblySpawnOffset);
    }

    [ContextMenu("Despawn All Networked Rune Rigs")]
    public void DespawnAllNetworkedRuneRigs()
    {
        if (Runner == null || !Runner.IsRunning || !Runner.IsServer)
            return;

        RuneRigObject[] rigs = FindObjectsByType<RuneRigObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (RuneRigObject rig in rigs)
        {
            if (rig != null && rig.Object != null && rig.Object.IsValid && rig.Object.HasStateAuthority)
                Runner.Despawn(rig.Object);
        }
    }

    private void SpawnRig(RuneRigData rigData, Vector3 position)
    {
        if (ObjectBufferAllocator.Instance == null)
        {
            Debug.LogError("[RuneRigSpawner] No ObjectBufferAllocator exists.", this);
            return;
        }

        ObjectBuffer runeRigBuffer = ObjectBufferAllocator.Instance.GetRuneRigBuffer(Runner.LocalPlayer);

        if (runeRigBuffer == null)
        {
            Debug.LogError("[RuneRigSpawner] The host RuneRig buffer could not be found.", this);
            return;
        }

        NetworkObject networkObject = runeRigBuffer.GetBufferedObject(position, Quaternion.identity, out _);

        if (networkObject == null || !networkObject.TryGetComponent(out RuneRigObject runeRig))
        {
            Debug.LogError("[RuneRigSpawner] The RuneRig buffer returned an invalid object.", this);
            return;
        }
        runeRig.StopLevitation();
        if (!runeRig.TryWriteRigData(rigData, out string error))
        {
            Debug.LogError($"[RuneRigSpawner] Failed to initialize rig: {error}", this);
            Runner.Despawn(networkObject);
        }
    }
}