using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;


public class RagDollCameraController : MonoBehaviour
{
    [SerializeField] private Transform networkedRenderTargetTransform;
    private Transform cameraTransform;       

    [SerializeField] private Vector3 localEyeOffset = new Vector3(0f, 1.55f, 0f);

    [Header("Input")]
    public Vector2 lookInput;
    [SerializeField] private float mouseSensitivity = 0.12f;
    public float lookSmoothing = 0.1f;

    [Header("Pitch Limits")]
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Body Yaw Drive")]
    [SerializeField] private bool useRbMoveRotation = true; 
    [SerializeField] private float maxYawDegreesPerSec = 720f;

    public float targetYaw, targetPitch, finalYaw, yawVel, finalPitch, pitchVel;

    public bool camActive = true;


    public void Spawned()
    {
        cameraTransform = Camera.main.transform;
        Debug.Log("Assigned camera");
    }

    void LateUpdate()
    {
        targetYaw += lookInput.x * mouseSensitivity;
        targetPitch -= lookInput.y * mouseSensitivity;

        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

        finalYaw = Mathf.SmoothDampAngle(finalYaw, targetYaw, ref yawVel, lookSmoothing);
        finalPitch = Mathf.SmoothDampAngle(finalPitch, targetPitch, ref pitchVel, lookSmoothing);

        cameraTransform.rotation = Quaternion.Euler(finalPitch, finalYaw, 0f);
        cameraTransform.position = networkedRenderTargetTransform.transform.position + localEyeOffset;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            camActive = !camActive;
            Cursor.lockState = !camActive ? CursorLockMode.None : CursorLockMode.Locked;
        }
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        if (camActive)
        {
            lookInput = context.ReadValue<Vector2>(); 
        }
    }
}
