using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngine.Windows;
using System.Collections.Generic;

[DefaultExecutionOrder(-10)]
public sealed class NetworkedPlayerInput : NetworkBehaviour, IBeforeUpdate
{
    private NetworkInputData _accumulatedInput;
    // private CharacterCameraController _characterCameraController;
    private NetworkId _localGrabItemId;
    private float _localGrabTargetDistance;
    private Quaternion _localGrabRotationOffset = Quaternion.identity;

    private RuneRigSpawnController _runeRigSpawnController;
    private RuneRigPlacementController _runeRigPlacementController;

    public override void Spawned()
    {
        if (!HasInputAuthority) return;
        // Register to Fusion input poll callback.
        var networkEvents = Runner.GetComponent<NetworkEvents>();
        networkEvents.OnInput.AddListener(OnInput);
        _runeRigSpawnController = GetComponent<RuneRigSpawnController>();
        _runeRigPlacementController = GetComponent<RuneRigPlacementController>();

        GameController.Instance.playerInput = GetComponent<PlayerInput>();

        //_characterCameraController = Camera.main.GetComponent<CharacterCameraController>();

        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (runner == null)
            return;

        var networkEvents = runner.GetComponent<NetworkEvents>();
        if (networkEvents != null)
        {
            networkEvents.OnInput.RemoveListener(OnInput);
        }
    }

    void IBeforeUpdate.BeforeUpdate()
    {
        // This method is called BEFORE ANY FixedUpdateNetwork() and is used to accumulate input from Keyboard/Mouse.
        // Input accumulation is mandatory - this method is called multiple times before new forward FixedUpdateNetwork() - common if rendering speed is faster than Fusion simulation.

        if (HasInputAuthority == false)
            return;

        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        if (keyboard != null)
        {
            Vector3 moveDirection = Vector3.zero;
            if (Keyboard.current.wKey.isPressed)
                moveDirection += Vector3.forward;

            if (Keyboard.current.sKey.isPressed)
                moveDirection += Vector3.back;

            if (Keyboard.current.aKey.isPressed)
                moveDirection += Vector3.left;

            if (Keyboard.current.dKey.isPressed)
                moveDirection += Vector3.right;

            moveDirection = moveDirection.normalized;
            _accumulatedInput.direction = moveDirection;


            if (keyboard.tabKey.wasPressedThisFrame)
            {
                GameController.Instance.ToggleSpellEditor();
            }

            //Vector2 scroll = Mouse.current?.scroll.ReadValue() ?? Vector2.zero;

            

            if (!GameController.Instance.isEditorActive)
            {
                _accumulatedInput.buttons.Set(EInputButton.LEFT_CLICK, mouse.leftButton.isPressed);
                _accumulatedInput.buttons.Set(EInputButton.RIGHT_CLICK, mouse.rightButton.isPressed);
                _accumulatedInput.buttons.Set(EInputButton.JUMP, keyboard.spaceKey.isPressed);
                _accumulatedInput.buttons.Set(EInputButton.PICKUP, keyboard.eKey.isPressed);
                _accumulatedInput.buttons.Set(EInputButton.DROP, keyboard.qKey.isPressed);
                _accumulatedInput.buttons.Set(EInputButton.SPRINT, keyboard.shiftKey.isPressed);
                _accumulatedInput.buttons.Set(EInputButton.LEVITATE, keyboard.rKey.isPressed);
                _accumulatedInput.buttons.Set(EInputButton.SELF_BONK, keyboard.tKey.isPressed);
                _accumulatedInput.buttons.Set(EInputButton.UN_SELF_BONK, keyboard.fKey.isPressed);
                _accumulatedInput.buttons.Set(EInputButton.TEST_COUNT, keyboard.cKey.isPressed);

               // _accumulatedInput.scroll = scroll.y/5f;
            }

        }

        NetworkObject playerObject = Runner.GetPlayerObject(Runner.LocalPlayer);
        if (!GameController.Instance.isEditorActive &&
            playerObject != null &&
            playerObject.TryGetComponent(out NetworkedHandsController hands) &&
            playerObject.TryGetComponent(out HybridCharacterController controller) &&
            playerObject.TryGetComponent(out NetworkedInventoryManager inventory) &&
            hands.RightHandMode == TargetingMode.DRAGG &&
            inventory.currentItemInHand != null)
        {

            NetworkId heldItemId = inventory.currentItemInHand.Id;

            if (_localGrabItemId != heldItemId)
            {
                _localGrabItemId = heldItemId;

                if (controller.GrabControlItemId == heldItemId) {
                    _localGrabTargetDistance = controller.GrabTargetDistance;
                    _localGrabRotationOffset = controller.GrabRotationOffset;
                }
                else {
                    _localGrabTargetDistance = Vector3.Distance(controller.GetEyePos(), inventory.currentItemInHand.transform.position);
                    _localGrabRotationOffset = Quaternion.identity;
                }
            }

            float scrollDelta = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            _localGrabTargetDistance += scrollDelta * controller.grabScrollSensitivity;
            _localGrabTargetDistance = Mathf.Clamp(_localGrabTargetDistance, controller.minGrabDistance, controller.maxGrabDistance);

            bool rotatingGrab = mouse != null && mouse.middleButton.isPressed;
            bool rollingGrab = rotatingGrab && keyboard != null && keyboard.shiftKey.isPressed;
            controller.camController.grabRotationActive = rotatingGrab;

            if (rotatingGrab)
            {
                Vector2 mouseDelta = mouse.delta.ReadValue();

                Vector3 horizontalRotationAxis = rollingGrab ? Vector3.forward : Vector3.up;

                Quaternion horizontalDelta = Quaternion.AngleAxis(mouseDelta.x * controller.grabRotationSensitivity, horizontalRotationAxis);
                Quaternion verticalDelta = Quaternion.AngleAxis(-mouseDelta.y * controller.grabRotationSensitivity, Vector3.right);

                _localGrabRotationOffset = Quaternion.Normalize(horizontalDelta * verticalDelta * _localGrabRotationOffset);

                if (rollingGrab)
                    _accumulatedInput.buttons.Set(EInputButton.SPRINT, false);
            }

            _accumulatedInput.grabControlItemId = heldItemId;
            _accumulatedInput.grabTargetDistance = _localGrabTargetDistance;
            _accumulatedInput.grabRotationOffset = _localGrabRotationOffset;
        }
        else
        {
            if (playerObject != null && playerObject.TryGetComponent(out HybridCharacterController inactiveController))
                inactiveController.camController.grabRotationActive = false;

            _localGrabItemId = default;
            _localGrabTargetDistance = 0f;
            _localGrabRotationOffset = Quaternion.identity;
            _accumulatedInput.grabControlItemId = default;
            _accumulatedInput.grabTargetDistance = 0f;
            _accumulatedInput.grabRotationOffset = Quaternion.identity;
        }

        _accumulatedInput.lookRotation = Camera.main.transform.rotation;
    }

