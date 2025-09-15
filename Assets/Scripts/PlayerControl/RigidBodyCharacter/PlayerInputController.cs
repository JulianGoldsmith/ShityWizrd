using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Character))]
[RequireComponent(typeof(CharacterMovementController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerInputController : MonoBehaviour
{
    public CharacterMovementController movementController;
    public CharacterCameraController cameraController;
 

    private Vector2 moveInput;
    private Vector2 lookInput;

    public float yaw, pitch;

    private void Awake()
    {
        movementController = GetComponent<CharacterMovementController>();
        cameraController = FindObjectOfType<CharacterCameraController>();
    }

    private void Update()
    {

        float sens = cameraController != null ? cameraController.cameraLookSensitivity : 2f;
        yaw += lookInput.x * sens;// * Time.deltaTime;
        pitch -= lookInput.y * sens;// * Time.deltaTime;

        Vector3 camForward = cameraController.transform.forward;
        Vector3 camRight = cameraController.transform.right;

  
        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();
        Vector3 moveDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        movementController.SetMoveDirection(moveDirection);
        movementController.SetTargetYawAndPitch(yaw, pitch);
    }


    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
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
        lookInput = context.ReadValue<Vector2>();
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        movementController.SetSprinting(!movementController.isSprinting);
    }

}