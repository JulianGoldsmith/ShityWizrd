using Fusion;
using System.Collections.Generic;
using UnityEngine;

//The data on this specific spell cast stored passed down in each triggerInfo (which holds information on specific triggers)
public class SpellState
{
    const int max_spawnable_cores = 50;

    public NetworkCastData NetCastData;


    public NetworkObject Caster { get; set; }
    public CastActionController Controller { get; }
    public EquipableItem CastItem { get; set; }
    public SpellGraph Spell { get; private set; }
    public CasterNode OriginalCasterNode { get; set; }
    public GameObject chargeCastVFX { get; set; }



    public ActiveCastID ActiveCastID
    {
        get => NetCastData.CastID;
        set => NetCastData.CastID = value;
    }

    public SpellGraphId SpellGraphIdFrom
    {
        get => NetCastData.BlueprintID;
        private set => NetCastData.BlueprintID = value;
    }

    public int ComboIndex
    {
        get => NetCastData.ComboIndex;
        set => NetCastData.ComboIndex = value;
    }

    public Vector3 CastPosition
    {
        get => NetCastData.CastPosition;
        set => NetCastData.CastPosition = value;
    }

    public Quaternion CastRotation
    {
        get => NetCastData.CastRotation;
        set => NetCastData.CastRotation = value;
    }

    public Vector3 CastAimTargetPos
    {
        get => NetCastData.CastAimTargetPos;
        set => NetCastData.CastAimTargetPos = value;
    }

    public Vector3 CastVelocity
    {
        get => NetCastData.CastVelocity;
        set => NetCastData.CastVelocity = value;
    }

    public float CastChargeLevel
    {
        get => NetCastData.CastChargeLevel;
        set => NetCastData.CastChargeLevel = value;
    }

    public bool isHeld
    {
        get => NetCastData.IsHeld;
        set => NetCastData.IsHeld = value;
    }

    public float ChargeStartTime
    {
        get => NetCastData.ChargeStartTime;
        set => NetCastData.ChargeStartTime = value;
    }

    public int SpawnedCoresCounter
    {
        get => NetCastData.SpawnedCoresCounter;
        set => NetCastData.SpawnedCoresCounter = value;
    }
    public SpellState(ActiveCastID spellID, CastActionController controller, EquipableItem item, SpellGraph spell, CasterNode originalCasterNode, NetworkObject caster)
    {
        // 1. Initialize the internal struct
        NetCastData = new NetworkCastData();

        // 2. Set the facade references (so your Enemy AI / Player UI doesn't break)
        this.Controller = controller;
        this.CastItem = item;
        this.Spell = spell;
        this.OriginalCasterNode = originalCasterNode;
        this.Caster = caster;

        // 3. Populate the network struct with the pass-throughs
        this.ActiveCastID = spellID;
        this.SpellGraphIdFrom = spell.spellGraphId;

        // 4. Safely extract network IDs for the struct
        if (caster != null) NetCastData.CasterId = caster.Id;
        if (item != null && item.TryGetComponent<NetworkObject>(out var itemNet))
        {
            NetCastData.WeaponId = itemNet.Id;
        }

        // 5. Initial Positioning
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

        this.SpawnedCoresCounter = 0;
    }

    public SpellState(NetworkRunner runner, NetworkCastData syncedData, SpellGraph blueprint)
    {
        this.NetCastData = syncedData;
        this.Spell = blueprint;

        if (syncedData.CasterId.IsValid && runner.TryFindObject(syncedData.CasterId, out NetworkObject casterObj))
        {
            this.Caster = casterObj;
            this.Controller = casterObj.GetComponent<CastActionController>();
        }
        else
        {
            this.Caster = null;
            this.Controller = null;
        }

        if (syncedData.WeaponId.IsValid && runner.TryFindObject(syncedData.WeaponId, out NetworkObject weaponObj))
        {
            this.CastItem = weaponObj.GetComponent<EquipableItem>();
        }
        else
        {
            this.CastItem = null;
        }
        this.OriginalCasterNode = null;
    }

    public bool CanSpawnAnotherCore()
    {
        // check if SpawnedCoresCounter has reached the limit.
        // if not, increment.
        SpawnedCoresCounter++;
        return SpawnedCoresCounter <= max_spawnable_cores;
    }
}

public struct NetworkCastData : INetworkStruct
{
    public ActiveCastID CastID;
    public SpellGraphId BlueprintID;

    public NetworkId CasterId;
    public NetworkId WeaponId;

    public Vector3 CastPosition;
    public Quaternion CastRotation;
    public Vector3 CastAimTargetPos;
    public Vector3 CastVelocity;

    public float CastChargeLevel;
    public int ComboIndex;
    public int SpawnedCoresCounter;

    public NetworkBool IsHeld;
    public float ChargeStartTime;
}
