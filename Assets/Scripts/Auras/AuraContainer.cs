using UnityEngine;
using Fusion;
using System.Collections.Generic;
using ExitGames.Client.Photon.StructWrapping;
using System.Linq;

public class AuraContainer : NetworkBehaviour
{
    // Contains all Auras attached to the current object.

    // This defines the maximum number auras that can be attached simultaneously.
    const int CAPACITY = 8;

    [Networked, Capacity(CAPACITY), OnChangedRender(nameof(OnActiveAuraIdsChanged))] 
    NetworkArray<AURA_ID> networked_active_aura_ids { get; }
    [Networked, Capacity(CAPACITY), OnChangedRender(nameof(OnActiveAuraTimersChanged))] 
    NetworkArray<TickTimer> networked_active_aura_timers { get; }

    // basically a local copy of the network active aura ids:
    [SerializeField] AURA_ID[] local_active_aura_ids = new AURA_ID[CAPACITY];

    // NOTE: current implementation doesn't allow modification
    // of aura values nor storing of values within an aura.
    // Need to add that. Don't think that really works in the
    // current architecture...

    // Maybe the host creates copy instances of auras where they can 
    // put modified values? E.g. to do X-marks-the-spot, you'd need to
    // store original position.
    //Dictionary<AURA_ID, Aura> host_master_lookup = new Dictionary<AURA_ID, Aura>();

    public override void Spawned()
    {
        base.Spawned();
        for (int i = 0; i < CAPACITY; i++)
        {
            if (HasStateAuthority)
                networked_active_aura_ids.Set(i, AURA_ID.NULL);
            else
                local_active_aura_ids[i] = AURA_ID.NULL;
        }
    }
    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

        // Run the auras each tick.
        // Have to go back to front in case they dissappear.

        for (int i = networked_active_aura_ids.Length - 1; i >= 0; i--)
        {
            if (networked_active_aura_ids[i] == AURA_ID.NULL)
            {
                continue;
            }
            Debug.Log($"Ticking aura {networked_active_aura_ids[i]} at time {networked_active_aura_timers[i].RemainingTime(Runner)}");
            AuraLookUp.Get(networked_active_aura_ids[i])?.OnTick(this);
            if (networked_active_aura_timers[i].Expired(Runner))
                RemoveAuraAtIndex(i);
        }
    }
    public void OnActiveAuraIdsChanged(NetworkBehaviourBuffer previous)
    {
        // Cleanup active auras, do any ticks if necessary.
        // Host doesn't need to do this.
        if (HasStateAuthority)
            return;

        for (int i = 0; i < networked_active_aura_ids.Length; i++)
        {
            // Client side auras.
            // Remove any that the host removed.
            // Add any that the host added.
            if (local_active_aura_ids[i] != networked_active_aura_ids[i])
            {
                if (networked_active_aura_ids[i] == AURA_ID.NULL)
                {
                    // new is null, so remove.
                    RemoveAuraAtIndex(i);
                }
                else
                {
                    // new is non-null, so attach.
                    if (local_active_aura_ids[i] != AURA_ID.NULL)
                    {
                        //local is non-null, so remove first.
                        RemoveAuraAtIndex(i);
                    }

                    AttachAura(networked_active_aura_ids[i]);

                }
            }
        }
    }
    public void OnActiveAuraTimersChanged(NetworkBehaviourBuffer previous)
    {
        for (int i = 0; i < networked_active_aura_timers.Length; i++)
        {
            // Client side auras.
            // Remove any that the host removed.
            // Add any that the host added.
            if (networked_active_aura_timers[i].Expired(Runner))
                RemoveAuraAtIndex(i);
        }
    }
    public void AttachAura(AURA_ID auraid)
    {
        AttachAura(AuraLookUp.Get(auraid));
    }
    public void AttachAura(Aura aura)
    {
        // check if already exists.
        int index;
        index = GetExistingSlot(aura.unique_label);
        Debug.Log($"Attaching aura to {name}");
        if (index > -1)
        {
            // Already exists, so just update.
            Debug.Log($"existing index found for {aura.unique_label} at index {index}");
            CreateAuraAtIndex(aura, index);
            return;
        }

        index = GetFirstEmptySlot();
        if (index < 0)
            throw new System.Exception("Not enough capacity for Aura.");

        Debug.Log($"empty slot found for {aura.unique_label} at {index}");
        CreateAuraAtIndex(aura, index);
        aura.OnApply(this);
    }
    void CreateAuraAtIndex(Aura aura, int index)
    {
        if (HasStateAuthority)
        {
            networked_active_aura_ids.Set(index, aura.unique_label);
            networked_active_aura_timers.Set(index, CreateTimer(aura));
        }
        else
            local_active_aura_ids[index] = aura.unique_label;
    }
    int GetExistingSlot(AURA_ID id)
    {
        for (int i = 0; i < networked_active_aura_ids.Length; ++i)
        {
            if (networked_active_aura_ids[i] == id)
                return i;
        }
        return -1;
    }
    int GetFirstEmptySlot()
    {
        for (int i = 0; i < networked_active_aura_ids.Length; ++i)
        {
            if (networked_active_aura_ids[i] == AURA_ID.NULL)
                return i;
        }
        return -1;
    }
    public void RemoveAuraAtIndex(int index)
    {
        AURA_ID auraid;
        
        if (HasStateAuthority)
            auraid = networked_active_aura_ids[index];
        else
            auraid = local_active_aura_ids[index];

        Debug.Log($"Removing aura at {index} {auraid}");

        if (auraid == AURA_ID.NULL)
            throw new System.Exception($"Null aura at index {index}");

        AuraLookUp.Get(auraid)?.OnExpire(this);

        if (HasStateAuthority)
            networked_active_aura_ids.Set(index, AURA_ID.NULL);
        else
            local_active_aura_ids[index] = AURA_ID.NULL;
    }

    TickTimer CreateTimer(Aura aura)
    {
        return TickTimer.CreateFromSeconds(Runner, aura.duration);
    }
}
