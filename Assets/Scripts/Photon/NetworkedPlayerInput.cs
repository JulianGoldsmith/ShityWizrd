using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngine.Windows;

[DefaultExecutionOrder(-10)]
public sealed class NetworkedPlayerInput : NetworkBehaviour, IBeforeUpdate
{
    private NetworkInputData _accumulatedInput;
   // private CharacterCameraController _characterCameraController;

    public override void Spawned()
    {
        if (!HasInputAuthority) return;
        // Register to Fusion input poll callback.
        var networkEvents = Runner.GetComponent<NetworkEvents>();
        networkEvents.OnInput.AddListener(OnInput);

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

        //// Enter key is used for locking/unlocking cursor in game view.
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        //if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
        //{
        //    if (Cursor.lockState == CursorLockMode.Locked)
        //    {
        //        Cursor.lockState = CursorLockMode.None;
        //        Cursor.visible = true;
        //    }
        //    else
        //    {
        //        Cursor.lockState = CursorLockMode.Locked;
        //        Cursor.visible = false;
        //    }
        //}

        //// Accumulate input only if the cursor is locked.
        //if (Cursor.lockState != CursorLockMode.Locked)
        //    return;

        //var mouse = Mouse.current;
        //if (mouse != null)
        //{
        //    var mouseDelta = mouse.delta.ReadValue();

        //    var lookRotationDelta = new Vector2(-mouseDelta.y, mouseDelta.x);
        //    lookRotationDelta *= LookSensitivity / 60f;
        //    _lookRotationAccumulator.Accumulate(lookRotationDelta);

        //    _accumulatedInput.Buttons.Set(EInputButton.Fire, mouse.leftButton.isPressed);
        //}

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

            //float sens = _characterCameraController != null ? _characterCameraController.cameraLookSensitivity : 2f;
            //Vector2 lookInput = PlayerInputController.global_look;

            //float yaw = _accumulatedInput.yawpitch.x + lookInput.x * sens;// * Time.deltaTime;
            //float pitch = _accumulatedInput.yawpitch.y - lookInput.y * sens;// * Time.deltaTime;

            //_accumulatedInput.yawpitch = new Vector2(yaw, pitch);

            //Vector3 camForward = _characterCameraController.transform.forward;
            //Vector3 camRight = _characterCameraController.transform.right;

            //camForward.y = 0;
            //camRight.y = 0;
            //camForward.Normalize();
            //camRight.Normalize();
            //moveDirection = (camForward * moveDirection.z + camRight * moveDirection.x).normalized;

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                GameController.Instance.ToggleSpellEditor();
            }

            Vector2 scroll = Mouse.current?.scroll.ReadValue() ?? Vector2.zero;

            

            if (!GameController.Instance.isEditorActive)
            {
                _accumulatedInput.buttons.Set(EInputButton.LEFT_CLICK, mouse.leftButton.isPressed);
                _accumulatedInput.buttons.Set(EInputButton.RIGHT_CLICK, mouse.rightButton.isPressed);
                _accumulatedInput.buttons.Set(EInputButton.JUMP, keyboard.spaceKey.isPressed);
                _accumulatedInput.buttons.Set(EInputButton.PICKUP, keyboard.eKey.isPressed);
                _accumulatedInput.buttons.Set(EInputButton.DROP, keyboard.qKey.isPressed);
                _accumulatedInput.buttons.Set(EInputButton.SPRINT, keyboard.shiftKey.isPressed);
                _accumulatedInput.buttons.Set(EInputButton.ADD, scroll.y > 0f);
                _accumulatedInput.buttons.Set(EInputButton.SUBTRACT, scroll.y < 0f);
                _accumulatedInput.scroll = scroll.y/5f;
            }

            
        }
        _accumulatedInput.lookRotation = Camera.main.transform.rotation;
    }

    private void OnInput(NetworkRunner runner, NetworkInput networkInput)
    {
        

        // Fusion polls accumulated input. This callback can be executed multiple times in a row if there is a performance spike.
        networkInput.Set(_accumulatedInput);
    }
}
