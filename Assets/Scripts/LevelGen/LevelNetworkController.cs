using Fusion;
using UnityEngine;

public class LevelNetworkController : NetworkBehaviour
{
    private LevelGenerator levelGenerator;

    [Header("Level gen")]
    [Networked, OnChangedRender(nameof(OnLevelSeedChanged))]
    public int LevelSeed { get; set; }
    private int oldSeed = -1;

    public override void Spawned()
    {
        levelGenerator = GetComponent<GameController>().levelGenerator;
    }

    public void HostGenerateNewLevel(int seed = -1)
    {
        if (!HasStateAuthority) return;

        RPC_GenerateLevel(seed);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_GenerateLevel(int seed)
    {
        Debug.Log($"RPC_GenerateLevel received. Generating level with seed: {seed}");

        this.LevelSeed = seed;

        oldSeed = this.LevelSeed;

        levelGenerator.StartGeneration(seed);
    }

    private void OnLevelSeedChanged()
    {
        if (this.LevelSeed != oldSeed && !HasStateAuthority)
        {
            Debug.Log($"OnChanged: Late-joiner detected. Generating level with seed: {this.LevelSeed}");
            levelGenerator.StartGeneration(this.LevelSeed);
        }
    }
}