using System;

public static class SpellBlueprintHydrator
{
    public static bool TryHydrate(SpellGraph graph, out RuntimeSpell runtimeSpell, out string error)
    {
        runtimeSpell = null;
        error = null;

        if (graph == null)
        {
            error = "SpellGraph is null.";
            return false;
        }

        if (graph.Data.Nodes == null || graph.Data.Nodes.Length == 0)
        {
            error = "SpellGraph contains no node data.";
            return false;
        }

        try
        {
            SpellCompilationContext context = new SpellCompilationContext();
            IRuntimeNode[] hydratedNodes = SpellHydrator.HydrateFullGraph(graph.Data, context);

            return TryCreateRuntimeSpell(
                SpellBlueprintFormat.LegacyGraph,
                hydratedNodes,
                out runtimeSpell,
                out error
            );
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static bool TryHydrate(RuneSpellBlueprintData blueprint, out RuntimeSpell runtimeSpell, out string error)
    {
        runtimeSpell = null;
        error = null;

        if (blueprint.NodeCount <= 0)
        {
            error = "Rune spell blueprint contains no nodes.";
            return false;
        }

        try
        {
            IRuntimeNode[] hydratedNodes = RuneSpellHydrator.Hydrate(blueprint);

            return TryCreateRuntimeSpell(
                SpellBlueprintFormat.RuneRig,
                hydratedNodes,
                out runtimeSpell,
                out error
            );
        }
        catch (Exception exception)
        {
            error = exception.ToString();
            return false;
        }
    }

    private static bool TryCreateRuntimeSpell(SpellBlueprintFormat format, IRuntimeNode[] hydratedNodes, out RuntimeSpell runtimeSpell, out string error)
    {
        runtimeSpell = null;
        error = null;

        if (hydratedNodes == null || hydratedNodes.Length == 0)
        {
            error = "Hydration produced no runtime nodes.";
            return false;
        }

        if (hydratedNodes[0] == null)
        {
            error = "Hydration produced a null root node.";
            return false;
        }

        if (!TryGetEntryType(hydratedNodes[0], out EntryPointType entryType))
        {
            error = $"Runtime root '{hydratedNodes[0].GetType().Name}' is not a valid spell entry.";
            return false;
        }

        runtimeSpell = new RuntimeSpell(format, entryType, hydratedNodes);
        return true;
    }

    private static bool TryGetEntryType(IRuntimeNode rootNode, out EntryPointType entryType)
    {
        entryType = default;

        if (rootNode is RuntimeEntryPoint entryPoint)
        {
            entryType = entryPoint.ExpectedType;
            return true;
        }

        if (rootNode is IRuntimeCore)
        {
            entryType = EntryPointType.SpawnCore;
            return true;
        }

        if (rootNode is ITrigger)
        {
            entryType = EntryPointType.Trigger;
            return true;
        }

        if (rootNode is IEffect)
        {
            entryType = EntryPointType.Effect;
            return true;
        }

        return false;
    }
}