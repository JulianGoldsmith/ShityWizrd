using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;


public class ActiveRagdollController : NetworkBehaviour
{
    [Header("Networking")]
    public NetworkRunner runner;

    [Header("Grounded movement")]
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private float acceleration = 200f;
    [SerializeField] private float braking = 100f;
    [SerializeField] private float jumpForce = 20f;

    [Header("input")]
    [Networked] public Vector2 moveInput { get; set; }

    [Header("anmatior")]
    public Animator animator;

    [Header("camera")]
    public Transform cameraTransform;

    [Header("perlivs balancing")]
    [SerializeField] private float uprightTorque = 1000f;
    [SerializeField] private float uprightMaxVelocity = 10f;

    [Header("Ground check and ride height")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float rideHeight = 1.0f;
    [SerializeField] private float groundCheckDistance = 1.5f;
    [SerializeField] private float suspensionForce = 500f;
    [SerializeField] private float suspensionDamping = 30f;
    [SerializeField] private bool isGrounded;

    public Transform animatedPelvis;
    private Rigidbody pelvisRigidbody;
    
    [Header("debugs")]
    public bool overrideAllSprings;
    public float sharedSpringValue = 20000f;
    public float sharedDampPercent = 0.1f;
    public AnimationCurve dampingCurve = AnimationCurve.Linear(0, 1, 1, 1);

    public List<ActiveRagdollBone> bones = new List<ActiveRagdollBone>();

    public bool configureJointsInWorldSpce = true;

    [Networked] public Vector3 rbpos { get; set; }
    [Networked] public Vector3 rbVelocity { get; set; }
    [Networked] public Quaternion rbRot { get; set; }

    void Awake()
    {
        InitilizeBones();
        cameraTransform = Camera.main.transform;
 
        //runner.ProvideInput = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            data.direction.Normalize();

            moveInput = new Vector2(data.direction.x, data.direction.z);
        }
        
    }

    void FixedUpdate()
    {
        
        ApplyUprightTorque();
        ApplySuspensionForce();
        UpdateBoneJoints();
       
        HandleGroundedMovement(moveInput);
        
        UpdateAnimatior();
    }

    void HandleGroundedMovement(Vector2 _moveInput)
    {

        if (Runner.IsServer)
        {

            Vector3 camForward = (cameraTransform.forward);
            Vector3 moveDirection = (camForward * _moveInput.y + cameraTransform.right * _moveInput.x).normalized;

            Vector3 targetVelocity = moveDirection * maxSpeed;

            Vector3 currentVelocity = new Vector3(pelvisRigidbody.linearVelocity.x, 0, pelvisRigidbody.linearVelocity.z);

            Vector3 velocityError = targetVelocity - currentVelocity;

            float forceMagnitude = _moveInput.sqrMagnitude > 0 ? acceleration : braking;
            Vector3 correctiveForce = velocityError * forceMagnitude * Runner.DeltaTime;

            pelvisRigidbody.AddForce(correctiveForce, ForceMode.Acceleration);

            rbpos = pelvisRigidbody.transform.position;
            rbVelocity = pelvisRigidbody.linearVelocity;
            rbRot = pelvisRigidbody.rotation;
        }
        else
        {
            pelvisRigidbody.transform.position = rbpos;
            pelvisRigidbody.linearVelocity = rbVelocity ;
            pelvisRigidbody.rotation = rbRot;
        }
    }

    void Jump()
    {
        pelvisRigidbody.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
    }

    void UpdateAnimatior()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(pelvisRigidbody.linearVelocity);

        float forward = localVelocity.z;
        float right = localVelocity.x;

