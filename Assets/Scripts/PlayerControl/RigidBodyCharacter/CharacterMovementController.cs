using UnityEngine;

public class CharacterMovementController : MonoBehaviour
{
    private Character character;

    [Header("Movement settings")]
    [SerializeField] public float walkSpeed = 4f, sprintSpeed = 8f;
    [SerializeField] private float acceleration = 100f;
    [SerializeField] private float maxSlopeAngle = 45f;
    public bool isSprinting = false;

    [Header("Grounding settings")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float rideHeight = 1.8f;
    [SerializeField] private float groundCheckDistance = 1.2f;
    [SerializeField] private float upwardRideSpring = 200f, downwardRideSpring = 20;
    [SerializeField] private float springDamper = 20f;

    [Header("Upright settings")]
    [SerializeField] private float uprightStrength = 100f;

    [SerializeField] private float jumpForce = 15f;

    private Vector3 moveInputDirection;
    private Vector3 groundNormal;
    private bool shouldJump = false;

    private float targetYaw, targetPitch;
    public float rotationSmoothing = 25f;



    public CharacterCameraController cameraController;
    private void Awake()
    {
        character = GetComponent<Character>();
        cameraController = Camera.main.GetComponent<CharacterCameraController>();
    }



    private void FixedUpdate()
    {
        HandleGrounding();
        HandleMovement();
        HandleRotation();
    }

    public void SetTargetYawAndPitch(float yaw, float pitch)
    {
        targetYaw = yaw;
        targetPitch = pitch;
    }

    public void SetSprinting(bool sprinting)
    {
        isSprinting = sprinting;
    }

    public void SetMoveDirection(Vector3 direction)
    {
        moveInputDirection = direction;
    }

    public void Jump()
    {
        if (character.IsGrounded)
        {
            shouldJump = true;
        }
    }

    private void HandleGrounding()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, rideHeight * groundCheckDistance, groundLayer))
        {
            character.IsGrounded = true;
            groundNormal = hit.normal;

            float currentHeight = hit.distance;
            float deltaToTargetHeight = rideHeight - currentHeight;

            float currentSpringStrength = deltaToTargetHeight > 0 ? upwardRideSpring : downwardRideSpring;
            float upwardVelocity = Vector3.Dot(character.Rigidbody.linearVelocity, Vector3.up);

            float springForce = (deltaToTargetHeight * currentSpringStrength) - (upwardVelocity * springDamper);

            character.Rigidbody.AddForce(Vector3.up * springForce);
        }
        else
        {
            character.IsGrounded = false;
            groundNormal = Vector3.up;
        }
    }

    private void HandleMovement()
    {
    
        Vector3 projectedMoveDirection = Vector3.ProjectOnPlane(moveInputDirection, groundNormal).normalized;

        float moveSpeed = isSprinting ? sprintSpeed : walkSpeed;

        Vector3 targetVelocity = projectedMoveDirection * moveSpeed;

        Vector3 currentHorizontalVelocity = Vector3.ProjectOnPlane(character.Rigidbody.linearVelocity, groundNormal);

        Vector3 velocityError = targetVelocity - currentHorizontalVelocity;

        Vector3 force = Vector3.ProjectOnPlane(velocityError * acceleration, groundNormal);
        character.Rigidbody.AddForce(force, ForceMode.Acceleration);


        if (shouldJump)
        {
            character.Rigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            shouldJump = false;
        }
    }

    private void HandleRotation()
    {

        Vector3 rbUp = character.Rigidbody.rotation * Vector3.up;
        Quaternion toUp = Quaternion.FromToRotation(rbUp, Vector3.up);
        toUp.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        character.Rigidbody.AddTorque(axis.normalized * (Mathf.Deg2Rad * angleDeg) * uprightStrength, ForceMode.Acceleration);

        Vector3 desiredFwd = Quaternion.Euler(0f, targetYaw, 0f) * Vector3.forward;
        desiredFwd = Vector3.ProjectOnPlane(desiredFwd, rbUp).normalized;
        Quaternion targetRot = Quaternion.LookRotation(desiredFwd, rbUp);

        character.Rigidbody.MoveRotation(targetRot);
        /*
        Quaternion uprightError = Quaternion.FromToRotation(transform.up, Vector3.up);
        uprightError.ToAngleAxis(out float angleInDegrees, out Vector3 rotationAxis);
        if (angleInDegrees > 180f) angleInDegrees -= 360f;

        Vector3 uprightTorque = rotationAxis.normalized * (angleInDegrees * Mathf.Deg2Rad * uprightStrength);
        character.Rigidbody.AddTorque(uprightTorque, ForceMode.Acceleration);

        // --- 2. Turning Torque (Aims the character) ---
        Quaternion targetTurnRotation = Quaternion.Euler(0, targetYaw, 0);
        Quaternion turnError = targetTurnRotation * Quaternion.Inverse(character.Rigidbody.rotation);
        turnError.ToAngleAxis(out angleInDegrees, out rotationAxis);
        if (angleInDegrees > 180f) angleInDegrees -= 360f;

        Vector3 turningTorque = rotationAxis.normalized * (angleInDegrees * Mathf.Deg2Rad * rotationSmoothing); // Use rotationSmoothing as the spring strength
        character.Rigidbody.AddTorque(turningTorque, ForceMode.Acceleration);*/
    }

}
