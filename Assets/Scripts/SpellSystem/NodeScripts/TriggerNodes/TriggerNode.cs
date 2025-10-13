using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public abstract class TriggerNode : SpellNode
{

    public List<FilterNode> filterNodes = new();
    public List<SpellNode> outcomeNodes = new();

    public VFXContext vfx_context;
    public ModifierType default_vfx_modifier_type;

    public abstract void SetUp(GameObject spellCore, SpellState state);

    public void Execute()
    {

    }

    public virtual void PassThroughVFX(SpellTrigger spelltrigger_mono, float _size)
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


} 