        animator.SetFloat("forwardSpeed", forward / (maxSpeed * 2));
        animator.SetFloat("rightSpeed", right / (maxSpeed * 2));
    }



    public void OnMove(InputAction.CallbackContext context)
    {
        
      
    }

    public void OnJump()
    {
        if (isGrounded)
        {
            Jump();
        }
    }

    #region ragdoll
    void ApplyUprightTorque()
    {
        Quaternion targetRotation = Quaternion.FromToRotation(transform.up, Vector3.up);
        targetRotation.ToAngleAxis(out float angle, out Vector3 axis);
        if (float.IsInfinity(axis.x)) return; //if already there

        Vector3 torque = axis.normalized * (angle * Mathf.Deg2Rad) * uprightTorque;

        torque -= pelvisRigidbody.angularVelocity * (uprightTorque * 0.1f); // Damping proportional to strength

        pelvisRigidbody.AddTorque(torque, ForceMode.Acceleration);
    }

    void ApplySuspensionForce()
    {
        isGrounded = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hitInfo, groundCheckDistance, groundLayer);

        if (isGrounded)
        {
            float heightDiff = rideHeight - hitInfo.distance;
            float upwardForce = heightDiff * suspensionForce;

            float currentVerticalVelocity = Vector3.Dot(pelvisRigidbody.linearVelocity, hitInfo.normal);
            float dampingForce = currentVerticalVelocity * suspensionDamping;

            Vector3 totalForce = (upwardForce - dampingForce) * Vector3.up;
            pelvisRigidbody.AddForce(totalForce, ForceMode.Acceleration);
        }
    }

    void InitilizeBones()
    {
        pelvisRigidbody = GetComponent<Rigidbody>();
        pelvisRigidbody.maxAngularVelocity = uprightMaxVelocity;


        foreach (var bone in bones)
        {
            if (bone.joint == null) continue;

            if (bone.animatedBone == null)
            {
                bone.animatedBone = animatedPelvis.root.GetComponentsInChildren<Transform>()
                                         .FirstOrDefault(t => t.name == bone.joint.name);
            }

            bone.rb = bone.joint.gameObject.GetComponent<Rigidbody>();

            bone.startLocalRotation = bone.joint.transform.localRotation;

            bone.joint.configuredInWorldSpace = configureJointsInWorldSpce;
            bone.joint.rotationDriveMode = RotationDriveMode.Slerp;

            bone.joint.xMotion = ConfigurableJointMotion.Locked;
            bone.joint.yMotion = ConfigurableJointMotion.Locked;
            bone.joint.zMotion = ConfigurableJointMotion.Locked;

            var drive = bone.joint.slerpDrive;
            drive.mode = JointDriveMode.Position;
            drive.positionSpring = overrideAllSprings ? sharedSpringValue : bone.positionSpringForce;
            float criticalDampening = 2f * Mathf.Sqrt(drive.positionSpring * bone.rb.inertiaTensor.magnitude);
            drive.positionDamper = overrideAllSprings ? sharedDampPercent * criticalDampening : bone.criticalDamperMult * criticalDampening;
            drive.maximumForce = Mathf.Infinity;
            bone.joint.slerpDrive = drive;

            var animBindLocal = bone.animatedBone ? bone.animatedBone.localRotation : Quaternion.identity;
            bone.bindCorrection = Quaternion.Inverse(animBindLocal) * bone.startLocalRotation;

            
        }
    }

    void UpdateBoneJoints()
    {
        if (animatedPelvis == null) return;

        foreach (var bone in bones)
        {
            if (bone.joint == null || bone.animatedBone == null) continue;

            

            Quaternion animNowLocal = bone.animatedBone.localRotation;

            Quaternion targetLocal = animNowLocal * bone.bindCorrection;

            bone.joint.SetTargetRotationLocal(targetLocal, bone.startLocalRotation);

            Quaternion currentRotation = bone.joint.transform.localRotation;
            Quaternion error = targetLocal * Quaternion.Inverse(currentRotation);
            error.ToAngleAxis(out float angle, out Vector3 axis);
            float normalizedAngle = Mathf.Abs(angle) / 180f;
            float dynamicDamperMult = dampingCurve.Evaluate(normalizedAngle);

            var drive = bone.joint.slerpDrive;
            drive.positionSpring = overrideAllSprings ? sharedSpringValue : bone.positionSpringForce;

            float criticalDampening = 2f * Mathf.Sqrt(drive.positionSpring * bone.rb.inertiaTensor.magnitude);


            drive.positionDamper = overrideAllSprings ? sharedDampPercent * criticalDampening * dynamicDamperMult : bone.criticalDamperMult * criticalDampening * dynamicDamperMult;
            drive.maximumForce = Mathf.Infinity;
            bone.joint.slerpDrive = drive;
        }
    }


    #endregion
}



[System.Serializable]
public class ActiveRagdollBone
{
    public ConfigurableJoint joint;
    public Transform animatedBone;

    public Rigidbody rb;
    
    public float positionSpringForce = 10000f;
    public float criticalDamperMult = 1f;

    [HideInInspector] public Quaternion startLocalRotation;
    [HideInInspector] public Quaternion bindCorrection;
}

//a configurableJointExtensions class i found online to help with converting configurable joints rotations from local to world space. VEry confusing otherwise! 
public static class ConfigurableJointExtensions
{
    /// <summary>
    /// Sets a joint's targetRotation to match a given local rotation.
    /// The joint transform's local rotation must be cached on Start and passed into this method.
    /// </summary>
    public static void SetTargetRotationLocal(this ConfigurableJoint joint, Quaternion targetLocalRotation, Quaternion startLocalRotation)
    {
        if (joint.configuredInWorldSpace)
        {
            Debug.LogError("SetTargetRotationLocal should not be used with joints that are configured in world space. For world space joints, use SetTargetRotation.", joint);
        }
        SetTargetRotationInternal(joint, targetLocalRotation, startLocalRotation, Space.Self);
    }

    /// <summary>
    /// Sets a joint's targetRotation to match a given world rotation.
    /// The joint transform's world rotation must be cached on Start and passed into this method.
    /// </summary>
    public static void SetTargetRotation(this ConfigurableJoint joint, Quaternion targetWorldRotation, Quaternion startWorldRotation)
    {
        if (!joint.configuredInWorldSpace)
        {
            Debug.LogError("SetTargetRotation must be used with joints that are configured in world space. For local space joints, use SetTargetRotationLocal.", joint);
        }
        SetTargetRotationInternal(joint, targetWorldRotation, startWorldRotation, Space.World);
    }

    static void SetTargetRotationInternal(ConfigurableJoint joint, Quaternion targetRotation, Quaternion startRotation, Space space)
    {
        // Calculate the rotation expressed by the joint's axis and secondary axis
        var right = joint.axis;
        var forward = Vector3.Cross(joint.axis, joint.secondaryAxis).normalized;
        var up = Vector3.Cross(forward, right).normalized;
        Quaternion worldToJointSpace = Quaternion.LookRotation(forward, up);

        // Transform into world space
        Quaternion resultRotation = Quaternion.Inverse(worldToJointSpace);

        // Counter-rotate and apply the new local rotation.
        // Joint space is the inverse of world space, so we need to invert our value
        if (space == Space.World)
        {
            resultRotation *= startRotation * Quaternion.Inverse(targetRotation);
        }
        else
        {
            resultRotation *= Quaternion.Inverse(targetRotation) * startRotation;
        }

        // Transform back into joint space
        resultRotation *= worldToJointSpace;

        // Set target rotation to our newly calculated rotation
        joint.targetRotation = resultRotation;
    }
}