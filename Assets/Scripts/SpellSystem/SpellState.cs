using Fusion;
using System.Collections.Generic;
using UnityEngine;

//The data on this specific spell cast stored passed down in each triggerInfo (which holds information on specific triggers)
public class SpellState
{
    // track the number of cores created in the current casting.
    // A simple way to avoid infinites.
    const int max_spawnable_cores = 50; // hard-cutoff for number cores to spawn within a single spell cast.
    public int ComboIndex { get; set; } = 0;
    public NetworkObject Caster { get; set; }

    public SpellGraphId SpellGraphIdFrom { get; private set; }

    public SpellGraph Spell { get; private set; }

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

    public int SpawnedCoresCounter { get; set; } 

    public CasterNode OriginalCasterNode { get; set; }  //Node responsible for casting the spell. 

    public SpellState(CastActionController controller, EquipableItem item, SpellGraph spell, CasterNode originalCasterNode, NetworkObject caster)
    {
        this.Controller = controller;
        this.CastItem = item;
        this.SpellGraphIdFrom = spell.spellGraphId;
        this.Spell = spell;
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

        SpawnedCoresCounter = 0;
        Caster = caster;
    }

    public bool CanSpawnAnotherCore()
    {
        // check if SpawnedCoresCounter has reached the limit.
        // if not, increment.
        SpawnedCoresCounter++;
        return SpawnedCoresCounter <= max_spawnable_cores;
    }
}
