using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Windows;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovementController : MonoBehaviour
{
    public CameraController mainCameraController;

    public float walkSpeed = 5f;
    public const float animationReferenceWalkSpeed = 5;
    public float sprintSpeed = 10f;
    public float rotationSpeed = 10f;
    public bool isSprinting = false;
    public Vector3 targetDirection;
    public Vector3 animatorVector;

    public float acceleration = 10f;  
    public float deceleration = 10f;  

    public float currentSpeed = 0f;   
    private float targetSpeed = 0f;    

    public float smoothInputTime = 0.1f;
    private Vector2 moveInput, lastMoveInput, smoothMoveInputRef;

    public bool lastLockOnState;

    private CharacterController controller;

    public float jumpHeight = 2f;          
    private bool isJumping = false;

    public float groundedGraceTime = 0.15f;
    private float groundedTimer = 0f;
    private float originalStepOffset;
    public bool IsGroundedStable => controller.isGrounded || groundedTimer > 0f;

    public Animator animator;
    public Transform childModel;

    public bool isLockedOn = false;
    public float lockOnRadius = 15f;
    public float lockOnAngle = 60f;
    public LayerMask targetMask;
    public Transform currentTarget;

    public float gravity = -9.81f;
    public float groundedGravity = -2f;
    public Vector3 velocity;

    public string rHandSlotNameOnRig = "mixamorig:R Item";

    private PlayerAnimationController animatorRootMotionScript;
    private InventoryManager inventoryManager;
    private PlayerCastActionController playerActionController;


    public enum PlayerMovementState
    {
        FreeMovement,
        LockedOn,
        Casting,
        Dodging
    }
    public PlayerMovementState currentState;

    bool IsRootMotionDriving => playerActionController.isCasting && !playerActionController.isUpperBodyAction;

    void Awake()
    {
        AssignComponentsInEditor();
        originalStepOffset = controller.stepOffset;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        PlayerMovement();
    }


    #region input system OnCalls



    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
        if (context.canceled)
        {
            moveInput = Vector2.zero;
        }
    }


    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Jump();
        }
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            isSprinting = !isSprinting; 
           
        }
    }

    public void OnLockOnToggle(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (isLockedOn)
            {
                UnlockTarget();
            }
            else
            {
                FindLockOnTarget();
            }
        }
    }

    #endregion


    void FindLockOnTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, lockOnRadius, targetMask);
        Transform bestTarget = null;
        float bestDot = -1f;

        foreach (Collider hit in hits)
        {
            Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;
            float dot = Vector3.Dot(mainCameraController.cameraRig.forward, dirToTarget);

            if (dot > Mathf.Cos(lockOnAngle * 0.5f * Mathf.Deg2Rad))
            {
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (bestTarget == null || dist < Vector3.Distance(transform.position, bestTarget.position))
                {
                    bestTarget = hit.transform;
                    bestDot = dot;
                }
            }
        }

        if (bestTarget)
        {
            currentTarget = bestTarget;
            isLockedOn = true;
            Debug.Log($"Locked onto: {currentTarget.name}");
        }
        else
        {
            Debug.Log("No valid target");
        }
    }


    void UnlockTarget()
    {
        isLockedOn = false;
        currentTarget = null;
        Debug.Log("Unlocked");
    }
    

    void Jump()
    {
        if (controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            isJumping = true;
            animator.SetTrigger("Jump");  // Add this line!
        }
    }

    void HandleGravity()
    {
        if (controller.isGrounded)
        {
            groundedTimer = groundedGraceTime;
        }
        else 
        {
            groundedTimer = Mathf.Max(0f, groundedTimer - Time.deltaTime); 
        }

        if (IsGroundedStable)
        {
            if (velocity.y < 0f) velocity.y = groundedGravity; // small stick force
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }
        controller.stepOffset = controller.isGrounded ? originalStepOffset : 0f;
    }

    void PlayerMovement()
    {
        //Smooth from last input to current
        lastMoveInput = Vector2.SmoothDamp(lastMoveInput, moveInput, ref smoothMoveInputRef, smoothInputTime);
        Vector3 input = new Vector3(lastMoveInput.x, 0f, lastMoveInput.y);
        float inputMagnitude = Mathf.Clamp01(input.magnitude); 


        //cam relative direction
        Vector3 camForward = mainCameraController.cameraRig.forward;
        Vector3 camRight = mainCameraController.cameraRig.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        //Get input direction relative to our camera. 
        Vector3 inputDirection = camForward * input.z + camRight * input.x;
        inputDirection.Normalize();

        HandleGravity();

        //speed from walk to run
        if (inputMagnitude < 0.1f) isSprinting = false;

        targetSpeed = (inputMagnitude > 0.1f) ? (isSprinting ? sprintSpeed : walkSpeed) : 0f;

        if (currentSpeed < targetSpeed)
        {
            currentSpeed += acceleration * Time.deltaTime;
            if (currentSpeed > targetSpeed) currentSpeed = targetSpeed;
        }
        else if (currentSpeed > targetSpeed)
        {
            currentSpeed -= deceleration * Time.deltaTime;
            if (currentSpeed < targetSpeed) currentSpeed = targetSpeed;
        }

        Vector3 moveDirection = Vector3.zero;

        bool allowMovement = true;
        if (playerActionController.isCasting && !playerActionController.isUpperBodyAction)
        {
            allowMovement = false;
        }

        
        if (((isLockedOn && currentTarget) || (playerActionController.isCasting && playerActionController.isUpperBodyAction)) && !isSprinting)
        {
            moveDirection = StrafeAndFaceTargetMovement(moveDirection, camForward, input);
        }
        else
        {
            moveDirection = FreeMovement(moveDirection, input, inputDirection);            
        }


        // Combine horizontal and vertical movement

        Vector3 finalMove = moveDirection * currentSpeed + Vector3.up * velocity.y;
        if (!IsRootMotionDriving)
        {
            controller.Move(finalMove * Time.deltaTime);
            // used by animation params
            animatorVector = finalMove;
        }
        else
        {
            moveDirection = Vector3.zero;
        }
       



        //Debug.Log("TargetSpeed " + targetSpeed + " current speed " + currentSpeed + " input mag " + inputMagnitude + "input x" + input.x + "input y" + input.y);
    }

    private Vector3 StrafeAndFaceTargetMovement(Vector3 moveDirection, Vector3 camForward, Vector3 input)
    {
        Vector3 dirToTarget = Vector3.zero;
        if (!isLockedOn || currentTarget == null)
        {
            dirToTarget = (CalculateCameraLookPoint() - transform.position);
        }
        else
        {
            dirToTarget = (currentTarget.position - transform.position);
        }

        dirToTarget.y = 0f;
        dirToTarget.Normalize();

        Vector3 forward = dirToTarget;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        moveDirection = (forward * input.z + right * input.x).normalized;

        Quaternion targetRotation = Quaternion.LookRotation(dirToTarget);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

        return moveDirection;
    }
    Vector3 FreeMovement(Vector3 moveDirection, Vector3 input, Vector3 inputDirection)
    {
        if (mainCameraController.currentMode == CameraController.CameraMode.FirstPerson)
        {
            moveDirection = inputDirection;
            return moveDirection;
        }
        // Rotate in movement direction
        if (Mathf.Clamp01(input.magnitude) > 0.1f)
        {
            Quaternion toRotation = Quaternion.LookRotation(inputDirection, Vector3.up);
            transform.rotation = toRotation;
        }

        moveDirection = transform.forward;
        childModel.rotation = transform.rotation;
        return moveDirection;
    }
    public void AssignComponentsInEditor()
    {
        animator = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();

        animatorRootMotionScript = GetComponentInChildren<PlayerAnimationController>();
        childModel = animatorRootMotionScript.gameObject.transform;


        inventoryManager = GetComponent<InventoryManager>();
        inventoryManager.itemSocketR = GetComponentsInChildren<Transform>(true)
                 .FirstOrDefault(t => t.name == rHandSlotNameOnRig);

        playerActionController = GetComponent<PlayerCastActionController>();
        //playerActionController.animator = animator;
        //playerActionController.animationController = animatorRootMotionScript;
        //playerActionController.inventory = inventoryManager;
    }

    public Vector3 CalculateCameraLookPoint()
    {
        Vector3 point = Vector3.zero;
        Transform cam = Camera.main.gameObject.transform;

        point = cam.position + cam.forward * 100f;
        return point;
    }
}
