using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class SpellHydrator
{
    /// <summary>
    /// Converts a flat network array of bytes into a fully linked C# execution graph.
    /// </summary>
    public static IRuntimeNode[] HydrateFullGraph(SpellNetworkData data, List<SpellNode> templateRegistry, SpellCompilationContext context)
    {
        IRuntimeNode[] compiledNodes = new IRuntimeNode[data.MaxNodeIndex + 1];

        // ==========================================
        // PASS 1: INSTANTIATION (Read the Nodes)
        // ==========================================
        for (int i = 0; i <= data.MaxNodeIndex; i++)
        {
            if (data.Nodes[i].TemplateID != 0)
            {
                SpellNode template = templateRegistry.FirstOrDefault(n => n.NetworkNodeID == data.Nodes[i].TemplateID);
                if (template != null)
                {
                    context.CurrentNodeIndex = i; 
                    compiledNodes[i] = template.CompileNode(context);
                }
            }
        }

        // ==========================================
        // PASS 2: THE SWITCHBOARD (Read the Wires)
        // ==========================================
        for (int i = 0; i <= data.MaxWireIndex; i++)
        {
            WireData wire = data.Wires[i];

            if (wire.FromSocketIndex != 255)
            {
                // The Source is ALWAYS the Output Socket. The Target is ALWAYS the Input Socket.
                IRuntimeNode source = compiledNodes[wire.FromNodeIndex];
                IRuntimeNode target = compiledNodes[wire.ToNodeIndex];

                if (source == null || target == null) continue;

                // 1. Entry Point Wiring
                if (source is RuntimeEntryPoint entryPoint)
                {
                    entryPoint.SetConnection(target);
                }
                // 2. Behaviours plug INTO Cores (Source = Behaviour, Target = Core)
                else if (source is IBehaviour behaviour && target is IRuntimeCore coreTarget)
                {
                    coreTarget.AddBehaviour(behaviour);
                }
                // 3. Cores plug INTO Triggers (Source = Core, Target = Trigger)
                else if (source is IRuntimeCore coreSource && target is ITrigger triggerTarget)
                {
                    coreSource.AddTrigger(triggerTarget);
                }
                // 4. Triggers plug INTO Effects or Downstream Cores (Source = Trigger, Target = Outcome)
                else if (source is ITrigger triggerSource)
                {
                    triggerSource.AddOutcome(target);
                }
            }
        }

        return compiledNodes;
    }
}