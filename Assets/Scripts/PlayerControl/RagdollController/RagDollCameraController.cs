using Fusion;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
public class RagDollCameraController : NetworkBehaviour, IAfterRender
{
    [Header("Tracking Target")]
    [Tooltip("Drag the transform you want to follow here (e.g., Physics Rigidbody, Render Root, or Smoothed Render Root)")]
    public Transform followTarget;

    [Header("Positional Settings")]
    public Vector3 localEyeOffset = new Vector3(0f, 1.55f, 0f);
    public float positionSmoothTime = 0.05f; // Add positional smoothing
    private Vector3 _posVelocity; // Reference for SmoothDamp

    [Header("Input")]
    public Vector2 lookInput;
    public float mouseSensitivity = 0.12f;
    public float lookSmoothing = 0.1f;

    [Header("Pitch Limits")]
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Pitch Vector Offsets")]
    public Vector3 pitchOffset = new Vector3(0.5f, 0.3f, 0f);

    [Header("State (Debug)")]
    public float targetYaw;
    public float targetPitch;
    public float finalYaw;
    public float finalPitch;
    public bool camActive = true;
    public bool isLocalAuthority = false;

    // Internal references
    private float _yawVel;
    private float _pitchVel;
    public Transform cameraTransform;

    public void Spawned(bool _isLocalAuthority)
    {
        isLocalAuthority = _isLocalAuthority;
        if (!isLocalAuthority) return;

        cameraTransform = Camera.main.transform;

        if (followTarget == null)
        {
            Debug.LogWarning("Camera Controller: No follow target assigned! Please assign one in the inspector.");
        }
        else
        {
            // Snap to initial position immediately to avoid a giant swoop at spawn
            cameraTransform.position = GetTargetEyePosition();
        }
    }

    // Explicitly use Fusion's AfterRender
    void IAfterRender.AfterRender()
    {
        UpdateCam();
    }

    void LateUpdate()
    {
       // UpdateCam();
    }

    private void UpdateCam()
    {
        if (!isLocalAuthority || cameraTransform == null || followTarget == null) return;

        // 1. Process Input
        targetYaw += lookInput.x * mouseSensitivity;
        targetPitch -= lookInput.y * mouseSensitivity;
        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

        // Consume input so it doesn't double-apply on frames without mouse movement
        lookInput = Vector2.zero;

        // 2. Smooth Rotation
        finalYaw = Mathf.SmoothDampAngle(finalYaw, targetYaw, ref _yawVel, lookSmoothing);
        finalPitch = Mathf.SmoothDampAngle(finalPitch, targetPitch, ref _pitchVel, lookSmoothing);

        Quaternion targetRotation = Quaternion.Euler(finalPitch, finalYaw, 0f);
        cameraTransform.rotation = targetRotation;

        // 3. Smooth Position
        Vector3 desiredEyePos = GetTargetEyePosition();

        // If smooth time is 0, snap directly (good for testing raw target jitter)
        if (positionSmoothTime <= 0f)
        {
            cameraTransform.position = desiredEyePos;
        }
        else
        {
            cameraTransform.position = Vector3.SmoothDamp(
                cameraTransform.position,
                desiredEyePos,
                ref _posVelocity,
                positionSmoothTime
            );
        }
    }

    private Vector3 GetTargetEyePosition()
    {
        // Get the base position
        Vector3 basePos = followTarget.position + localEyeOffset;

        // Add your custom pitch offset
        float pitchInRadians = finalPitch * Mathf.Deg2Rad;
        float forwardOffset = -Mathf.Sin(pitchInRadians) * pitchOffset.x;
        float downOffset = -(1 - Mathf.Cos(pitchInRadians)) * pitchOffset.y;

        Vector3 eyeOffsetDueToPitch = new Vector3(0, downOffset, forwardOffset);
        Quaternion lookDirOnPlane = Quaternion.Euler(0f, finalYaw, 0f);

        return basePos + (lookDirOnPlane * eyeOffsetDueToPitch);
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
