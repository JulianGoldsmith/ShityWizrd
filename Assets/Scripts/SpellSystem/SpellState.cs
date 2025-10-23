using Fusion;
using System.Collections.Generic;
using UnityEngine;

//The data on this specific spell cast stored passed down in each triggerInfo (which holds information on specific triggers)
public class SpellState
{
    public SpellStateID SpellStateID;
    public CastActionController Controller { get; } //this means we can always get the player / enemies pos and rot + GetForward etc

    //Snapshot on cast varibales geenrally set by the CasterNode
    public EquipableItem CastItem { get; set; } //this means we can always get the items pos and rot
    public Vector3 CastPosition { get; set; } //Position in world space when the spell is created
    public Quaternion CastRotation { get; set; } //Rotation in world space when the spell is created
    public Vector3 CastAimTargetPos { get; set; } //this will the point where our camera was looking when cast. or AI equivalent
    public Vector3 CastVelocity {get; set; } //this will represent the force and direction of the original cast. 
    public float CastChargeLevel { get; set; } = 0f; 

    public bool isHeld = false; //Represents if the player/ spell/ AI is holding down cast 
    public float ChargeStartTime { get; set; }
    public GameObject chargeCastVFX;

    public Dictionary<string, float> floatValues = new();
    public Dictionary<string, object> objectValues = new();

    public CasterNode OriginalCasterNode { get; set; }  //Node responsible for casting the spell. 

    public SpellState(CastActionController controller, EquipableItem item, CasterNode originalCasterNode)
    {
        this.SpellStateID = new SpellStateID(controller);
        this.Controller = controller;
        this.CastItem = item;

        if (item != null && item.projectileSpawnPoint != null)
        {
            this.CastPosition = item.projectileSpawnPoint.position;
            this.CastRotation = item.projectileSpawnPoint.rotation;
        }
        else if (controller != null)
        {
            this.CastPosition = controller.transform.position;
            this.CastRotation = controller.transform.rotation;
        }

        OriginalCasterNode = originalCasterNode;
    }

    public void SetFloat(string key, float value)
    {
        floatValues[key] = value;
    }

    public float GetFloat(string key, float fallback = 0f)
    {
        return floatValues.TryGetValue(key, out var val) ? val : fallback;
    }

    public bool TryGetFloat(string key, out float value)
    {
        float? result = this.GetFloat(key);
        if (result.HasValue)
        {
            value = result.Value; // Assign the value if found
            return true;          // Return true for success
        }
        else
        {
            value = default;      // Assign a default value
            return false;         // Return false for failure
        }
    }
}

public struct SpellStateID : INetworkStruct
{
    public NetworkBehaviourId nb_id;
    public int cast_id;
    public SpellStateID(NetworkBehaviour nb, int? guid_override = null)
    {
        // create a unique id.
        nb_id = nb.Id;
        if (guid_override != null)
            cast_id = guid_override.Value;
        else
            cast_id= CreateNewGuid(nb);
    }
    static int CreateNewGuid(NetworkBehaviour nb)
    {
        // increment guid, return prior.
        return SpellStateManager.instance.GetNextSpellStateId(nb);
    }
}