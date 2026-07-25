using UnityEngine;

public class RuneRigFoundationDebug : MonoBehaviour
{
    public SpellNode Egg;
    public SpellNode Impact;
    public SpellNode Freeze;
    public EquipableItem TestWeapon;
    [ContextMenu("Run Merge And Split Test")]
    public void RunMergeAndSplitTest()
    {
        if (Egg == null || Impact == null || Freeze == null)
        {
            Debug.LogError("[RuneRigDebug] Assign Egg, Impact and Freeze definitions.", this);
            return;
        }

        RuneRigData eggRig = new RuneRigData(new[]
        {
            RuneNodeData.CreateLooseRoot(Egg.NetworkNodeID, 5)
        });

        RuneRigData impactAssembly = new RuneRigData(new[]
        {
            RuneNodeData.CreateLooseRoot(Impact.NetworkNodeID, 2),
            RuneNodeData.CreateChild(Freeze.NetworkNodeID, 0, 0, true, 0)
        });

        RuneRigMergeResult mergeResult = RuneRigOperations.Merge(eggRig, impactAssembly, 0, 0, false);

        if (!mergeResult.Succeeded)
        {
            Debug.LogError($"[RuneRigDebug] Merge failed: {mergeResult.Error}", this);
            return;
        }

        Debug.Log($"[RuneRigDebug] Merge succeeded. Combined node count: {mergeResult.CombinedRig.NodeCount}.", this);

        RuneRigData combinedRig = mergeResult.CombinedRig;

        if (combinedRig.NodeCount != 3)
        {
            Debug.LogError("[RuneRigDebug] Combined rig should contain three nodes.", this);
            return;
        }

        if (combinedRig.Nodes[1].ParentNodeIndex != 0 || combinedRig.Nodes[1].ParentBayIndex != 0)
        {
            Debug.LogError("[RuneRigDebug] Impact was not attached to Egg bay zero.", this);
            return;
        }

        if (combinedRig.Nodes[1].ConnectionIsLocked)
        {
            Debug.LogError("[RuneRigDebug] The new Impact-to-Egg connection should be unlocked.", this);
            return;
        }

        if (!combinedRig.Nodes[2].ConnectionIsLocked)
        {
            Debug.LogError("[RuneRigDebug] The internal Freeze connection lost its locked state.", this);
            return;
        }

        RuneRigSplitResult splitResult = RuneRigOperations.Split(combinedRig, 1);

        if (!splitResult.Succeeded)
        {
            Debug.LogError($"[RuneRigDebug] Split failed: {splitResult.Error}", this);
            return;
        }

        Debug.Log($"[RuneRigDebug] Split succeeded. Remaining: {splitResult.RemainingRig.NodeCount}, detached: {splitResult.DetachedRig.NodeCount}.", this);

        if (!AreEqual(eggRig, splitResult.RemainingRig))
        {
            Debug.LogError("[RuneRigDebug] Remaining Egg rig does not match the original.", this);
            return;
        }

        if (!AreEqual(impactAssembly, splitResult.DetachedRig))
        {
            Debug.LogError("[RuneRigDebug] Detached Impact assembly does not match the original.", this);
            return;
        }

        Debug.Log("[RuneRigDebug] Complete merge/split round trip succeeded.", this);
    }

    [ContextMenu("Confirm Locked Connection Is Rejected")]
    public void ConfirmLockedConnectionIsRejected()
    {
        if (Impact == null || Freeze == null)
        {
            Debug.LogError("[RuneRigDebug] Assign Impact and Freeze definitions.", this);
            return;
        }

        RuneRigData lockedAssembly = new RuneRigData(new[]
        {
            RuneNodeData.CreateLooseRoot(Impact.NetworkNodeID, 2),
            RuneNodeData.CreateChild(Freeze.NetworkNodeID, 0, 0, true, 0)
        });

        RuneRigSplitResult result = RuneRigOperations.Split(lockedAssembly, 1);

        if (result.Succeeded)
        {
            Debug.LogError("[RuneRigDebug] A locked connection was incorrectly detached.", this);
            return;
        }

        Debug.Log($"[RuneRigDebug] Locked connection correctly rejected: {result.Error}", this);
    }

