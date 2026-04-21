using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using Fusion;

/// <summary>
/// Main script controlling player casting, ie sword swing, cast spell etc, works with player movement controller and player animation controller
/// </summary>


[RequireComponent(typeof(NetworkedInventoryManager))]
public class PlayerCastActionController : CastActionController
{
    //public Transform cameraTrans;
    [Networked] NetworkButtons prior_buttons { get; set; }
    [Networked] int primary_attack_pressed {  get; set; }
    [Networked] int primary_attack_released { get; set; }
    [Networked] Quaternion lookDirection { get; set; }
    private ChangeDetector _changes;
    

    public override void Spawned()
    {
        base.Spawned();

        _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (inventory == null) inventory = GetComponent<NetworkedInventoryManager>();
      
    }
    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

        if (GetInput(out NetworkInputData data))
        {
            //if (data.buttons.WasPressed(prior_buttons, EInputButton.LEFT_CLICK))
            //{
            //    primary_attack_pressed++;
            //}
            //if (data.buttons.WasReleased(prior_buttons, EInputButton.LEFT_CLICK))
            //{
            //    primary_attack_released++;
            //}
            if (data.buttons.WasPressed(prior_buttons, EInputButton.LEFT_CLICK))
            {
                OnInputEvent(true);
            }
            if (data.buttons.WasReleased(prior_buttons, EInputButton.LEFT_CLICK))
            {
                OnInputEvent(false);
            }
            prior_buttons = data.buttons;
            lookDirection = data.lookRotation;
        }

        //foreach (var change in _changes.DetectChanges(this))
        //{
        //    if (change == nameof(primary_attack_pressed))
        //    {
        //        OnInputEvent(true);
        //    }

        //    if (change == nameof(primary_attack_released))
        //    {
        //        OnInputEvent(false);
        //    }
        //}

        //OnInputTryCast();
    }

    private void OnInputEvent(bool isPress)
    {
        /*if (!HasStateAuthority && Runner.IsResimulation)*/
        //Debug.Log("ReawakenAndPlaceCalledInResim");

        EquipableItem item;

        if (inventory == null || inventory.activeItem == null || !inventory.activeItem.TryGetComponent<EquipableItem> (out item))
            return;

        if (item == null)
            return;

        var actions = item.primaryActions;
        if (actions == null || actions.Count == 0)
            return;

        int comboIndex = primaryComboCounter;
        if (comboTimer <= 0f)
        {
            comboIndex = 0;
            primaryComboCounter = 0;
        }

        if (comboIndex < 0 || comboIndex >= actions.Count)
            comboIndex = 0;

        ItemAction action = actions[comboIndex];
        if (action == null)
            return;

        if (isPress)
        {
            action.OnPress( comboIndex, false);
        }
        else
        {
            action.OnRelease( comboIndex);
        }
    }


    public override Vector3 GetAimTarget()
    {
        RaycastHit hit;

        HybridCharacterController hcc = GetComponent<HybridCharacterController>();
        Vector3 viewpoint = hcc.hipsRb.position + hcc.camController.localEyeOffset + hcc.camController.GetEyePosBasedOnPitch(lookDirection);
        //Physics.Raycast(viewpoint, lookDirection * Vector3.forward, out hit);
        //Ray ray = cameraTrans.GetComponent<Camera>().ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
        //return Physics.Raycast(ray, out RaycastHit hit, 100f) ? hit.point : ray.GetPoint(100f);
        Vector3 fallback = lookDirection * Vector3.forward * 100f + viewpoint;
        return Physics.Raycast(viewpoint, lookDirection * Vector3.forward, out hit) ? hit.point : fallback;
    }

    public override Vector3 GetForward()
    {
        return lookDirection * Vector3.forward;
    }

    public override EyePosAndLookDir GetEyePosAndLookDir()
    {
        var hcc = GetComponent<HybridCharacterController>();
 
        return hcc.GetEyePosAndLookDir();
    }

    #region Hitbox TODO

    public override void ActivateHitbox(int hitBoxID, SpellState state)
    {
        if (inventory.activeItem == null) return;

        EquipableItem item = inventory.activeItem.GetComponent<EquipableItem>();
        if (item == null) return;

        //HitBoxBehaviour hitbox = item.GetHitbox(hitBoxID);

        //if (hitbox != null)
        //{
        //    hitbox.Initialize(this, state);
        //    hitbox.ResetHitBox();
        //    hitbox.EnableHitBox();
        //}
        //else
        //{
        //    Debug.LogError($"Player item {item.name} has no hitbox with ID {hitBoxID}");
        //}
    }

    public override void DeactivateHitbox(int hitBoxID)
    {
        if (inventory.activeItem == null) return;

        EquipableItem item = inventory.activeItem.GetComponent<EquipableItem>();
        if (item == null) return;

        //HitBoxBehaviour hitbox = item.GetHitbox(hitBoxID);

        //if (hitbox != null)
        //{
        //    hitbox.DisableHitBox();
        //    hitbox.Initialize(null, null); // Unlink
        //}
    }


    #endregion


}


