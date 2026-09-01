using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering.VirtualTexturing;

[System.Serializable]
public abstract class SpellNode : ScriptableObject
{
    //This is the base node for the whole spell system
     [System.NonSerialized] public string InstanceGuid;
    public string nodeName;
    public string description;
    public List<string> nodeTags = new List<string>();
    [HideInInspector]
    public abstract List<SocketDefinition> GetSockets();
    [HideInInspector]
    public List<PropertyBinder> valueContainers = new List<PropertyBinder>();

    [System.NonSerialized]
    protected Dictionary<string, object> baseValues; //values that can be overwritten with the promotable attribute are stored here

    public Texture2D icon;
    public Mesh overrideMesh = null;
    public Material overrideMaterial = null;
    public float ovverideVisualScale = 1f;

    [Header("Network Identity")]
    [Tooltip("Assigned by the Dictionary Publisher. Do not edit.")]
    public ushort NetworkNodeID = 0;

    public bool showInSpellEditor = true;

    [Header("Physical Rune")]
    public PhysicalRuneSettings PhysicalRune = new PhysicalRuneSettings();

    public virtual void Compile()
    {
        ApplyPromotableValues();
        AssignCasterSpecificReferecnes(); //things like hitBoxs
    }

    public abstract IRuntimeNode CompileNode(SpellCompilationContext context);

    ////Promotable attribues settings setting 
    public virtual void StoreBaseValues()
    {
        baseValues = new Dictionary<string, object>();
        var fields = this.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            if (field.GetCustomAttribute<PromotableAttribute>() != null)
            {
                baseValues[field.Name] = field.GetValue(this);
            }
        }
    }
    protected void AppendBaseValuesFromDependency<T>(T obj)
    {
        // Allow nodes to store base values from dependencies,
        // for example, an objectcore can store the basevalues from
        // its physicsobject.
        if (baseValues == null)
        {
            StoreBaseValues();
            //Debug.LogError($"[{this.name}] is trying to store basevalues from {obj} but baseValues does not exist.");
            //return;
        }

        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            if (field.GetCustomAttribute<PromotableAttribute>() != null)
            {
                baseValues[field.Name] = field.GetValue(obj);
            }
        }
    }
    public T ApplyPromotableValuesGeneric<T>(T obj_to_mod)
    {
        object obj = obj_to_mod;
        if (obj == null) return (T)obj;

        if (baseValues == null)
        {
            StoreBaseValues();
        }

        var fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            var promotableAttr = field.GetCustomAttribute<PromotableAttribute>();
            if (promotableAttr != null)
            {
                if (baseValues.TryGetValue(field.Name, out object baseValue))
                {
                    // THE DATA-ORIENTED FIX: 
                    // Just pass the base value directly to the object! 
                    // Dynamic math is now handled exclusively by Runtime wrappers.
                    field.SetValue(obj, baseValue);
                }
            }
        }
        return (T)obj;
    }
    protected void ApplyPromotableValues()
    {
        ApplyPromotableValuesGeneric(this);
    }
    

    public virtual SpellNode CloneThisNode()
    {
        var spellNodeBase = Instantiate(this);
        spellNodeBase.valueContainers = new List<PropertyBinder>();
        spellNodeBase.StoreBaseValues();
        spellNodeBase.name = this.name;
        return spellNodeBase;
    }

    public abstract List<SpellNode> GetAllDependentNodes();

    const int infinite_search_catch = 100;
    public SpellNode GetNodeInChain(string instance_guid)
    {
        if (instance_guid == null || instance_guid == "")
            return null;

        // Look through all dependent nodes, and their dependencies recursively 
        // until the given instance guid is found.
        List<SpellNode> search = GetAllDependentNodes();
        int remaining_search_count = search.Count;
        if (remaining_search_count == 0)
            return null;

        int infinite_loop_counter = 0;
        SpellNode next_node = search[0];
        while(next_node != null || remaining_search_count > 0)
        {
            if (infinite_loop_counter >= infinite_search_catch)
                return null;
            infinite_loop_counter++;

            if (next_node != null)
            {
                // can't just put next_node != null in the while loop
                // since entrypoints can have multiple dependents which
                // can remain null.
                if (next_node.InstanceGuid == instance_guid)
                    return next_node;

                search.AddRange(next_node.GetAllDependentNodes());
            }

            remaining_search_count = search.Count;

            if(remaining_search_count > 0)
            {
                next_node = search[0];
                search.RemoveAt(0);
                remaining_search_count--;
                continue;
            }
        }
        return null;
    }

    public void AssignCasterSpecificReferecnes()
    {

    }


    public NodeType GetRuneType() => GetTypeCategory(this.GetType());
    public static NodeType GetTypeCategory(System.Type type)
    {
        if (typeof(EntryPointNode).IsAssignableFrom(type)) return NodeType.System;
        if (typeof(LinkNode).IsAssignableFrom(type)) return NodeType.Link;
        if (typeof(LinkLawNode).IsAssignableFrom(type)) return NodeType.LinkLaw;
        if (typeof(CoreNode).IsAssignableFrom(type)) return NodeType.Core;
        if (typeof(BehaviourNode).IsAssignableFrom(type)) return NodeType.Behaviour;
        if (typeof(TriggerNode).IsAssignableFrom(type)) return NodeType.Trigger;
        if (typeof(EffectNode).IsAssignableFrom(type)) return NodeType.Effect;
        if (typeof(ValueNode).IsAssignableFrom(type)) return NodeType.Value;
        if (typeof(SubgraphNode).IsAssignableFrom(type)) return NodeType.Subgraph;
        return NodeType.Misc;
    }
}



public enum CasterTriggerMethod
{
    OnCast,         // Triggers on the main execution event (e.g., "ExecuteCore")
    OnHitboxActivate  // Triggers when the hitbox is activated (e.g., "ActivateHitBox")
}

public enum NodeType
{
    System = 0,
    Core = 1,
    Behaviour = 2,
    Trigger = 3,
    Effect = 4,
    Value = 5,
    Subgraph = 6,
    Misc = 7,
    Link = 8,
    LinkLaw = 9
}



