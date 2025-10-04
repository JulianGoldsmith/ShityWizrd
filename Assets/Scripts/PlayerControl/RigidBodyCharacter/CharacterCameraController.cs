using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;
using static UnityEngine.Tilemaps.Tilemap;

public class CharacterCameraController : MonoBehaviour
{
    public enum CameraState
    {
        ThirdPerson,
        FirstPerson,
        EditorView
    }

    public CameraState currentState = CameraState.ThirdPerson;

    public Transform editorTarget;
    public Vector3 editorOffset = new Vector3(0, 0, 10);

    public Transform target;
    [Tooltip("Player Eyes position")]
    public Transform firstPersonAnchor;

    [Header("3rd Settings")]
    public Vector3 thirdPersonOffset = new Vector3(0f, 2f, -5f);
    public float thirdPersonSmoothing = 15f;
    public Vector2 thirdPersonPitchClamp = new Vector2(-40f, 80f);

    [Header("FPS Settings")]
    public Vector2 firstPersonPitchClamp = new Vector2(-180f, 180f);
    [SerializeField]
    private float firstPersonSmoothing = 30f;

    public float cameraLookSensitivity = 2.0f;

    [SerializeField] public CharacterAnimationController animationController;
    public CharacterHandsController handController;
    public PlayerInputController inputController;
    public CharacterMovementController characterMovementController;

    public float finalYaw, yawVel, finalPitch, pitchVel;


    [SerializeField] private Vector3 leanDirection = new Vector3(0, -0.5f, 0.5f);
    [SerializeField] private float maxLeanDistance = 0.5f;

    public float smoothedYawFP => finalYaw;

    public void Start()
    {
        if (animationController == null && target != null)
        {
            animationController = target.GetComponentInChildren<CharacterAnimationController>();
        }
        if (animationController != null)
        {
            animationController.ToggleViewChanged(currentState);
        }
        handController = GetComponentInChildren<CharacterHandsController>();
    }

   
    public void OnToggleView(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            currentState = (currentState == CameraState.ThirdPerson) ? CameraState.FirstPerson : CameraState.ThirdPerson;
            if (animationController != null)
            {
                animationController.ToggleViewChanged(currentState);
            }
            if(currentState == CameraState.FirstPerson)
            {
                transform.parent = firstPersonAnchor.transform;
            }
            else
            {
                transform.parent = null;
            }
        }
    }

    private void LateUpdate()
    {
        if (currentState == CameraState.EditorView)
        {
            transform.position = editorTarget.position + editorOffset;
            transform.rotation = Quaternion.LookRotation(Vector3.down);
            //Debug.Log("Running editor view");
        }
        else if (currentState == CameraState.ThirdPerson)
        {
            if (target == null) return;
            ThirdPersonCamera();
        }
        else
        {
            FirstPersonCamera();
            if (handController != null)
            {
                float rbYaw = characterMovementController != null
                    ? characterMovementController.GetComponent<Rigidbody>().rotation.eulerAngles.y
                    : inputController.yaw; // fallback

                handController.DriveFromView(finalPitch, finalYaw, Time.deltaTime);
            }
        }
    }

    private void ThirdPersonCamera()
    {
        inputController.pitch = Mathf.Clamp(inputController.pitch, thirdPersonPitchClamp.x, thirdPersonPitchClamp.y);
        
        Quaternion desiredRotation = Quaternion.Euler(inputController.pitch, inputController.yaw, 0f);
        Vector3 desiredPosition = target.position + desiredRotation * thirdPersonOffset;

        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * thirdPersonSmoothing);
        transform.LookAt(target.position + Vector3.up * thirdPersonOffset.y);
    }

    private void FirstPersonCamera()
    {
        float targetYaw = inputController.yaw;
        float targetPitch = inputController.pitch;

        targetPitch = Mathf.Clamp(targetPitch, firstPersonPitchClamp.x, firstPersonPitchClamp.y);

        finalYaw = Mathf.SmoothDampAngle(finalYaw, targetYaw, ref yawVel, firstPersonSmoothing);
        finalPitch = Mathf.SmoothDampAngle(finalPitch, targetPitch, ref pitchVel, firstPersonSmoothing);


        float leanAmount = Mathf.Clamp01(finalPitch / 90f);


        Vector3 leanOffset = leanDirection.normalized * maxLeanDistance * leanAmount;


        

        transform.rotation = Quaternion.Euler(finalPitch, finalYaw, 0f);
        transform.position = firstPersonAnchor.position + transform.TransformDirection(leanOffset);

    }

    public void SwitchToEditorView()
    {
        currentState = CameraState.EditorView;
    }

    public void SwitchToGameplayView()
    {
        currentState = CameraState.FirstPerson;
        Vector3 angles = transform.eulerAngles;
        finalYaw = angles.y;
        finalPitch = angles.x;
    }
}
