using Fusion;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.InputSystem;


public class RagDollCameraController : MonoBehaviour
{
    [SerializeField] private Transform networkedRenderTargetTransform;
    private Transform cameraTransform;       

    public Vector3 localEyeOffset = new Vector3(0f, 1.55f, 0f);

    [Header("Input")]
    public Vector2 lookInput;
    [SerializeField] public float mouseSensitivity = 0.12f;
    public float lookSmoothing = 0.1f;

    [Header("Pitch Limits")]
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Body Yaw Drive")]
    [SerializeField] private bool useRbMoveRotation = true; 
    [SerializeField] private float maxYawDegreesPerSec = 720f;

    [Header("Pitch Vector Offsets")]
    public Vector3 pitchOffset = new Vector3(0.5f, 0.3f, 0f); 

    public float targetYaw, targetPitch, finalYaw, yawVel, finalPitch, pitchVel;

    public bool camActive = true;

    public bool isLocalAuthority = false;


    public void Spawned(bool _isLocalAuthority)
    {
        isLocalAuthority = _isLocalAuthority;
        if (!isLocalAuthority) return;
        cameraTransform = Camera.main.transform;
        Debug.Log("Assigned camera");
    }

    void LateUpdate()
    {
        if (!isLocalAuthority) return;
        targetYaw += lookInput.x * mouseSensitivity;
        targetPitch -= lookInput.y * mouseSensitivity;

        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

        finalYaw = Mathf.SmoothDampAngle(finalYaw, targetYaw, ref yawVel, lookSmoothing);
        finalPitch = Mathf.SmoothDampAngle(finalPitch, targetPitch, ref pitchVel, lookSmoothing);


        

        cameraTransform.rotation = Quaternion.Euler(finalPitch, finalYaw, 0f);

        Vector3 eyeOffsetDueToPitch = GetEyePosBasedOnPitch(cameraTransform.rotation);


        cameraTransform.position = networkedRenderTargetTransform.transform.position + localEyeOffset + eyeOffsetDueToPitch;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            camActive = !camActive;
            Cursor.lockState = !camActive ? CursorLockMode.None : CursorLockMode.Locked;
        }
    }

    public Vector3 GetEyePosBasedOnPitch(Quaternion _lookRot)
    {
        float pitchInRadians = _lookRot.eulerAngles.x * Mathf.Deg2Rad;
        float forwardOffset = -Mathf.Sin(pitchInRadians) * pitchOffset.x;
        float downOffset = -(1 - Mathf.Cos(pitchInRadians)) * pitchOffset.y;

        Vector3 eyeOffsetDueToPitch = new Vector3(0, downOffset, forwardOffset);

        Quaternion lookDirOnPlane = Quaternion.Euler(0f, _lookRot.eulerAngles.y, 0f);

        return lookDirOnPlane * eyeOffsetDueToPitch;
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        if (!isLocalAuthority) return;
        if (camActive)
        {
            lookInput = context.ReadValue<Vector2>(); 
        }
    }
}
