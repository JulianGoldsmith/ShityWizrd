using Fusion;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public abstract class CoreNode : SpellNode
{
    public CasterTriggerMethod casterTriggerMethod = CasterTriggerMethod.OnCast;
    public List<BehaviourNode> defaultBehaviourNodes;
    public List<TriggerNode> defaultTriggerNodes; 
    [HideInInspector]
    public List<BehaviourNode> behaviourNodes = new List<BehaviourNode>();
    [HideInInspector]
    public List<TriggerNode> triggerNodes = new List<TriggerNode>();


    [System.NonSerialized]
    public CoreExecutionPlan CompiledPlan;

    public override void Compile()
    {
        base.Compile(); // Resolves your promotable values
        CompileExecutionPlan();
    }

    private void CompileExecutionPlan()
    {
        CompiledPlan = new CoreExecutionPlan();
        SpellCompilationContext sCC = new SpellCompilationContext();
        // 1. Compile all Behaviours
        if (defaultBehaviourNodes != null)
        {
            foreach (var b in defaultBehaviourNodes)
                if (b != null) CompiledPlan.Behaviours.Add(b.CompileBehaviour(sCC));
        }
        if (behaviourNodes != null)
        {
            foreach (var b in behaviourNodes)
                if (b != null) CompiledPlan.Behaviours.Add(b.CompileBehaviour(sCC));
        }

        // 2. Compile all Triggers
        if (defaultTriggerNodes != null)
        {
            foreach (var t in defaultTriggerNodes)
                if (t != null) CompiledPlan.Triggers.Add(t.CompileTrigger(sCC));
        }
        if (triggerNodes != null)
        {
            foreach (var t in triggerNodes)
                if (t != null) CompiledPlan.Triggers.Add(t.CompileTrigger(sCC));
        }
    }

    public abstract void CreateSpellCore(SpellTriggerInfo triggerInfo);

    protected bool CanSpawn(SpellState state)
    {
        return state?.CanSpawnAnotherCore()??false;
    }

    public void AttatchBehavioursAndTriggers(GameObject spellCore, SpellTriggerInfo triggerInfo)
    {
        foreach (BehaviourNode behaviourNode in defaultBehaviourNodes)
        {
            behaviourNode.SetUp(spellCore, triggerInfo);
        }
        foreach (TriggerNode triggerNode in defaultTriggerNodes)
        {
            triggerNode.SetUp(spellCore, triggerInfo.State);
        }
        foreach (BehaviourNode behaviourNode in behaviourNodes)
        {
            behaviourNode.SetUp(spellCore, triggerInfo);
        }
        foreach (TriggerNode triggerNode in triggerNodes)
        {
            triggerNode.SetUp(spellCore, triggerInfo.State);
        }
    }
    public void AttatchBehavioursAndTriggers(GameObject spellCore, SpellState state)
    {
        // triggerless setup.
        // need to find a way to pass triggerinfo across network.
        foreach (BehaviourNode behaviourNode in defaultBehaviourNodes)
        {
            behaviourNode.SetUp(spellCore, null);
        }
        foreach (TriggerNode triggerNode in defaultTriggerNodes)
        {
            triggerNode.SetUp(spellCore, state);
        }
        foreach (BehaviourNode behaviourNode in behaviourNodes)
        {
            behaviourNode.SetUp(spellCore, null);
        }
        foreach (TriggerNode triggerNode in triggerNodes)
        {
            triggerNode.SetUp(spellCore, state);
        }
    }

    public override List<SocketDefinition> GetSockets()
    {
        var sockets = new List<SocketDefinition>
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
                    name: "Behaviours In",
                    type: SocketType.BehaviourLink,
                    direction: SocketDirection.Input,
                    tag: DataTypeTag.Generic,
                    dataType: typeof(BehaviourNode),
                    owningNodeGUID: this.InstanceGuid
                )
            };

        if (defaultTriggerNodes == null || defaultTriggerNodes.Count == 0)
        {
            // If no innate trigger, show the default triggers out socket so the player can add their own trigger nodes
            sockets.Add(new SocketDefinition(
                name: "Triggers Out",
                type: SocketType.ExecutionLink,
                direction: SocketDirection.Output,
                tag: DataTypeTag.Generic,
                dataType: null,
                owningNodeGUID: this.InstanceGuid
            ));
        }
        else
        {
            // If there is an innate trigger promote its sockets
            foreach (var trigger in defaultTriggerNodes)
            {
                if (trigger == null) continue;

                foreach (var triggerSocket in trigger.GetSockets())
                {
                    // not its own in
                    if (triggerSocket.Direction == SocketDirection.Input && triggerSocket.Name == "Exec In")
                    {
                        continue; 
                    }
                    var promotedSocket = new SocketDefinition(triggerSocket);
                    promotedSocket.OwningNodeGUID = trigger.InstanceGuid;
                    promotedSocket.TargetFieldName = triggerSocket.Name;
                    sockets.Add(promotedSocket);
                }
            }
        }







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


            // inspect our default behaviours for promotable properties.
        if (defaultBehaviourNodes != null)
        {
            foreach (var behaviour in defaultBehaviourNodes)
            {
                if (behaviour == null) continue;

                var fields = behaviour.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var field in fields)
                {
  
                    var promotableAttr = field.GetCustomAttribute<PromotableAttribute>();
                    if (promotableAttr != null)
                    {

                        sockets.Add(new SocketDefinition(
                            name: promotableAttr.DisplayName, 
                            type: SocketType.Data,
                            direction: SocketDirection.Input,
                            tag: promotableAttr.Tag,        
                            dataType: field.FieldType,
                            owningNodeGUID: behaviour.InstanceGuid,
                            targetFieldName: field.Name
                        ));
                    }
                }
            }
        }

        return sockets;
    }

    public override SpellNode CloneThisNode()
    {
        CoreNode newInstance = Instantiate(this);
        newInstance.valueContainers = new List<PropertyBinder>();
        newInstance.StoreBaseValues();
        //this is now handled by creating a new rune
        newInstance.name = this.name;
        newInstance.defaultBehaviourNodes = new List<BehaviourNode>();
        newInstance.defaultTriggerNodes = new List<TriggerNode>();
        newInstance.behaviourNodes = new List<BehaviourNode>();
        newInstance.triggerNodes = new List<TriggerNode>();

        if (this.defaultBehaviourNodes != null)
        {
            foreach (var behaviour in this.defaultBehaviourNodes)
            {
                if (behaviour != null) newInstance.defaultBehaviourNodes.Add(behaviour.CloneThisNode() as BehaviourNode);
            }
        }

        if (this.defaultTriggerNodes != null)
        {
            foreach (var trigger in this.defaultTriggerNodes)
            {
                if (trigger != null) newInstance.defaultTriggerNodes.Add(trigger.CloneThisNode() as TriggerNode);
            }
        }

        return newInstance;
    }

    public override List<SpellNode> GetAllDependentNodes()
    {
        List<SpellNode> dependencies = new List<SpellNode>();
        if (defaultBehaviourNodes != null) dependencies.AddRange(defaultBehaviourNodes.ConvertAll(node => node as SpellNode));
        if (defaultTriggerNodes != null) dependencies.AddRange(defaultTriggerNodes.ConvertAll(node => node as SpellNode));
        if (behaviourNodes != null) dependencies.AddRange(behaviourNodes.ConvertAll(node => node as SpellNode));
        if (triggerNodes != null) dependencies.AddRange(triggerNodes.ConvertAll(node => node as SpellNode));
        return dependencies;
    }
}