    [ContextMenu("Run Blueprint Round Trip Test")]
    public void RunBlueprintRoundTripTest()
    {
        if (Egg == null || Impact == null || Freeze == null)
        {
            Debug.LogError("[RuneRigDebug] Assign Egg, Impact and Freeze definitions.", this);
            return;
        }

        RuneRigData originalRig = new RuneRigData(new[]
        {
        RuneNodeData.CreateLooseRoot(Egg.NetworkNodeID, 5),
        RuneNodeData.CreateChild(Impact.NetworkNodeID, 0, 0, false, 2),
        RuneNodeData.CreateChild(Freeze.NetworkNodeID, 1, 0, true, 0)
    });

        if (!RuneSpellBlueprintBuilder.TryCreateBlueprint(originalRig, out RuneSpellBlueprintData blueprint, out string blueprintError))
        {
            Debug.LogError($"[RuneRigDebug] Blueprint creation failed: {blueprintError}", this);
            return;
        }

        if (originalRig.Nodes[0].ParentNodeIndex != RuneParent.None)
        {
            Debug.LogError("[RuneRigDebug] Blueprint creation modified the original loose rig.", this);
            return;
        }

        if (blueprint.GetNode(0).ParentNodeIndex != RuneParent.Root)
        {
            Debug.LogError("[RuneRigDebug] Blueprint root does not use the ROOT sentinel.", this);
            return;
        }

  

        if (!blueprint.GetNode(2).ConnectionIsLocked)
        {
            Debug.LogError("[RuneRigDebug] Blueprint lost the locked Freeze connection.", this);
            return;
        }

        if (!RuneSpellBlueprintBuilder.TryCreateLooseRig(blueprint, out RuneRigData reconstructedRig, out string reconstructionError))
        {
            Debug.LogError($"[RuneRigDebug] Loose rig reconstruction failed: {reconstructionError}", this);
            return;
        }

        if (!AreEqual(originalRig, reconstructedRig))
        {
            Debug.LogError("[RuneRigDebug] Reconstructed loose rig does not match the original.", this);
            return;
        }

        RuneNodeData[] externalCopy = blueprint.CreateNodeCopy();
        externalCopy[0].ParentNodeIndex = RuneParent.None;

        if (blueprint.GetNode(0).ParentNodeIndex != RuneParent.Root)
        {
            Debug.LogError("[RuneRigDebug] External array modification changed the immutable blueprint.", this);
            return;
        }

        Debug.Log($"[RuneRigDebug] Blueprint round trip succeeded. Runes: {blueprint.NodeCount}.", this);
    }

