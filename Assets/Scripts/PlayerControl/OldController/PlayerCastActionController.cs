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
    NetworkButtons prior_buttons;
    int primary_attack_pressed { get; set; }
    int primary_attack_released { get; set; }
    Quaternion lookDirection;

    public override void Spawned()
    {
        //if (animator == null) animator = GetComponentInChildren<Animator>();
        if (inventory == null) inventory = GetComponent<NetworkedInventoryManager>();
        //if (cameraTrans == null)
        //{
        //    cameraTrans = Camera.main.transform;
        //}
        //if (animationController != null)
        //{
        //    animationController = GetComponentInChildren<GenericAnimationController>();
        //    animationController.OnAnimationEventTriggered += HandleAnimationEvent;
        //}
    }
    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            if (data.buttons.WasPressed(prior_buttons, EInputButton.LEFT_CLICK))
            {
                primary_attack_pressed++;
            }
            if (data.buttons.WasReleased(prior_buttons, EInputButton.LEFT_CLICK))
            {
                primary_attack_released++;
            }
            prior_buttons = data.buttons;

            lookDirection = data.lookRotation;
        }
    }

    public void FixedUpdate()
    {
        if (primary_attack_pressed > 0)
        {
            if (currentAttackCooldown <= 0)
            {
                primary_attack_pressed = 0;
                Debug.Log("Try start cast");
                StartCast(primary_attack_released > 0);
                if (primary_attack_released > 0)
                    primary_attack_released = 0;
            }
        }
        else if (primary_attack_released > 0)
        {
            primary_attack_released = 0;
            EndCast();
        }
    }


    public void OnPrimaryAttack(InputAction.CallbackContext context)
    {
        // Removing this for now.
        // Deal with it later.

        //switch (context.phase)
        //{
        //    case InputActionPhase.Started:

        //        if (currentAttackCooldown > 0)
        //        {
        //            if (comboTimer > 0)
        //            {
        //                primaryAttackReleaseBuffered = false;
        //                primaryAttackBuffered = true;
                        
        //            }
        //            return; // Do nothing else
        //        }
        //        StartCast(false);
        //        break;

        //    case InputActionPhase.Canceled:

        //        EndCast();
        //        break;
        //}
        
    }
    private void Update()
    {
        if (HasInputAuthority && Keyboard.current.tabKey.wasPressedThisFrame)
            GameController.Instance.ToggleSpellEditor();
    }
    public void OnToggleEditor(InputAction.CallbackContext context)
    {
        //if (context.performed)
        //{
        //    Debug.Log("ContextPerformed");
        //    GameController.Instance.ToggleSpellEditor();
        //}
    }

    public override Vector3 GetAimTarget()
    {
        // This won't work right now.
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

    
}


