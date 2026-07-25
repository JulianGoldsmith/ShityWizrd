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
    public SpellState(ActiveCastID spellID, CastActionController controller, EquipableItem item, SpellGraphId blueprintID, NetworkObject caster)
    {
        NetCastData = new NetworkCastData();

        Controller = controller;
        CastItem = item;
        Caster = caster;
        Spell = null;
        OriginalCasterNode = null;

        ActiveCastID = spellID;
        SpellGraphIdFrom = blueprintID;

        if (caster != null)
            NetCastData.CasterId = caster.Id;

        if (item != null && item.TryGetComponent(out NetworkObject itemNetworkObject))
            NetCastData.WeaponId = itemNetworkObject.Id;

        if (item != null && item.projectileSpawnPoint != null)
        {
            CastPosition = item.projectileSpawnPoint.position;
            CastRotation = item.projectileSpawnPoint.rotation;
        }
        else if (controller != null)
        {
            CastPosition = controller.transform.position;
            CastRotation = controller.transform.rotation;
        }

        SpawnedCoresCounter = 0;
    }

    public SpellState(ActiveCastID spellID, CastActionController controller, EquipableItem item, SpellGraph spell, CasterNode originalCasterNode, NetworkObject caster)
        : this(spellID, controller, item, spell != null ? spell.spellGraphId : default, caster)
    {
        Spell = spell;
        OriginalCasterNode = originalCasterNode;
    }

    public SpellState(NetworkRunner runner, NetworkCastData syncedData)
    {
        NetCastData = syncedData;
        Spell = null;

        if (syncedData.CasterId.IsValid && runner.TryFindObject(syncedData.CasterId, out NetworkObject casterObject))
        {
            Caster = casterObject;
            Controller = casterObject.GetComponent<CastActionController>();
        }
        else
        {
            Caster = null;
            Controller = null;
        }

        if (syncedData.WeaponId.IsValid && runner.TryFindObject(syncedData.WeaponId, out NetworkObject weaponObject))
            CastItem = weaponObject.GetComponent<EquipableItem>();
        else
            CastItem = null;

        OriginalCasterNode = null;
    }

    public SpellState(NetworkRunner runner, NetworkCastData syncedData, SpellGraph legacyBlueprint)
        : this(runner, syncedData)
    {
        Spell = legacyBlueprint;
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