    [ContextMenu("Run Direct Rune Hydration Test")]
    public void RunDirectRuneHydrationTest()
    {
        if (Egg == null || Impact == null || Freeze == null)
        {
            Debug.LogError("[RuneRigDebug] Assign Egg, Impact and Freeze definitions.", this);
            return;
        }

        RuneRigData looseRig = new RuneRigData(new[]
        {
            RuneNodeData.CreateLooseRoot(Egg.NetworkNodeID,5),
            RuneNodeData.CreateChild(Impact.NetworkNodeID,0,0,false,2),
            RuneNodeData.CreateChild(Freeze.NetworkNodeID,1,0,true,0)
        });

        if (!RuneSpellBlueprintBuilder.TryCreateBlueprint(looseRig, out RuneSpellBlueprintData blueprint, out string blueprintError))
        {
            Debug.LogError($"[RuneRigDebug] Blueprint creation failed: {blueprintError}", this);
            return;
        }

        IRuntimeNode[] runtimeNodes;

        try
        {
            runtimeNodes = RuneSpellHydrator.Hydrate(blueprint);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[RuneRigDebug] Direct hydration failed: {exception.Message}", this);
            return;
        }

        if (runtimeNodes.Length != 3)
        {
            Debug.LogError($"[RuneRigDebug] Expected three runtime nodes but received {runtimeNodes.Length}.", this);
            return;
        }

        if (runtimeNodes[0] is not RuntimeCoreBase runtimeCore)
        {
            Debug.LogError("[RuneRigDebug] Egg did not compile into a RuntimeCoreBase.", this);
            return;
        }

        if (runtimeNodes[1] is not RuntimeTriggerBase runtimeTrigger)
        {
            Debug.LogError("[RuneRigDebug] Impact did not compile into a RuntimeTriggerBase.", this);
            return;
        }

        if (runtimeNodes[2] is not IEffect)
        {
            Debug.LogError("[RuneRigDebug] Freeze did not compile into an IEffect.", this);
            return;
        }

        if (runtimeCore.Triggers.Count != 1 || !ReferenceEquals(runtimeCore.Triggers[0], runtimeTrigger))
        {
            Debug.LogError("[RuneRigDebug] Impact was not connected to the Egg runtime core.", this);
            return;
        }

        if (runtimeTrigger.Outcomes.Count != 1 || !ReferenceEquals(runtimeTrigger.Outcomes[0], runtimeNodes[2]))
        {
            Debug.LogError("[RuneRigDebug] Freeze was not connected as the Impact outcome.", this);
            return;
        }

        if (runtimeNodes[0] is RuntimeEntryPoint)
        {
            Debug.LogError("[RuneRigDebug] Direct hydration unexpectedly created a RuntimeEntryPoint.", this);
            return;
        }

        Debug.Log("[RuneRigDebug] Direct hydration succeeded: Egg → Impact → Freeze. No legacy graph or wires were created.", this);
    }

    [ContextMenu("Run Blueprint Serialization Test")]
    public void RunBlueprintSerializationTest()
    {
        if (Egg == null || Impact == null || Freeze == null)
        {
            Debug.LogError("[RuneRigDebug] Assign Egg, Impact and Freeze definitions.", this);
            return;
        }

        RuneRigData looseRig = new RuneRigData(new[]
        {
        RuneNodeData.CreateLooseRoot(Egg.NetworkNodeID,5),
        RuneNodeData.CreateChild(Impact.NetworkNodeID,0,3,false,2),
        RuneNodeData.CreateChild(Freeze.NetworkNodeID,1,1,true,0)
    });

        if (!RuneSpellBlueprintBuilder.TryCreateBlueprint(looseRig, out RuneSpellBlueprintData originalBlueprint, out string blueprintError))
        {
            Debug.LogError($"[RuneRigDebug] Blueprint creation failed: {blueprintError}", this);
            return;
        }

        byte[] serializedData;
        RuneSpellBlueprintData reconstructedBlueprint;

        try
        {
            serializedData = RuneSpellBlueprintSerializer.Serialize(originalBlueprint);
            reconstructedBlueprint = RuneSpellBlueprintSerializer.Deserialize(serializedData);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[RuneRigDebug] Blueprint serialization failed: {exception.Message}", this);
            return;
        }

        if (serializedData.Length != 17)
        {
            Debug.LogError($"[RuneRigDebug] Expected a 17-byte packet but received {serializedData.Length} bytes.", this);
            return;
        }

        RuneRigData originalData = new RuneRigData(originalBlueprint.CreateNodeCopy());
        RuneRigData reconstructedData = new RuneRigData(reconstructedBlueprint.CreateNodeCopy());

        if (!AreEqual(originalData, reconstructedData))
        {
            Debug.LogError("[RuneRigDebug] Reconstructed blueprint does not match the original blueprint.", this);
            return;
        }

        if (reconstructedBlueprint.GetNode(1).ParentBayIndex != 3)
        {
            Debug.LogError("[RuneRigDebug] Impact lost its parent bay index.", this);
            return;
        }

        if (reconstructedBlueprint.GetNode(2).ParentBayIndex != 1 || !reconstructedBlueprint.GetNode(2).ConnectionIsLocked)
        {
            Debug.LogError("[RuneRigDebug] Freeze lost its bay index or locked connection.", this);
            return;
        }

        IRuntimeNode[] runtimeNodes;

        try
        {
            runtimeNodes = RuneSpellHydrator.Hydrate(reconstructedBlueprint);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[RuneRigDebug] Reconstructed blueprint failed to hydrate: {exception.Message}", this);
            return;
        }

        if (runtimeNodes.Length != 3 || runtimeNodes[0] is not RuntimeCoreBase || runtimeNodes[1] is not RuntimeTriggerBase || runtimeNodes[2] is not IEffect)
        {
            Debug.LogError("[RuneRigDebug] Reconstructed blueprint produced the wrong runtime nodes.", this);
            return;
        }

        Debug.Log($"[RuneRigDebug] Blueprint serialization succeeded. {reconstructedBlueprint.NodeCount} runes used {serializedData.Length} bytes and hydrated correctly.", this);
    }

