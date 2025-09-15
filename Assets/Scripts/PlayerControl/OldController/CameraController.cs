using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class CameraController : MonoBehaviour
{
    [SerializeField]
    public PlayerMovementController playerMovementController;

    public Transform gameplayTarget;
    public Transform editorTarget;
    public Transform cameraRig;
    public Transform player;



    public Vector3 cameraOffset = new Vector3(0f, 2f, 0f);
    public float lookSensitivity = 1f;
    public Vector2 pitchClamp = new Vector2(-30f, 60f);
    public float camDistance = 5f;
    private Vector2 lookInput;
    private float yaw;
    private float pitch;


    public Transform firstPersonAnchor; 
    public Vector2 firstPersonPitchClamp = new Vector2(-180f, 180f);



    [SerializeField] private float lockOnCamHeightMin = 2.0f;
    [SerializeField] private float lockOnCamHeightMax = 3.5f;
    [SerializeField] private float lockOnCamDistanceMin = 4.0f;
    [SerializeField] private float lockOnCamDistanceMax = 6.0f;
    [SerializeField] private float lockOnDistanceMin = 2.0f;
    [SerializeField] private float lockOnDistanceMax = 8.0f;
    public float cameraLockHeightSmoothTime, cameraLockRotationSmoothTime;
    private float dynamicHeight;
    private float dynamicDistance;
    private float cameraHeightVelocity, cameraDistanceVelocity;
    private float pitchVelocity, yawVelocity;


    public Vector3 editorOffset = new Vector3(0, 0, 10);

    private bool isEditorView = false;
    private Quaternion cameraTargetRotation;
    private bool lastLockOnState;

    public enum CameraMode{ ThirdPerson, FirstPerson }
    [SerializeField] public CameraMode currentMode = CameraMode.FirstPerson;

    private void Start()
    { 
        cameraRig.parent = null;
    }

    void LateUpdate()
    {
        if (isEditorView)
        {
            cameraRig.position = editorTarget.position + editorOffset;
            cameraRig.transform.rotation = Quaternion.LookRotation(Vector3.down);
        }
        else
        {
            if (currentMode == CameraMode.ThirdPerson)
            {
                ThirdPersonCameraMovement();
            }
            else 
            {
                FirstPersonCameraMovement();
            }
        }
        
    }

    public void SwitchToEditorView()
    {
        isEditorView = true;
    }

    public void SwitchToGameplayView()
    {
        isEditorView = false;
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    public void ThirdPersonCameraMovement()
    {
        if (playerMovementController.isLockedOn && playerMovementController.currentTarget)
        {
            Vector3 toTarget = playerMovementController.currentTarget.position - player.position;
            toTarget.y = 0f;

            float distance = toTarget.magnitude;
            float t = Mathf.InverseLerp(lockOnDistanceMin, lockOnDistanceMax, distance);

            float targetHeight = Mathf.Lerp(lockOnCamHeightMax, lockOnCamHeightMin, t);
            float targetDistance = Mathf.Lerp(lockOnCamDistanceMax, lockOnCamDistanceMin, t);

            dynamicHeight = Mathf.SmoothDamp(dynamicHeight, targetHeight, ref cameraHeightVelocity, cameraLockHeightSmoothTime);
            dynamicDistance = Mathf.SmoothDamp(dynamicDistance, targetDistance, ref cameraDistanceVelocity, cameraLockHeightSmoothTime);

            Vector3 lookDir = (playerMovementController.currentTarget.position) - cameraRig.position;
            Quaternion desiredRotation = Quaternion.LookRotation(lookDir.normalized);


            Vector3 desiredEuler = desiredRotation.eulerAngles;

            pitch = Mathf.SmoothDampAngle(pitch, desiredEuler.x, ref pitchVelocity, cameraLockRotationSmoothTime);
            yaw = Mathf.SmoothDampAngle(yaw, desiredEuler.y, ref yawVelocity, cameraLockRotationSmoothTime);

            cameraTargetRotation = Quaternion.Euler(pitch, yaw, 0f);

            Vector3 basePosition = player.position - cameraTargetRotation * Vector3.forward * dynamicDistance;
            Vector3 offsetPosition = basePosition + cameraRig.transform.right * cameraOffset.x + (new Vector3(0, dynamicHeight, 0)) + cameraRig.transform.forward * cameraOffset.z;

            cameraRig.position = offsetPosition;
            cameraRig.rotation = cameraTargetRotation;
        }
        else
        {
            if (playerMovementController.lastLockOnState)
            {
                Vector3 angles = cameraRig.eulerAngles;
                yaw = angles.y;
            }

            yaw += lookInput.x * lookSensitivity;
            pitch -= lookInput.y * lookSensitivity;
            pitch = Mathf.Clamp(pitch, pitchClamp.x, pitchClamp.y);

            cameraTargetRotation = Quaternion.Euler(pitch, yaw, 0f);

            dynamicHeight = Mathf.SmoothDamp(dynamicHeight, cameraOffset.y, ref cameraHeightVelocity, cameraLockHeightSmoothTime);
            dynamicDistance = Mathf.SmoothDamp(dynamicDistance, camDistance, ref cameraDistanceVelocity, cameraLockHeightSmoothTime);

            Vector3 basePosition = player.position - cameraTargetRotation * Vector3.forward * dynamicDistance;
            Vector3 offsetPosition = basePosition + cameraRig.transform.right * cameraOffset.x + (new Vector3(0, dynamicHeight, 0)) + cameraRig.transform.forward * cameraOffset.z;

            cameraRig.position = offsetPosition;
            cameraRig.rotation = cameraTargetRotation;
        }

        playerMovementController.lastLockOnState = playerMovementController.isLockedOn;
    }

    public void FirstPersonCameraMovement()
    {
        yaw += lookInput.x * lookSensitivity;
        pitch -= lookInput.y * lookSensitivity;
        pitch = Mathf.Clamp(pitch, firstPersonPitchClamp.x, firstPersonPitchClamp.y);

        cameraRig.position = firstPersonAnchor.position;

        cameraRig.rotation = Quaternion.Euler(pitch, yaw, 0f);

        playerMovementController.transform.rotation = Quaternion.Euler(0, yaw, 0);
    }

    public void OnToggleCameraView(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (currentMode == CameraMode.ThirdPerson)
            {
                currentMode = CameraMode.FirstPerson;
                yaw = player.eulerAngles.y;
                pitch = 0f; 
            }
            else
            {
                currentMode = CameraMode.ThirdPerson;
            }
        }
    }

    public void OnLook(InputAction.CallbackContext context)
    {

        lookInput = context.ReadValue<Vector2>();
    }
}