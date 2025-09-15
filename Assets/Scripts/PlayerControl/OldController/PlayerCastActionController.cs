using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Security.Cryptography;

/// <summary>
/// Main script controlling player casting, ie sword swing, cast spell etc, works with player movement controller and player animation controller
/// </summary>


[RequireComponent(typeof(InventoryManager))]
public class PlayerCastActionController : CastActionController
{
    public Transform cameraTrans;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (inventory == null) inventory = GetComponent<InventoryManager>();
        if (cameraTrans == null)
        {
            cameraTrans = Camera.main.transform;
        }
        if (animationController != null)
        {
            animationController = GetComponentInChildren<GenericAnimationController>();
            animationController.OnAnimationEventTriggered += HandleAnimationEvent;
        }

    }

    public void OnPrimaryAttack(InputAction.CallbackContext context)
    {
        
        switch (context.phase)
        {
            case InputActionPhase.Started:

                if (currentAttackCooldown > 0)
                {
                    if (comboTimer > 0)
                    {
                        primaryAttackReleaseBuffered = false;
                        primaryAttackBuffered = true;
                        
                    }
                    return; // Do nothing else
                }
                StartCast(false);
                break;

            case InputActionPhase.Canceled:

                EndCast();
                break;
        }
        
    }

    public void OnToggleEditor(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            GameController.Instance.ToggleSpellEditor();
        }
    }

    public override Vector3 GetAimTarget()
    {
        Ray ray = cameraTrans.GetComponent<Camera>().ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
        return Physics.Raycast(ray, out RaycastHit hit, 100f) ? hit.point : ray.GetPoint(100f);
    }

    public override Vector3 GetForward()
    {
        return cameraTrans.forward;
    }

    
}