    [ContextMenu("Run Host Rune Registration Test")]
    public void RunHostRuneRegistrationTest()
    {
        if (Egg == null || Impact == null || Freeze == null)
        {
            Debug.LogError("[RuneRigDebug] Assign Egg, Impact and Freeze definitions.", this);
            return;
        }

        if (SpellStateManager.instance == null)
        {
            Debug.LogError("[RuneRigDebug] No SpellStateManager exists in the scene.", this);
            return;
        }

        if (!SpellStateManager.instance.Object.HasStateAuthority)
        {
            Debug.LogError("[RuneRigDebug] This test must be run on the host.", this);
            return;
        }

        RuneRigData looseRig = new RuneRigData(new[]
        {
        RuneNodeData.CreateLooseRoot(Egg.NetworkNodeID,5),
        RuneNodeData.CreateChild(Impact.NetworkNodeID,0,0,false,2),
        RuneNodeData.CreateChild(Freeze.NetworkNodeID,1,0,true,0)
    });

        if (!RuneSpellBlueprintBuilder.TryCreateBlueprint(looseRig, out RuneSpellBlueprintData blueprint, out string blueprintError))
        {
            Debug.LogError($"[RuneRigDebug] Blueprint creation failed: {blueprintError}", this);
            return;
        }

        SpellGraphId spellId;

        try
        {
            spellId = SpellStateManager.instance.RegisterRuneSpellOnHost(blueprint);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[RuneRigDebug] Host registration failed: {exception.Message}", this);
            return;
        }

        if (spellId.IsNull())
        {
            Debug.LogError("[RuneRigDebug] Host registration returned a null spell ID.", this);
            return;
        }

        if (!SpellStateManager.instance.TryGetRuneSpellBlueprint(spellId, out RuneSpellBlueprintData storedBlueprint))
        {
            Debug.LogError("[RuneRigDebug] Registered blueprint was not stored in the rune blueprint dictionary.", this);
            return;
        }

        RuneRigData originalData = new RuneRigData(blueprint.CreateNodeCopy());
        RuneRigData storedData = new RuneRigData(storedBlueprint.CreateNodeCopy());

        if (!AreEqual(originalData, storedData))
        {
            Debug.LogError("[RuneRigDebug] Stored rune blueprint does not match the submitted blueprint.", this);
            return;
        }

        if (!SpellStateManager.instance.hydratedSpells.TryGetValue(spellId, out RuntimeSpell runtimeSpell))
        {
            Debug.LogError("[RuneRigDebug] Registered rune spell was not added to hydratedSpells.", this);
            return;
        }

        if (runtimeSpell.Format != SpellBlueprintFormat.RuneRig)
        {
            Debug.LogError("[RuneRigDebug] Runtime spell has the wrong blueprint format.", this);
            return;
        }

        if (runtimeSpell.HydratedNodes.Length != 3 || runtimeSpell.RootNode is not RuntimeCoreBase runtimeCore)
        {
            Debug.LogError("[RuneRigDebug] Runtime spell has the wrong nodes or root.", this);
            return;
        }

        if (runtimeCore.Triggers.Count != 1 || runtimeCore.Triggers[0] is not RuntimeTriggerBase runtimeTrigger)
        {
            Debug.LogError("[RuneRigDebug] Impact was not connected to the registered Egg core.", this);
            return;
        }

        if (runtimeTrigger.Outcomes.Count != 1 || runtimeTrigger.Outcomes[0] is not IEffect)
        {
            Debug.LogError("[RuneRigDebug] Freeze was not connected to the registered Impact trigger.", this);
            return;
        }

        Debug.Log($"[RuneRigDebug] Host rune registration succeeded. Spell ID: {spellId.BlueprintNumber}, runes: {storedBlueprint.NodeCount}.", this);
    }