    private void OnInput(NetworkRunner runner, NetworkInput networkInput)
    {
        _accumulatedInput.interactionTarget = default;

        var playerObj = runner.GetPlayerObject(runner.LocalPlayer);
        if (playerObj != null &&
            playerObj.TryGetComponent(out NetworkedHandsController hands) &&
            playerObj.TryGetComponent(out HybridCharacterController controller) &&
            playerObj.TryGetComponent(out NetworkedInventoryManager inv))
        {
            if (hands.RightHandMode == TargetingMode.DRAGG && inv.currentItemInHand != null)
            {
                //Debug.Log("sending drag Input");
                
                Vector3 eyePos = controller.GetEyePos();
                Quaternion lookRot = controller.GetLookRot();

                float pitch = lookRot.eulerAngles.x;
                if (pitch > 180f) pitch -= 360f;
                pitch = -pitch;
                float pitch01 = (pitch + 90f) / 180f;

                float addedHeight = hands.dragPitchToHeightModifierCurve.Evaluate(pitch01);

                Vector3 offset = hands.dragTargetOffset + new Vector3(0f, addedHeight, hands.DragDistance);

                Vector3 targetPos = eyePos + (lookRot * offset);
                _accumulatedInput.dragTargetPos = targetPos;

                // FACING: aim from the item COM to the eye (same as your server logic conceptually)
                var itemNO = inv.currentItemInHand;
                var item = itemNO.GetComponent<DraggableItem>();
                Vector3 com = (item != null && item.rb != null) ? item.rb.worldCenterOfMass : itemNO.transform.position;

                Vector3 facing = (eyePos - com);
                _accumulatedInput.dragFacingDir = facing.sqrMagnitude > 1e-6f ? facing.normalized : Vector3.forward;
            }
            else
            {
                _accumulatedInput.dragTargetPos = Vector3.zero;
                _accumulatedInput.dragFacingDir = Vector3.zero;
            }

            if (_runeRigPlacementController != null && _runeRigPlacementController.TryGetAttachmentTarget(out NetworkInteractionTarget runePlacementTarget))
            {
                _accumulatedInput.interactionTarget = runePlacementTarget;
            }
            else if (inv.TryGetLookedAtInteractionTarget(out NetworkInteractionTarget interactionTarget))
            {
                _accumulatedInput.interactionTarget = interactionTarget;
            }

            // Debug.Log($"drag pos {_accumulatedInput.dragTargetPos} drag rot {_accumulatedInput.dragFacingDir}");
        }

        // Fusion polls accumulated input. This callback can be executed multiple times in a row if there is a performance spike.
        _accumulatedInput.runeRigSpawnCommand = 0u;

        if (_runeRigSpawnController != null && _runeRigSpawnController.TryGetNextSpawnCommand(out uint command))
            _accumulatedInput.runeRigSpawnCommand = command;

        networkInput.Set(_accumulatedInput);

        _accumulatedInput.runeRigSpawnCommand = 0u;
    }
}
