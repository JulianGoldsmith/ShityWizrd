using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class HybridCharacterController : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private BoneMapper boneMapper;
    public Animator targetAnimator, hybridAnimator;

    [Header("Physics Proxies")]
    [Tooltip("The list of physical bodies and the animation bones they should target.")]
    [SerializeField] private List<TargetedProxy> proxies = new List<TargetedProxy>();

    [Header("Physics Driving Forces")]
    [SerializeField] private float springStrength = 5000f;
    [SerializeField] private float criticalDampingMult = 1f;
    [Header("Physics Driving Rotation")]
    [SerializeField] private float rotationSpringStrength = 5000f;
    [SerializeField] private float rotationCriticalDampingMult = 1f;

    [Header("Root Balancing")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float rideHeight = 1.2f;
    [SerializeField] private float suspensionForce = 500f;
    [SerializeField] private float uprightTorque = 1000f;

    private Rigidbody _rootRigidbody;
    private CapsuleCollider _collider;

    private Vector2 _moveInput;

    [SerializeField] private float moveAcceleration = 200f;
    [SerializeField] private float moveBraking = 100f;
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private float lookWithoutRotateAngle = 80f;

    void Awake()
    {
        _rootRigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<CapsuleCollider>();
        boneMapper = this.GetComponent<BoneMapper>();
        foreach (var proxy in proxies)
        {
            proxy.hybridTransform = targetAnimator.GetComponentsInChildren<Transform>()
                                         .FirstOrDefault(t => t.name == proxy.targetTransform.name);
        }
    }

    void FixedUpdate()
    {
        ApplySuspensionForce();
        ApplyUprightTorque();

        HandlePlayerControl();

        DrivePhysicsProxies();
    }

    private void Update()
    {
        UpdateAnimatior();
    }

    void UpdateAnimatior()
    {
        Vector3 localVelocity = transform.TransformDirection(_rootRigidbody.linearVelocity);

        float forward = -localVelocity.z;
        float right = -localVelocity.x;

        targetAnimator.SetFloat("forwardSpeed", forward / (maxSpeed * 2));
        targetAnimator.SetFloat("rightSpeed", right / (maxSpeed * 2));
        hybridAnimator.SetFloat("forwardSpeed", forward / (maxSpeed * 2));
        hybridAnimator.SetFloat("rightSpeed", right / (maxSpeed * 2));
    }

    private void HandlePlayerControl()
    {
        Quaternion lookRotation = Camera.main.transform.rotation;
        Vector3 lookForward = lookRotation * Vector3.forward;
        lookForward.y = 0;
        lookForward.Normalize();
        Vector3 lookRight = Vector3.Cross(Vector3.up, lookForward);

        Vector3 moveDirection = (lookForward * _moveInput.y + lookRight * _moveInput.x);
        Vector3 targetVelocity = moveDirection * maxSpeed;
        Vector3 currentVelocity = new Vector3(_rootRigidbody.linearVelocity.x, 0, _rootRigidbody.linearVelocity.z);
        Vector3 velocityError = targetVelocity - currentVelocity;

        float forceMagnitude = _moveInput.sqrMagnitude > 0 ? moveAcceleration : moveBraking;
        Vector3 correctiveForce = velocityError * forceMagnitude;
        _rootRigidbody.AddForce(correctiveForce);


        Quaternion targetRotation = Quaternion.LookRotation(lookForward);
        float angleToTarget = Quaternion.Angle(_rootRigidbody.rotation, targetRotation);
        bool isMoving = _moveInput.sqrMagnitude > 0.01f;

        // We rotate if the player is moving OR if they look past the free-look angle.
        if (isMoving || angleToTarget > lookWithoutRotateAngle)
        {
            Quaternion newRotation = Quaternion.Slerp(_rootRigidbody.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed);
            _rootRigidbody.MoveRotation(newRotation);
        }
    }

    private void ApplySuspensionForce()
    {
        // Raycast down to find the ground

        Vector3 bodyCenter = transform.TransformPoint(_collider.center);
        if (Physics.Raycast(bodyCenter, Vector3.down, out RaycastHit hitInfo, rideHeight * 2f, groundLayer))
        {
            // The rest of the logic is the same, but now it's based on a correct raycast.
            float heightError = rideHeight - hitInfo.distance;
            float upwardForce = heightError * suspensionForce;

            float currentVerticalVelocity = Vector3.Dot(_rootRigidbody.linearVelocity, Vector3.up);
            float dampingForce = -currentVerticalVelocity * Mathf.Sqrt(suspensionForce);

            _rootRigidbody.AddForce(Vector3.up * (upwardForce + dampingForce));
        }
    }

    private void ApplyUprightTorque()
    {
        Quaternion targetRotation = Quaternion.FromToRotation(transform.up, Vector3.up) * _rootRigidbody.rotation;
        Quaternion deltaRotation = targetRotation * Quaternion.Inverse(_rootRigidbody.rotation);
        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);

        if (float.IsInfinity(axis.x)) return;

        if (angle > 180f) angle -= 360f;

        Vector3 torque = axis * (angle * Mathf.Deg2Rad) * uprightTorque;
        Vector3 damping = -_rootRigidbody.angularVelocity * Mathf.Sqrt(uprightTorque); // Simplified damping

        _rootRigidbody.AddTorque(torque + damping);
    }

    private void DrivePhysicsProxies()
    {
        foreach (var proxy in proxies)
        {
            //if (proxy.targetTransform == null || proxy.rb == null) continue;

            //pos
            Vector3 targetPosition = proxy.targetTransform.position;
            Vector3 currentPosition = proxy.rb.position;

            Vector3 positionError = targetPosition - currentPosition;
            Vector3 springForce = positionError * springStrength;
            float damping = (2 * (Mathf.Sqrt(springStrength * proxy.rb.mass))) * criticalDampingMult;
            Vector3 dampingForce = -proxy.rb.linearVelocity * damping;

            proxy.rb.AddForce(springForce + dampingForce, ForceMode.Acceleration);


            //rot
            Quaternion targetRotation = proxy.targetTransform.rotation;
            Quaternion currentRotation = proxy.rb.rotation;

            Quaternion deltaRotation = targetRotation * Quaternion.Inverse(currentRotation);

            deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);

            if (angle > 180f)
                angle -= 360f;

            if (float.IsInfinity(axis.x))
                continue;

            Vector3 proportionalTorque = axis * (angle * Mathf.Deg2Rad) * rotationSpringStrength;

            damping = (2 * Mathf.Sqrt(rotationSpringStrength * proxy.rb.inertiaTensor.magnitude));

            Vector3 dampingTorque = -proxy.rb.angularVelocity * damping;

            proxy.rb.AddTorque(proportionalTorque + dampingTorque);
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }
}

[System.Serializable]
public class TargetedProxy
{
    public Rigidbody rb;
    public string targetBoneName;
    public Transform targetTransform;
    public Transform hybridTransform;
}
