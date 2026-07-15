using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public abstract class TriggerNode : SpellNode
{

    public List<FilterNode> filterNodes = new();
    public List<SpellNode> outcomeNodes = new();

    [Header("VFX Taxonomy")]
    public VFXTopology Topology = VFXTopology.Sphere;
    public VFXLifecycle Lifecycle = VFXLifecycle.Sustain;

    public abstract override IRuntimeNode CompileNode(SpellCompilationContext context);
    public abstract void SetUp(GameObject spellCore, SpellState state);

    public List<VFXTheme> GetDownstreamThemes(SpellCompilationContext context)
    {
        List<VFXTheme> themes = new List<VFXTheme>();

        if (context.GraphData.Wires != null && context.TemplateRegistry != null)
        {
            for (int i = 0; i <= context.GraphData.MaxWireIndex; i++)
            {
                var wire = context.GraphData.Wires[i];

                if (wire.FromSocketIndex != 255 && wire.FromNodeIndex == context.CurrentNodeIndex)
                {
                    var targetNodeData = context.GraphData.Nodes[wire.ToNodeIndex];

                    SpellNode targetTemplate = context.TemplateRegistry.FirstOrDefault(n => n.NetworkNodeID == targetNodeData.TemplateID);

                    if (targetTemplate is EffectNode effectNode)
                    {
                        Debug.Log($"[Bake] Found Effect via Wire! Theme: {effectNode.Theme}");
                        if (!themes.Contains(effectNode.Theme))
                        {
                            themes.Add(effectNode.Theme);
                        }
                    }
                }
            }
        }

        // Fallback if no effects were found
        if (themes.Count == 0)
        {
            Debug.Log("[Bake] WARNING: No effect themes found! Defaulting to Fallback (White).");
            themes.Add(VFXTheme.Fallback);
        }

        return themes;
    }

    public virtual void OnAttach(SpellTrigger spelltrigger_mono, float _size)
    {
        spelltrigger_mono.OnAttach(this, _size);
    }

    public override List<SocketDefinition> GetSockets()
    {
        List<SocketDefinition> sockets = new List<SocketDefinition>
            {
                new SocketDefinition(
                    name: "Exec In",
                    type: SocketType.ExecutionLink,
                    direction: SocketDirection.Input,
                    tag: DataTypeTag.Generic,
                    dataType: null,
                    owningNodeGUID: this.InstanceGuid
                ),

                new SocketDefinition(
                    name: "Filters In",
                    type: SocketType.FilterLink,
                    direction: SocketDirection.Input,
                    tag: DataTypeTag.Generic,
                    dataType: typeof(FilterNode),
                    owningNodeGUID: this.InstanceGuid
                ),

                new SocketDefinition(
                    name: "On Event", 
                    type: SocketType.ExecutionLink,
                    direction: SocketDirection.Output,
                    tag: DataTypeTag.Generic,
                    dataType: null,
                    owningNodeGUID: this.InstanceGuid
                ),
            };

        var coreModifiableFields = this.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var field in coreModifiableFields)
        {
            var promotableAttr = field.GetCustomAttribute<PromotableAttribute>(); if (promotableAttr != null)
            {
                sockets.Add(new SocketDefinition(
                    name: promotableAttr.DisplayName,
                    type: SocketType.Data,
                    direction: SocketDirection.Input,
                    tag: promotableAttr.Tag,
                    dataType: field.FieldType,
                    owningNodeGUID: this.InstanceGuid,
                    targetFieldName: field.Name
                ));
            }
        }
        return sockets;
    }

    public override List<SpellNode> GetAllDependentNodes()
    {
        List<SpellNode> dependencies = new List<SpellNode>();
        if (filterNodes != null) dependencies.AddRange(filterNodes.ConvertAll(node => node as SpellNode));
        if (outcomeNodes != null) dependencies.AddRange(outcomeNodes);
        return dependencies;
    }

}