    [ContextMenu("Submit Rune Network Test")]
    public void SubmitRuneNetworkTest()
    {
        if (Egg == null || Impact == null || Freeze == null)
        {
            Debug.LogError("[RuneRigDebug] Assign Egg, Impact and Freeze definitions.", this);
            return;
        }

        if (SpellStateManager.instance == null)
        {
            Debug.LogError("[RuneRigDebug] No SpellStateManager exists in the scene.", this);
            return;
        }

        RuneRigData looseRig = new RuneRigData(new[]
        {
            RuneNodeData.CreateLooseRoot(Egg.NetworkNodeID,5),
            RuneNodeData.CreateChild(Impact.NetworkNodeID,0,0,false,2),
            RuneNodeData.CreateChild(Freeze.NetworkNodeID,1,0,true,0)
        });

        if (!RuneSpellBlueprintBuilder.TryCreateBlueprint(looseRig, out RuneSpellBlueprintData blueprint, out string blueprintError))
        {
            Debug.LogError($"[RuneRigDebug] Blueprint creation failed: {blueprintError}", this);
            return;
        }

        SpellStateManager.instance.SubmitRuneSpellToHost(blueprint, default);
        Debug.Log($"[RuneRigDebug] Sent {blueprint.NodeCount}-rune network test submission.", this);
    }

    [ContextMenu("Equip Test Weapon With Rune Spell")]
    public void EquipTestWeaponWithRuneSpell()
    {
        if (Egg == null || Impact == null || Freeze == null || TestWeapon == null)
        {
            Debug.LogError("[RuneRigDebug] Assign Egg, Impact, Freeze and TestWeapon.", this);
            return;
        }

        if (SpellStateManager.instance == null)
        {
            Debug.LogError("[RuneRigDebug] No SpellStateManager exists in the scene.", this);
            return;
        }

        if (TestWeapon.Object == null || !TestWeapon.Object.IsValid)
        {
            Debug.LogError("[RuneRigDebug] TestWeapon is not a valid spawned NetworkObject.", this);
            return;
        }

        RuneRigData looseRig = new RuneRigData(new[]
        {
        RuneNodeData.CreateLooseRoot(Egg.NetworkNodeID,5),
        RuneNodeData.CreateChild(Impact.NetworkNodeID,0,0,false,2),
        RuneNodeData.CreateChild(Freeze.NetworkNodeID,1,0,true,0)
    });

        if (!RuneSpellBlueprintBuilder.TryCreateBlueprint(looseRig, out RuneSpellBlueprintData blueprint, out string blueprintError))
        {
            Debug.LogError($"[RuneRigDebug] Blueprint creation failed: {blueprintError}", this);
            return;
        }

        SpellStateManager.instance.SubmitRuneSpellToHost(blueprint, TestWeapon.Object.Id);
        Debug.Log($"[RuneRigDebug] Submitted rune spell for weapon '{TestWeapon.name}'.", this);
    }

    private static bool AreEqual(RuneRigData first, RuneRigData second)
    {
        if (first.NodeCount != second.NodeCount)
            return false;

        for (int i = 0; i < first.NodeCount; i++)
        {
            RuneNodeData firstNode = first.Nodes[i];
            RuneNodeData secondNode = second.Nodes[i];

            if (firstNode.RuneDefinitionId != secondNode.RuneDefinitionId)
                return false;

            if (firstNode.ParentNodeIndex != secondNode.ParentNodeIndex)
                return false;

            if (firstNode.ParentBayConnection != secondNode.ParentBayConnection)
                return false;

            if (firstNode.BayCapacity != secondNode.BayCapacity)
                return false;
        }

        return true;
    }
}