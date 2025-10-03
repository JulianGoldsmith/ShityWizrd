using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class LocalActiveRagDoll : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private Rigidbody ragdollHipsRigidbody;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform animatedPelvisRoot;
    public NetworkedRagdollController characterController;
    


    [Header("Hips Follow PD Controller")]
    [SerializeField] private Vector3 targetHipsOffset = new Vector3(0, -0.2f, 0); // Offset from root to where hips should be
    [SerializeField] private float hipsPositionSpring = 8000f;  // Proportional force (the "spring")
    [SerializeField] private float hipsPositionDamper = 300f;   // Derivative force (the "damper")
    [SerializeField] private float hipsRotationSpring = 8000f;
    [SerializeField] private float hipsRotationDamper = 300f;
    [SerializeField] float posHalflife = 0.02f; // seconds; 0 = totally hard lock
    [SerializeField] float rotHalflife = 0.02f; // seconds


    [Header("Ragdoll Bone Settings")]
    public List<ActiveRagdollBone> bones = new List<ActiveRagdollBone>();
    public bool configureJointsInWorldSpce = true;


    private Rigidbody rootRigidbody;

    [Header("debugs")]
    public bool overrideAllSprings;
    public float sharedSpringValue = 20000f;
    public AnimationCurve springCurve = AnimationCurve.Linear(0, 1, 1, 1);
    public float sharedDampPercent = 0.1f;
    public AnimationCurve dampingCurve = AnimationCurve.Linear(0, 1, 1, 1);
    public bool useCriticalDampening = false;


    [Header("Hips Spring to network pos")]
    public Rigidbody networkToLocalSpring;
    public ConfigurableJoint networkSpringJoint;


    [SerializeField] private AnimationCurve linearSpringCurve = AnimationCurve.EaseInOut(0, 0.1f, 1, 1);
    [SerializeField] private float maxLinearSpring = 100000f;


    [SerializeField] private AnimationCurve linearDampingCurve = AnimationCurve.Linear(0, 1, 1, 1);
    [SerializeField] private float maxLinearCritDampMult = 1f;


    [SerializeField] private AnimationCurve angularSpringCurve = AnimationCurve.EaseInOut(0, 0.1f, 180, 1);
    [SerializeField] private float maxAngularSpring = 100000f;

    [SerializeField] private AnimationCurve angularDampingCurve = AnimationCurve.Linear(0, 1, 1, 1);
    [SerializeField] private float maxAngularCritDampMult = 1f;

    void Awake()
    {
        rootRigidbody = GetComponent<Rigidbody>();
        if (cameraTransform == null) cameraTransform = Camera.main.transform;

        
        transform.parent = null;
        //networkToLocalSpring.transform.parent = null;

        InitilizeBones();
    }


    private void FixedUpdate()
    {
        //Debug.Log("Running fixed Update");
        //UpdateHipsFollowPD();
        //UpdateNetworkSpring();
        UpdateBoneJoints();

    }

    private void LateUpdate()
    {
       // UpdateHipsFollowPD();
    }

    #region local ragdoll / local animator

    void UpdateAnimator()
    {
        if (!animator) return;
        Vector3 localVel = transform.InverseTransformDirection(characterController.rendererVelocity);
        animator.SetFloat("forwardSpeed", localVel.z / characterController.maxSpeed, 0.1f, Time.deltaTime);
        animator.SetFloat("rightSpeed", localVel.x / characterController.maxSpeed, 0.1f, Time.deltaTime);
    }



    public void UpdateHipsFollowPD()
    {
        Vector3 targetPos = characterController.rendererPos + targetHipsOffset;
        Quaternion targetRot = characterController.rendererRot;

        rootRigidbody.transform.position = targetPos;
        rootRigidbody.transform.rotation = targetRot;
        rootRigidbody.MovePosition(targetPos);
        rootRigidbody.MoveRotation(targetRot);
    }

    public void UpdateHipsFollowPD(Transform targetTrans)
    {
        /*
        Vector3 targetPos = targetTrans.position + targetHipsOffset;
        Quaternion targetRot = targetTrans.rotation;

        rootRigidbody.transform.position = targetPos;
        rootRigidbody.transform.rotation = targetRot;
        rootRigidbody.MovePosition(targetPos);
        rootRigidbody.MoveRotation(targetRot);*/
    }

    void UpdateNetworkSpring()
    {
        Vector3 targetPos = characterController.rendererPos + targetHipsOffset;
        Quaternion targetRot = characterController.rendererRot;
        //networkToLocalSpring.MovePosition(targetPos);
        //networkToLocalSpring.MoveRotation(targetRot);

        networkSpringJoint.targetPosition = Vector3.zero;
        networkSpringJoint.targetRotation = Quaternion.identity;


        float distance = Vector3.Distance(networkToLocalSpring.position, ragdollHipsRigidbody.position)/10;

        Quaternion rotationError = networkToLocalSpring.rotation * Quaternion.Inverse(ragdollHipsRigidbody.rotation);
        rotationError.ToAngleAxis(out float angle, out Vector3 axis);
        angle = Mathf.Abs(angle);

        float linearSpringMultiplier = linearSpringCurve.Evaluate(distance);
        float linearDampMultiplier = linearDampingCurve.Evaluate(distance);

        float currentLinearSpring = maxLinearSpring * linearSpringMultiplier;

        float criticalLinearDamper = 2f * Mathf.Sqrt(ragdollHipsRigidbody.mass * currentLinearSpring);
        float currentLinearDamper = criticalLinearDamper * maxLinearCritDampMult * linearDampMultiplier;

        var linearDrive = new JointDrive
        {
            positionSpring = currentLinearSpring,
            positionDamper = currentLinearDamper,
            maximumForce = Mathf.Infinity
        };
        networkSpringJoint.xDrive = linearDrive;
        networkSpringJoint.yDrive = linearDrive;
        networkSpringJoint.zDrive = linearDrive;


        float angularSpringMultiplier = angularSpringCurve.Evaluate(angle);
        float angularDampMultiplier = angularDampingCurve.Evaluate(angle);
        float currentAngularSpring = maxAngularSpring * angularSpringMultiplier;
        float criticalAngularDamper = 2f * Mathf.Sqrt(ragdollHipsRigidbody.inertiaTensor.magnitude * currentAngularSpring);
        float currentAngularDamper = criticalAngularDamper * maxAngularCritDampMult * angularDampMultiplier;

        var angularDrive = new JointDrive
        {
            positionSpring = currentAngularSpring,
            positionDamper = currentAngularDamper,
            maximumForce = Mathf.Infinity
        };
        networkSpringJoint.slerpDrive = angularDrive;
    }

    void UpdateBoneJoints()
    {
        if (animatedPelvisRoot == null) return;

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
            var spring = (overrideAllSprings? sharedSpringValue: bone.positionSpringForce) * springCurve.Evaluate(normalizedAngle);

            drive.positionSpring = spring;

            float criticalDampening = 2f * Mathf.Sqrt(spring *  (bone.rb.inertiaTensor.magnitude));

            drive.positionDamper = overrideAllSprings ? sharedDampPercent * (useCriticalDampening?criticalDampening:1) * dynamicDamperMult : bone.criticalDamperMult * (useCriticalDampening ? criticalDampening : 1) * dynamicDamperMult;
            drive.maximumForce = Mathf.Infinity;
            bone.joint.slerpDrive = drive;
        }
    }

    void InitilizeBones()
    {

        foreach (var bone in bones)
        {
            if (bone.joint == null) continue;

            if (bone.animatedBone == null)
            {
                bone.animatedBone = animatedPelvisRoot.root.GetComponentsInChildren<Transform>()
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
            drive.positionDamper = overrideAllSprings ? sharedDampPercent * (useCriticalDampening ? criticalDampening : 1)  : bone.criticalDamperMult * (useCriticalDampening ? criticalDampening : 1) ;
            drive.maximumForce = Mathf.Infinity;
            bone.joint.slerpDrive = drive;

            var animBindLocal = bone.animatedBone ? bone.animatedBone.localRotation : Quaternion.identity;
            bone.bindCorrection = Quaternion.Inverse(animBindLocal) * bone.startLocalRotation;
        }
    }
    #endregion


}
