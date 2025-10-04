using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;

[RequireComponent(typeof(Character))]
[RequireComponent(typeof(CharacterMovementController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerInputController : NetworkBehaviour
{
    public CharacterMovementController movementController;
    public CharacterCameraController cameraController;

    public static Vector2 global_look;

    private Vector3 moveInput;
    private Vector2 lookInput;
    

    public float yaw, pitch;

    public override void Spawned()
    {
        movementController = GetComponent<CharacterMovementController>();
        cameraController = FindObjectOfType<CharacterCameraController>();
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            //moveInput = data.direction;
            //lookInput = data.yawpitch;

            //float sens = cameraController != null ? cameraController.cameraLookSensitivity : 2f;
            //yaw += lookInput.x * sens;// * Time.deltaTime;
            //pitch -= lookInput.y * sens;// * Time.deltaTime;

            //Vector3 camForward = cameraController.transform.forward;
            //Vector3 camRight = cameraController.transform.right;

            //camForward.y = 0;
            //camRight.y = 0;
            //camForward.Normalize();
            //camRight.Normalize();
            //Vector3 moveDirection = (camForward * moveInput.z + camRight * moveInput.x).normalized;
            //moveInput = moveDirection;

            moveInput = data.direction;
            lookInput = data.yawpitch;
            yaw = data.yawpitch.x;
            pitch = data.yawpitch.y;
        }        
    }

    private void FixedUpdate()
    {
        //float sens = cameraController != null ? cameraController.cameraLookSensitivity : 2f;
        //yaw += lookInput.x * sens;// * Time.deltaTime;
        //pitch -= lookInput.y * sens;// * Time.deltaTime;
        
        movementController.SetMoveDirection(moveInput);
        movementController.SetTargetYawAndPitch(yaw, pitch);

    }


    public void OnMove(InputAction.CallbackContext context)
    {
        //moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            movementController.Jump();
        }
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        //lookInput = context.ReadValue<Vector2>();
        if (HasInputAuthority)
        {
            lookInput = context.ReadValue<Vector2>();
            global_look = lookInput;
        }
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        movementController.SetSprinting(!movementController.isSprinting);
    }

}