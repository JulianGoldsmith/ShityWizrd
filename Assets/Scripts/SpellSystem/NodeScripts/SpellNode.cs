using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.VirtualTexturing;

[System.Serializable]
public abstract class SpellNode : ScriptableObject
{
    //This is the base node for the whole spell system
     [System.NonSerialized] public string InstanceGuid;
    public string nodeName;
    public string description;
    public RuneCategoryTag category;
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
            Debug.LogError($"[{this.name}] is trying to store basevalues from {obj} but baseValues does not exist.");
            return;
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
    protected void ApplyPromotableValuesGeneric<T>(T obj)
    {
        // Extend this to work with anything, not just a spellnode
        // (so that it can work with PhysicsObjectProperties too.
        if(obj == null)
        {
            Debug.LogError("Tried to modify values on a null-object.");
            return;
        }

        if (baseValues == null)
        {
            Debug.LogError($"Base values have not been stored for node {obj} you need to call StoredBaseValues() after cloning");
            return;
        }
        var fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            var promotableAttr = field.GetCustomAttribute<PromotableAttribute>();
            if (promotableAttr != null)
            {
                object? baseValue;
                if (!baseValues.TryGetValue(field.Name, out baseValue))
                    return;

                var found = valueContainers.FirstOrDefault(c => c.TargetFieldName == field.Name);
                Debug.Log($"[{GetType().Name}] Apply: field={field.Name}, type={field.FieldType}, " +
                          $"base={baseValue}, " +
                          $"binder={(found != null)}, mods={(found?.ModifyingNodes?.Count ?? 0)}");

                if (field.FieldType == typeof(float))
                {
                    float finalValue = GetFinalValue(field.Name, (float)baseValue);
                    field.SetValue(obj, finalValue);
                }
                else if (field.FieldType == typeof(bool))
                {
                    bool finalValue = GetFinalValue(field.Name, (bool)baseValue);
                    field.SetValue(obj, finalValue);
                }
                else if (field.FieldType == typeof(PhysicsObjectMaterial))
                {
                    // Only ever replace, so don't need to 'calculate' a final value.
                    PhysicsObjectMaterial finalValue = GetFinalValue(field.Name, (PhysicsObjectMaterial)baseValue);
                    field.SetValue(obj, finalValue);
                }
            }

        }
    }
    protected void ApplyPromotableValues()
    {
        ApplyPromotableValuesGeneric(this);
    }
    
    protected float GetFinalValue(string fieldName, float baseValue)
    {
        float finalValue = baseValue;

        var container = valueContainers.FirstOrDefault(c =>
            c.TargetFieldName == fieldName &&
            c.OwningNodeGUID == this.InstanceGuid);
        if (container == null)
        {
            return baseValue;
        }

        var modifiers = new List<ValueModifier<float>>();
        foreach (var node in container.ModifyingNodes)
        {
            if (node is ValueNode<float> floatNode)
            {
                modifiers.Add(floatNode.GetModifier(null));
            }
        }

        var setModifiers = modifiers.Where(m => m.Type == ValueModifierType.Set).ToList();
        if (setModifiers.Any())
        {
            finalValue = setModifiers.Last().Value;
        }
        foreach (var mod in modifiers.Where(m => m.Type == ValueModifierType.Add))
        {
            finalValue += mod.Value;
        }
        foreach (var mod in modifiers.Where(m => m.Type == ValueModifierType.Multiply))
        {
            finalValue *= mod.Value;
        }
        return finalValue;
    }

    protected bool GetFinalValue(string fieldName, bool baseValue)
    {
        bool finalValue = baseValue;

        var container = valueContainers.FirstOrDefault(c =>
            c.TargetFieldName == fieldName &&
            c.OwningNodeGUID == this.InstanceGuid);
        if (container == null)
        {
            return baseValue; 
        }

        var modifiers = new List<ValueModifier<bool>>();
        foreach (var node in container.ModifyingNodes)
        {
            if (node is ValueNode<bool> boolNode)
            {
 
                modifiers.Add(boolNode.GetModifier(null)); 
            }
        }

        var setModifiers = modifiers.Where(m => m.Type == ValueModifierType.Set).ToList();
        if (setModifiers.Any())
        {
            finalValue = setModifiers.Last().Value;
        }

        return finalValue;
    }

    protected PhysicsObjectMaterial GetFinalValue(string fieldName, PhysicsObjectMaterial baseValue)
    {
        PhysicsObjectMaterial finalValue = baseValue;

        var container = valueContainers.FirstOrDefault(c =>
            c.TargetFieldName == fieldName &&
            c.OwningNodeGUID == this.InstanceGuid);
        if (container == null)
        {
            return baseValue;
        }

        var modifiers = new List<ValueModifier<PhysicsObjectMaterial>>();
        foreach (var node in container.ModifyingNodes)
        {
            if (node is ValueNode<PhysicsObjectMaterial> matNode)
            {
                modifiers.Add(matNode.GetModifier(null));
            }
        }

        var setModifiers = modifiers.Where(m => m.Type == ValueModifierType.Set).ToList();
        if (setModifiers.Any())
        {
            finalValue = setModifiers.Last().Value;
        }
        return finalValue;
    }


    public virtual SpellNode CloneThisNode()
    {
        var spellNodeBase = Instantiate(this);
        spellNodeBase.valueContainers = new List<PropertyBinder>();
        spellNodeBase.StoreBaseValues();
        spellNodeBase.name = this.name;
        return spellNodeBase;
    }
}

public enum RuneCategoryTag
{
    Cast,
    Core,
    Behaviour,
    Trigger,
    Filter,
    Effect,
    Value
}

public enum CasterTriggerMethod
{
    OnCast,         // Triggers on the main execution event (e.g., "ExecuteCore")
    OnHitboxActivate  // Triggers when the hitbox is activated (e.g., "ActivateHitBox")
}





