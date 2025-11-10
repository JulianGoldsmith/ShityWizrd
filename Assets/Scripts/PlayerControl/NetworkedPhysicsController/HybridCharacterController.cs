using Fusion;
using Fusion.Addons.Physics;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEditor;

public enum BONKEDSTATE
{
    ALIVE,
    BONKED,
}

public class HybridCharacterController : NetworkBehaviour
{
    [Header("Components")]
    public Rigidbody hipsRb;
    public Vector3 hipsOffset;
    public List<NetworkRigidbody3D> networkRigidbody3Ds = new List<NetworkRigidbody3D>();
    public float totalMass;

    [Header("Grounded Settings")]
    public float groundCheckExtraDistance = 1f;
    public LayerMask groundLayer;
    [Networked] public bool IsGrounded { get; set; }

    [Header("Suspension Settings")]
    public float rideHeight = 0.8f; // The target height the hips will float at
    public float rideSpringStrength = 100f; // How stiff the suspension spring is
    public float rideSpringDamper = 10f; // How much the spring is damped to prevent bouncing
    public float suspensionCastRadius = 0.25f; // The radius of the spherecast


    [Header("Movement Settings")]
    public float maxWalkSpeed = 3f, maxSprintSpeed = 5f;
    public float acceleration = 20, braking = 20;
    public float jumpForce = 50f;

    [Header("Rotation Settings")]
    private Quaternion bodyRot;
    public float turnStrength = 100f, turnDamping = 10f;
    public AnimationCurve turnBufferCurve;
    public float uprightForce, uprightDamp;

    [Header("Animation")]
    public Animator targetAnimator, finalAnimator;
    public BoneMapper boneMapper;
    public Transform spineIKTarget;
    public Vector3 headOffset;
    public Transform armatureHipsRoot;
    private Vector3 armatureHipsStartOffset;
    public ArmatureRetargeter retargeter;

    [Header("PD armature")]
    public List<PDSpring> pDSprings = new List<PDSpring>();
    [Networked] Quaternion initialTorsoRot { get; set; }
    [Networked] Quaternion initialHeadRot { get; set; }

    public List<PdBone> pdBones = new List<PdBone>();

    private float pdDesignDt  = 1f / 64f;

    [Header("Network Input")]
    [HideInInspector][Networked] public Vector2 moveInput { get; set; }
    [HideInInspector][Networked] public Quaternion lookRot { get; set; }
    [HideInInspector][Networked] public NetworkButtons _lastButtonsInput { get; set; }
    [Networked] private int _jumpCount { get; set; }
    [Networked] public bool sprint { get; set; }
    private int _lastVisibleJump;


    [Header("Network Pos")]
    [SerializeField] public Transform networkedRenderRoot;
    [SerializeField] public Transform smoothedNetworkedRenderRoot;
    [HideInInspector] public Vector3 rendererPos;
    [HideInInspector] public Quaternion rendererRot;
    [HideInInspector] public Vector3 rendererVelocity;
    [HideInInspector] public float rendererYawSpeed;
    [HideInInspector] public Vector3 rendererAngularVel;
    [HideInInspector] public Vector3 lastRendererPos;
    [HideInInspector] Quaternion lastRendererRot;
    private ChangeDetector _changeDetector;
    [Networked] public bool isHost { get; set; }
    [HideInInspector] public bool cashIsHost = false;
    public Material hostMat, clientMat;
    public SkinnedMeshRenderer modelRenderer, ragDollRenderer;

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;
    public RagDollCameraController camController;
    [SerializeField] public Transform cameraAnchorTransform;
    [SerializeField] private float cameraAnchorSmoothSpeed = 20f;

    [Header("Hands")]
    public NetworkedHandsController handController;

    private Vector3 previousVelocity;
    public Vector3 Acceleration { get; private set; }

    [Header("Bonked Variables")]
    public CharacterBonkController bonkController;


    ChangeDetector
        changeDetector;

    [Header("For Teleporting")]
    private bool _teleportRequested = false;
    private Vector3 _teleportTargetPos;
    private Quaternion _teleportTargetRot;

    [Header("Disable/Enable CC")]
    private int disableCC = -1; // -1 = enabled - > 0 = disabled for X ticks.

    public override void Spawned()
    {
        _lastVisibleJump = _jumpCount;
        lastRendererPos = networkedRenderRoot.position;
        lastRendererRot = networkedRenderRoot.rotation;

        //general setup
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);


        //Camera Setup
        camController = this.GetComponent<RagDollCameraController>();
        if (cameraTransform == null) cameraTransform = Camera.main.transform;
        if (camController != null)
        {
            if (HasInputAuthority)
            {
                //camController.enabled = true;
                camController.Spawned(true);
            }
            else
            {
                camController.Spawned(false);
                //camController.enabled = false;
            }
        }

        //boneMapper.Spawn(false, targetAnimator.transform, finalAnimator.transform);

        //for (int i = 0; i < pDSprings.Count; i++)
        //{
        //    var spring = pDSprings[i];
        //    spring.Init(hipsRb.transform, HasInputAuthority);
        //}
        //if (HasStateAuthority)
        //{
        //    initialTorsoRot = pDSprings[1].startJointRotation;
        //    initialHeadRot = pDSprings[0].startJointRotation;
        //}


        //hands
        handController.Spawn(networkedRenderRoot, HasInputAuthority);

        if (HasInputAuthority)
        {
            GameController.Instance.playerInput = this.GetComponent<PlayerInput>();
        }


        this.cameraAnchorTransform.parent = null;

        bonkController = this.GetComponent<CharacterBonkController>();
      //  Debug.Log($"Bonk controller loaded {bonkController != null}");

        hipsRb.maxDepenetrationVelocity = 10f;
        hipsRb.maxAngularVelocity = 20f;

        Runner.SetIsSimulated(this.Object, true);

        foreach (NetworkRigidbody3D nrb in networkRigidbody3Ds)
        {
            Runner.SetIsSimulated(nrb.Object, true);
            totalMass += nrb.Rigidbody.mass;

           // NetworkTRSP nt = nrb.Object.GetComponent<NetworkTRSP>();

        }

        armatureHipsStartOffset = new Vector3(armatureHipsRoot.transform.localPosition.x,
                armatureHipsRoot.transform.localPosition.y, armatureHipsRoot.transform.localPosition.z);
    }


    public override void FixedUpdateNetwork()
    {
        if (!IsFinite(hipsRb.transform.position))
        {
            Debug.LogError($"<color=red>CRITICAL: hipsRb transform.position is invalid Stopped simulation for this tick.</color>");
            //return;
        }
        if (!IsFinite(hipsRb.transform.rotation))
        {
            Debug.LogError($"<color=red>CRITICAL: hipsRb transform.rotation is invalid Stopped simulation for this tick.</color>");
            //return;
        }
        if (!IsFinite(hipsRb.linearVelocity))
        {
            Debug.LogError($"<color=red>CRITICAL: hipsRb hipsRb.linearVelocity is invalid Stopped simulation for this tick.</color>");
            //hipsRb.linearVelocity = Vector3.zero;
            //return;
        }
        if (!IsFinite(hipsRb.angularVelocity))
        {
            Debug.LogError($"<color=red>CRITICAL: hipsRb hipsRb.angularVelocity is invalid Stopped simulation for this tick.</color>");
            // return;
        }



        if (_teleportRequested)
        {
            ExecuteTeleport(_teleportTargetPos, _teleportTargetRot);

            _teleportRequested = false;

            return;
        }


        if (disableCC > 0)
        {
            disableCC--; 
            return;      // Skip ALL simulation logic for this tick
        }

        //Debug.Log($"NetworkUpdate for - Is Local = {HasInputAuthority} + {this.GetComponent<NetworkObject>().InputAuthority} + {isHost}");
        DetectVariablesChangedOnNetwork();
        if (GetInput(out NetworkInputData data) && HasStateAuthority || HasInputAuthority)
        {

            data.direction.Normalize();
            moveInput = new Vector2(data.direction.x, data.direction.z);
            lookRot = IsFinite(data.lookRotation) ? data.lookRotation : Quaternion.identity;

            sprint = data.buttons.IsSet(EInputButton.SPRINT);

            if (data.buttons.WasPressed(_lastButtonsInput, EInputButton.JUMP) && IsGrounded)
            {
                ApplyJump(); //animation is applied in Render -> Update Animations()
                _jumpCount++;
            }

            if (data.buttons.WasPressed(_lastButtonsInput, EInputButton.SELF_BONK))
            {
                if (bonkController.BonkedState != BONKEDSTATE.BONKED)
                    GetBonked(); //animation is applied in Render -> Update Animations()
            }

            if (data.buttons.WasPressed(_lastButtonsInput, EInputButton.UN_SELF_BONK))
            {
                GetUnBonked(); //animation is applied in Render -> Update Animations()
            }
            _lastButtonsInput = data.buttons;
        }


        //changed
        if (bonkController.BonkedState == BONKEDSTATE.BONKED) return;

        UpdateAnimatorPos(true);

        ApplyUprightTorque();

        // 1) look torque
        ApplyLookRotation();


        // 2) suspension
        UpdateHips();


        // 3) planar movement PD
        ApplyHipsMovement();

        // 5) anything past here
        UpdateSpineIK();


        // 4) PD springs (joints)
        UpdateTorsoAndHead();

      


        if (Runner.DeltaTime > 1e-6f)
        {
            Acceleration = (hipsRb.linearVelocity - previousVelocity) / Runner.DeltaTime;
        }
        else
        {
            Acceleration = Vector3.zero;
        }
        previousVelocity = hipsRb.linearVelocity;

    }

    public override void Render()
    {
        //transform.position = hipsRb.transform.position;
        //transform.rotation = hipsRb.transform.rotation;
        if (disableCC > 0)
        {

            return;
        }
        UpdateCameraAnchor();
        CasheMovement();
        UpdateAnimator();

    }

  

    private void UpdateCameraAnchor()
    {
        //if (!cameraAnchorTransform || !networkedRenderRoot) return;

        float dt = Time.deltaTime;
        Vector3 targetPos = networkedRenderRoot.position;
        Quaternion targetRot = networkedRenderRoot.rotation;

        if (HasInputAuthority)
        {
            cameraAnchorTransform.position = Vector3.Lerp(cameraAnchorTransform.position, targetPos, dt * cameraAnchorSmoothSpeed);
            cameraAnchorTransform.rotation = Quaternion.Slerp(cameraAnchorTransform.rotation, targetRot, dt * cameraAnchorSmoothSpeed);
        }
        else
        {
            cameraAnchorTransform.position = targetPos;
            cameraAnchorTransform.rotation = targetRot;
        }
    }


    public void GetBonked()
    {
        bonkController.GetBonked();
    }

    public void GetUnBonked()
    {
        bonkController.GetUnBonked();
    }


    void UpdateAnimator()
    {
        if (!targetAnimator) return;

        var targetPos = rendererPos + hipsOffset;
        var targetRot = rendererRot;

        if (!IsFinite(targetPos))
        {
            Debug.Log($"<color=red> CRITIAL target RENDERER POS is INFINATE IN UPDATE ANIMATOR? ");
            targetPos = hipsRb.transform.position;
        }
        if (!IsFinite(targetRot))
        {
            Debug.Log($"<color=red> CRITIAL target RENDERER ROT is INFINATE IN UPDATE ANIMATOR? ");
            targetRot = hipsRb.transform.rotation;
        }

        //update Armature Positions - updates to the render target for smoothing
        UpdateAnimatorPos(false);

        Vector3 localVel = networkedRenderRoot.transform.InverseTransformDirection(rendererVelocity);

        if (!IsFinite(localVel)) localVel = Vector3.zero;
        if (float.IsNaN(rendererYawSpeed) || float.IsInfinity(rendererYawSpeed)) rendererYawSpeed = 0;

        targetAnimator.SetFloat("forwardSpeed", localVel.z / (maxWalkSpeed * 2), 0.1f, Time.deltaTime);
        targetAnimator.SetFloat("rightSpeed", localVel.x / (maxWalkSpeed * 2), 0.1f, Time.deltaTime);
        targetAnimator.SetFloat("RotationSpeed", rendererYawSpeed, 0.1f, Time.deltaTime);

        //animationController.UpdateSpineIkTarget(lookRot);
        //finalAnimator.SetFloat("forwardSpeed", localVel.z / (maxWalkSpeed * 2), 0.1f, Time.deltaTime);
        //finalAnimator.SetFloat("rightSpeed", localVel.x / (maxWalkSpeed * 2), 0.1f, Time.deltaTime);
        //finalAnimator.SetFloat("RotationSpeed", rendererYawSpeed, 0.1f, Time.deltaTime);

        //Jumping 
        targetAnimator.SetBool("IsGrounded", IsGrounded);
        //finalAnimator.SetBool("IsGrounded", IsGrounded);
        if (_jumpCount > _lastVisibleJump)
        {
            targetAnimator.SetTrigger("Jump");
            //finalAnimator.SetTrigger("Jump");
            _lastVisibleJump = _jumpCount;
        }

        if (retargeter != null)
        {
            retargeter.animatedHipRootMotion = (new Vector3(armatureHipsRoot.transform.localPosition.x,
                armatureHipsRoot.transform.localPosition.y, armatureHipsRoot.transform.localPosition.z) - armatureHipsStartOffset) * 0.01f;
            retargeter.animatedHipRotation = armatureHipsRoot.transform.localRotation;
        }
    }

    void UpdateAnimatorPos(bool inFixedUpdate)
    {
        Vector3 targetPos;
        Quaternion targetRot;
        if (!inFixedUpdate)
        {
            targetPos = rendererPos + hipsOffset;
            targetRot = rendererRot;
        }
        else
        {

            targetPos = hipsRb.transform.position + hipsOffset;
            targetRot = hipsRb.transform.rotation;
            //hipsRootMotionYDetla = armatureHipsRoot.localPosition.z / 100f; // i think this will use z as up from blender
        }


        if (!IsFinite(targetPos))
        {
            Debug.Log($"<color=red> CRITIAL target RENDERER POS is INFINATE IN UPDATE ANIMATOR? ");
        }
        if (!IsFinite(targetRot))
        {
            Debug.Log($"<color=red> CRITIAL target RENDERER ROT is INFINATE IN UPDATE ANIMATOR? ");
        }

        targetAnimator.gameObject.transform.position = smoothedNetworkedRenderRoot.transform.position + hipsOffset; 
        targetAnimator.gameObject.transform.rotation = smoothedNetworkedRenderRoot.transform.rotation;
        finalAnimator.gameObject.transform.position = smoothedNetworkedRenderRoot.transform.position + ((new Vector3(armatureHipsRoot.transform.localPosition.x,
                armatureHipsRoot.transform.localPosition.y, armatureHipsRoot.transform.localPosition.z)) /100); 
        finalAnimator.gameObject.transform.rotation = smoothedNetworkedRenderRoot.transform.rotation;
    }

    public void UpdateSpineIK()
    {
        Vector3 lookDir;

        //if (HasInputAuthority)
        //{
        //    lookDir = cameraTransform.forward;
        //}
        //else
        //{
        //    lookDir = lookRot * Vector3.forward;
        //}

        lookDir = lookRot * Vector3.forward;

        //if(HasStateAuthority)
        spineIKTarget.position = (hipsRb.position + headOffset) + (lookDir * 5);
    }

    public void CasheMovement()
    {
        if (!networkedRenderRoot) return;


        if (!IsFinite(networkedRenderRoot.position) || !IsFinite(networkedRenderRoot.rotation)) return;

        // Read the smoothed proxy pose here
        rendererPos = networkedRenderRoot.position;
        rendererRot = networkedRenderRoot.rotation;

        if (Time.deltaTime <= 1e-6f) return;

        float dtRender = Mathf.Max(Time.deltaTime, 1e-6f);

        // Approximate render-space velocity (good enough for PD damping)
        var p = networkedRenderRoot.position;
        rendererVelocity = (p - lastRendererPos) / (Time.deltaTime);
        lastRendererPos = p;

        //if (!IsFinite(lastRendererRot)) lastRendererRot = rendererRot;

        if (!IsFinite(rendererRot)) Debug.LogError("[NaN] rendererRot");
        if (!IsFinite(lastRendererRot)) Debug.LogError("[NaN] lastRendererRot");

        Quaternion deltaRotation = rendererRot * Quaternion.Inverse(lastRendererRot);
        deltaRotation.ToAngleAxis(out float angleInDegrees, out Vector3 axis);
        if (angleInDegrees > 180f)
        {
            angleInDegrees -= 360f;
        }

        Vector3 angularVelocityInRadians;
        if (axis.sqrMagnitude < 1e-6f)
        {
            angularVelocityInRadians = Vector3.zero;
        }
        else
        {
            angularVelocityInRadians = axis.normalized * (angleInDegrees * Mathf.Deg2Rad / dtRender);
        }


        rendererAngularVel = angularVelocityInRadians * 0.57f;
        rendererYawSpeed = rendererAngularVel.y;

        lastRendererRot = rendererRot;
    }

    public void DetectVariablesChangedOnNetwork()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(isHost):
                    modelRenderer.material = isHost ? hostMat : clientMat;
                    break;
            }
        }
    }

    public void ApplyLookRotation()
    {

        //var _lookRot = HasInputAuthority ? cameraTransform.rotation : lookRot;
        var _lookRot = lookRot;
        Vector3 flatLookDir = Vector3.ProjectOnPlane(_lookRot * Vector3.forward, Vector3.up).normalized;
        //Vector3 flatBodyDir = Vector3.ProjectOnPlane(networkedRenderRoot.rotation * Vector3.forward, Vector3.up).normalized;
        Vector3 flatBodyDir = Vector3.ProjectOnPlane(hipsRb.rotation * Vector3.forward, Vector3.up).normalized;
        if (flatLookDir.sqrMagnitude < 1e-6f) flatLookDir = Vector3.forward; // safe default
        if (flatBodyDir.sqrMagnitude < 1e-6f) flatBodyDir = Vector3.forward; // safe default

        Quaternion targetBodyRot = networkedRenderRoot.rotation;

        Vector3 targetLookDir;

        bool isMoveing = moveInput.sqrMagnitude > 0.01f;
        if (isMoveing)
        {
            targetLookDir = flatLookDir;
        }
        else
        {

            targetLookDir = GetBufferedTargetLookDir(flatBodyDir, flatLookDir);
        }

        float rotDifferenceInDegrees = Vector3.SignedAngle(flatBodyDir, targetLookDir, Vector3.up);

        rotDifferenceInDegrees /= 180;

        float angularVelocityY = hipsRb.angularVelocity.y;
        if (float.IsNaN(angularVelocityY) || float.IsInfinity(angularVelocityY))
        {
            angularVelocityY = 0;
        }

        float proportionalTorque = rotDifferenceInDegrees * turnStrength;
        float derivativeTorque = angularVelocityY * turnDamping;

        float torqueForce = proportionalTorque - derivativeTorque;

        Vector3 torque = Vector3.up * (torqueForce);
        if (!IsFinite(torque))
        {
            Debug.Log($"<color=red> CRITICAL NANA __ CAUSED BY TORQUE </color>");
        }
        else if (Mathf.Abs(torqueForce) > 1e4f)
        {
            Debug.LogWarning($"[BIG] torqueY={torqueForce}");
        }

        hipsRb.AddTorque(torque, ForceMode.Acceleration);
    }

    public Vector3 GetBufferedTargetLookDir(Vector3 _flatBodyDir, Vector3 _flatLookDir)
    {
        float rotDifferenceInDegrees = Vector3.SignedAngle(_flatBodyDir, _flatLookDir, Vector3.up);

        float rotDifference01 = rotDifferenceInDegrees / 180f;

        if (float.IsNaN(rotDifference01)) rotDifference01 = 0;

        float evaluatedCurve = turnBufferCurve.Evaluate(Mathf.Abs(rotDifference01));
        if (float.IsNaN(evaluatedCurve)) evaluatedCurve = 0;

        rotDifferenceInDegrees = rotDifferenceInDegrees * evaluatedCurve;

        Quaternion rotation = Quaternion.AngleAxis(rotDifferenceInDegrees, Vector3.up);

        return rotation * _flatBodyDir;
    }

    public void ApplyJump()
    {
        if (IsGrounded)
            hipsRb.AddForce(jumpForce * Vector3.up, ForceMode.VelocityChange);

        //Debug.Log("Jumped");
    }
    void ApplyUprightTorque()
    {
        Vector3 up = hipsRb.rotation * Vector3.up;
        Vector3 axis = Vector3.Cross(up, Vector3.up);
        float angle = Mathf.Asin(Mathf.Clamp(axis.magnitude, -1f, 1f));
        if (angle != 0f) axis /= axis.magnitude;


        float kp = uprightForce;
        float kd = uprightDamp;


        Vector3 tiltCorrection = axis * (angle * kp);

        Vector3 angVel = hipsRb.angularVelocity;
        angVel.y = 0f;

        Vector3 damping = -angVel * kd;

        Vector3 torque = tiltCorrection + damping;

        if (!IsGrounded) torque *= 0.5f;

        hipsRb.AddTorque(torque, ForceMode.Acceleration);
    }
    public void UpdateHips()
    {
        // IsGrounded = Physics.Raycast(hipsRb.transform.position, Vector3.down, out RaycastHit hitInfo, groundCheckDistance, groundLayer);

        Vector3 castOrigin = hipsRb.worldCenterOfMass;

        float distanceToCast = rideHeight + suspensionCastRadius + groundCheckExtraDistance;

        //Debug.DrawRay(castOrigin, Vector3.down* distanceToCast, Color.red);

        IsGrounded = Physics.SphereCast(castOrigin, suspensionCastRadius, Vector3.down, out RaycastHit hitInfo, distanceToCast, groundLayer);
        Debug.DrawRay(castOrigin, Vector3.down * distanceToCast, Color.aliceBlue);

        if (IsGrounded)
        {
            float currentDistance = hitInfo.distance;

            float heightError = (rideHeight) - currentDistance;

            float springForce = heightError * rideSpringStrength;

            float currentVerticalVelocity = Vector3.Dot(hipsRb.linearVelocity, Vector3.up);

            float damperForce = -currentVerticalVelocity * rideSpringDamper;

            if (Mathf.Abs(currentVerticalVelocity) > 200f)
                Debug.LogWarning($"[BIG] vY={currentVerticalVelocity}");

            Vector3 finalForce = Vector3.up * (springForce + damperForce);

            if (!IsFinite(finalForce))
            {
                Debug.Log($"<color=red> CRITICAL NANA __ CAUSED BY UPDATEHIPS Grounded SPRING </color>");
            }


            float f = springForce + damperForce;
            if (Mathf.Abs(f) > 1e4f) Debug.LogWarning($"[BIG] spring+damper={f}  vY={currentVerticalVelocity}");


            hipsRb.AddForce(finalForce, ForceMode.Acceleration);



            if (hitInfo.transform.TryGetComponent<Rigidbody>(out Rigidbody hitRb))
            {
                hitRb.AddForce(hipsRb.mass * -0.98f * Vector3.up, ForceMode.Acceleration); ////THIS WILL CAUSE JITTER - RETHINK THIS
            }
        }
    }

    private void ApplyHipsMovement()
    {

        //Debug.Log($"Apply movement - Is Local = {HasInputAuthority} + {this.GetComponent<NetworkObject>().InputAuthority}");
        Vector2 _moveInput = moveInput;
        Quaternion _lookRot = lookRot;
        _moveInput.Normalize();

        Vector3 camForward = _lookRot * Vector3.forward;
        camForward.y = 0;
        camForward.Normalize();

        Vector3 camRight = _lookRot * Vector3.right;
        camRight.y = 0;
        camRight.Normalize();

        Vector3 moveDirection = (camForward * _moveInput.y + camRight * _moveInput.x);
        Vector3 targetVelocity = moveDirection * (sprint ? maxSprintSpeed : maxWalkSpeed);

        //this may seem a bit hackey but it seems to solve the overshoot caused by "constant" input prediction (ie predicing continue run)
        if (IsProxy && Runner.IsResimulation)
        {
            targetVelocity = targetVelocity / 2;
        }

        Vector3 currentVelocity = hipsRb.linearVelocity;
        if (!IsFinite(currentVelocity))
        {
            currentVelocity = Vector3.zero;
        }
        currentVelocity.y = 0;

        Vector3 velocityError = targetVelocity - currentVelocity;

        float forceMagnitude = _moveInput.sqrMagnitude > 0.01f ? acceleration : IsGrounded?braking: braking/20f;
        Vector3 correctiveForce = velocityError * forceMagnitude;

        if (!IsFinite(correctiveForce))
        {
            Debug.Log($"<color=red> CRITICAL NANA __ CAUSED BY ApplyHipsMovement MOVEMENT PD </color>");
        }

        if (correctiveForce.sqrMagnitude > 1e8f) // ~ |f| > 1e4
            Debug.LogWarning($"[BIG] correctiveForce |f|={correctiveForce.magnitude}");

        hipsRb.AddForce(correctiveForce, ForceMode.Acceleration);
    }

    private void UpdateTorsoAndHead()
    {
        //float dt = Mathf.Max((float)Runner.DeltaTime, 1e-4f);
        //for (int i = 0; i < pDSprings.Count; i++)
        //{
        //    var spring = pDSprings[i];
        //    spring.UpdateBoneDrive(dt, pdDesignDt, i == 1 ? initialTorsoRot : initialHeadRot);
        //}

        float deltaTime = Mathf.Max((float)Runner.DeltaTime, 1e-4f);

        // Ensure physics bodies use interpolation = None and matching solver iterations elsewhere.

        for (int i = 0; i < pdBones.Count; i++)
        {
            var bone = pdBones[i];
            bone.designDeltaTime = pdDesignDt; // same reference dt across the rig
            bone.Step(Runner.DeltaTime);
        }

    }

    public Quaternion GetLookRot()
    {
        return HasInputAuthority ? cameraTransform.rotation : lookRot;
    }

    public Vector3 GetEyePos()
    {
        return networkedRenderRoot.transform.position + camController.localEyeOffset + camController.GetEyePosBasedOnPitch(HasInputAuthority ? cameraTransform.rotation : lookRot);
        //return hipsRb.transform.position + camController.localEyeOffset + camController.GetEyePosBasedOnPitch(HasInputAuthority?cameraTransform.rotation:lookRot);
    }

    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        if (!Object.HasStateAuthority) return;
        DisableCCFor(64);
        _teleportRequested = true;
        _teleportTargetPos = position;
        _teleportTargetRot = rotation;
    }

    public void ExecuteTeleport(Vector3 position, Quaternion rotation)
    {
        if (!Object.HasStateAuthority) return;
        Debug.Log($"Teleporting player to: {position}");


        //this.transform.GetComponent<NetworkTransform>().Teleport(position);

        Vector3 targetPos = position /* - this.transform.position + Vector3.up*0.6f*/;

        var hipsNRB = hipsRb.GetComponent<NetworkRigidbody3D>();
        if (hipsNRB != null)
        {
            hipsNRB.Teleport(targetPos, rotation);
            hipsNRB.Rigidbody.linearVelocity = Vector3.zero;
            hipsNRB.Rigidbody.angularVelocity = Vector3.zero;
        }
        else
        {
            hipsRb.transform.SetPositionAndRotation(targetPos, rotation);
        }

        foreach(NetworkRigidbody3D nrb in networkRigidbody3Ds)
        {
            hipsNRB.Teleport(targetPos, rotation);
            nrb.ResetRigidbody();
            Debug.Log($"Teleported {nrb.name}");
        }

        //foreach (var spring in pDSprings)
        //{
        //    if (spring.nrb == null) return;

        //    Vector3 newWorldPos = targetPos;
        //    Quaternion newWorldRot = rotation;

        //    spring.nrb.Teleport(newWorldPos, newWorldRot);
        //    spring.nrb.Rigidbody.linearVelocity = Vector3.zero;
        //    spring.nrb.Rigidbody.angularVelocity = Vector3.zero;
        //}

        if (targetAnimator != null)
        {
            targetAnimator.transform.SetPositionAndRotation(targetPos, rotation);
        }
        if (finalAnimator != null)
        {
            finalAnimator.transform.SetPositionAndRotation(targetPos, rotation);
        }
        if (handController != null)
        {
            handController.TeleportHands(targetPos, hipsRb.transform.position);
        }
    }

    public void DisableCCFor(int ticks)
    {
        disableCC = ticks;
        foreach(NetworkRigidbody3D n in networkRigidbody3Ds)
        {
            n.ResetRigidbody();
        }
    }

    public static bool IsFinite(Quaternion q) => float.IsFinite(q.x) && float.IsFinite(q.y) && float.IsFinite(q.z) && float.IsFinite(q.w);
    public static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

    bool CheckHips(string where)
    {
        var p = hipsRb.transform.position;
        var r = hipsRb.transform.rotation;
        var v = hipsRb.linearVelocity;
        var w = hipsRb.angularVelocity;

        bool ok = IsFinite(p) && IsFinite(r) && IsFinite(v) && IsFinite(w);
        if (!ok)
        {
            Debug.LogError(
                $"<color=red>[HIP-INVALID] after {where} | </color>" +
                $"pos={p} rot={r} v={v} w={w} | tick={Runner?.Tick.ToString() ?? "titk"}"
            );
        }
        return ok;
    }
    void DumpBodyProps(string where)
    {
        var it = hipsRb.inertiaTensor;
        var itR = hipsRb.inertiaTensorRotation;
        var scl = hipsRb.transform.lossyScale;
        Debug.LogWarning($"[RB] {where} scale={scl} inertia={it} inertiaRot={itR} CCD={hipsRb.collisionDetectionMode}");
    }

    [ContextMenu("PD Ragdoll/Bake Anchors • Use Each Target Transform")]
    private void ContextBakeAnchorsFromTargets()
    {

        if (pdBones == null || pdBones.Count == 0) { Debug.LogWarning("No PdBones to bake."); return; }

        for (int i = 0; i < pdBones.Count; i++)
        {
          var bone = pdBones[i];
          if (bone == null) continue;
          if (bone.targetTransform == null)
          {
            Debug.LogWarning($"PdBone {i}: targetTransform is missing, skipped.");
            continue;
          }
          bone.BakeAnchorsFromWorldPivot(bone.targetTransform.position);
        }

    Debug.Log("Baked PD anchors from each bone's targetTransform remember to save");

    }

}



[System.Serializable]
public class PDSpring
{
    public ConfigurableJoint joint;
    public Transform target;

    [Header("Angular Drive")]
    public float angluarSpringForce = 100f;
    public float angularSpringDamp = 50f;
    public AnimationCurve angularErrorMultiplier = AnimationCurve.Linear(0f, 1f, 1f, 10f);
    public Quaternion startJointRotation;
    private Quaternion startTargetRotation;


    [Header("Positional Drive")]
    public bool usePositionDrive = false;
    public float positionSpringForce = 1000f;
    public float positionSpringDamp = 10f;
    private Vector3 startTargetLocalPosition;
    public float linearLimit = 0f;

    public bool wasKinematicOnDisable;
    public Transform ragdollEquivelent;

    [HideInInspector] public NetworkRigidbody3D nrb;
    [HideInInspector] public Vector3 localOffsetFromHips;
    [HideInInspector] public Quaternion localRotationFromHips;

    public void Init(Transform hipsTransform, bool hasStateAuth)
    {
        startJointRotation = joint.transform.localRotation;
        startTargetRotation = target.localRotation;
        startTargetLocalPosition = target.localPosition;

        localOffsetFromHips = hipsTransform.InverseTransformPoint(joint.transform.position);
        localRotationFromHips = Quaternion.Inverse(hipsTransform.rotation) * joint.transform.rotation;
        //joint.gameObject.GetComponent<NetworkObject>().RemoveInputAuthority();

        var limit = new SoftJointLimit
        {
            limit = this.linearLimit
        };
        joint.linearLimit = limit;

        joint.projectionMode = JointProjectionMode.PositionAndRotation;
        joint.projectionAngle = 2f;     // small rescue cone
        joint.projectionDistance = 0.02f;
        

        nrb = joint.GetComponent<NetworkRigidbody3D>();
    }

    public void UpdateBoneDrive(float dt, float designDt, Quaternion? startRot = null)
    {

        //Quaternion currentTargetLocalRot = target.localRotation;
        //Quaternion deltaRotation = currentTargetLocalRot * Quaternion.Inverse(startTargetRotation);

        if (!HybridCharacterController.IsFinite(target.localRotation) || !HybridCharacterController.IsFinite(target.localPosition)) return;

        joint.SetTargetRotationLocal(target.localRotation, startRot ?? startJointRotation);

        float angleErr01 = Mathf.Abs(Quaternion.Angle(joint.transform.rotation, target.rotation)) / 180f;
        if (float.IsNaN(angleErr01)) angleErr01 = 0f;

        float forceMult = angularErrorMultiplier.Evaluate(angleErr01);

        // --- Time-step invariant scaling ---
        //float ratio = Mathf.Max(designDt / Mathf.Max(dt, 1e-4f), 0.01f);
        float springScaled = angluarSpringForce * forceMult ;
        float damperScaled = angularSpringDamp ;

        var s = joint.slerpDrive;
        s.positionSpring = springScaled;
        s.positionDamper = damperScaled;
        s.maximumForce = float.MaxValue;
        joint.slerpDrive = s;

        if (usePositionDrive)
        {
            Vector3 positionDelta = target.localPosition - startTargetLocalPosition;
            joint.targetPosition = positionDelta;

            var d = new JointDrive
            {
                positionSpring = positionSpringForce ,
                positionDamper = positionSpringDamp ,
                maximumForce = float.MaxValue
            };
            joint.xDrive = d; joint.yDrive = d; joint.zDrive = d;
        }
        else
        {
            var d = new JointDrive { positionSpring = 0, positionDamper = 0, maximumForce = 0 };
            joint.xDrive = d; joint.yDrive = d; joint.zDrive = d;
        }


    }
}


[Serializable]
public class PdBone
{
    [Header("Rigidbodies (child follows parent)")]
    public Rigidbody childRigidbody;      
    public Rigidbody parentRigidbody;      

    [Header("Target (LOCAL to parent frame)")]
    public Transform targetTransform;       

    [Header("Rotation PD")]
    public float proportionalGainRotation = 400f; 
    public float derivativeGainRotation = 40f;

    public bool applyEqualAndOppositeTorque = false; 

    public AnimationCurve rotationErrorCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Safety clamp")]
    public float maximumAngleRadians = 45f * Mathf.Deg2Rad; 
    public float maximumTorqueAcceleration = 2000f;       

    [Header("------------Position-----------------")]

    [Header("Optional Position PD (anchors)")]
    public bool usePositionDrive = false;
    public Vector3 parentAnchorLocal = Vector3.zero; 
    public Vector3 childAnchorLocal = Vector3.zero;
    [HideInInspector] public Quaternion worldTargetToChild;

    public float proportionalGainPosition = 2000f;
    public float derivativeGainPosition = 80f;

    public AnimationCurve positionErrorCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public bool applyEqualAndOppositeForce = false;
    public float forceTransfereRatio = 1f;

    [Header("Safety clamp")]
    public float maximumForceAcceleration = 2000f;              

    [Header("Time step reference")]
    public float designDeltaTime = 1f / 64f;

    const float snapDistance = 4f;


    public void Step(float deltaTime, float ragDollRotationStrength = 1)
    {
        if (childRigidbody == null || parentRigidbody == null || targetTransform == null)
            return;

        float safeDeltaTime = Mathf.Max(deltaTime, 1e-4f);
        //float scale = Mathf.Max(designDeltaTime / safeDeltaTime, 0.01f);
        float s = Mathf.Clamp(ragDollRotationStrength, 0.05f, 10f);

        Quaternion parentRotationWorld = parentRigidbody.rotation;
        //Quaternion targetRotationWorld = parentRotationWorld * targetTransform.localRotation;

        Quaternion targetParentRotationWorld = targetTransform.parent.rotation;
        Quaternion targetLocalRotation = Quaternion.Inverse(targetParentRotationWorld) * targetTransform.rotation;

        Quaternion targetRotationWorld = parentRotationWorld * targetLocalRotation;


        Quaternion rotationError = targetRotationWorld * Quaternion.Inverse(childRigidbody.rotation);
        rotationError.ToAngleAxis(out float angleDegrees, out Vector3 errorAxisWorld);

        if (angleDegrees > 180f) angleDegrees -= 360f;
        float angleRadians = angleDegrees * Mathf.Deg2Rad;

        if (errorAxisWorld.sqrMagnitude < 1e-8f)
            errorAxisWorld = Vector3.zero;
        else
            errorAxisWorld.Normalize();

        angleRadians = Mathf.Clamp(angleRadians, -maximumAngleRadians, maximumAngleRadians);

        Vector3 angularVelocityErrorWorld = childRigidbody.angularVelocity - parentRigidbody.angularVelocity;

        float proportionalRotationScaled = proportionalGainRotation * (s*s);
        float derivativeRotationScaled = derivativeGainRotation * s;

        float normalizedAngle = Mathf.Clamp01(Mathf.Abs(angleRadians) / Mathf.Max(maximumAngleRadians, 1e-4f));
        
        float rotationMult =  rotationErrorCurve.Evaluate(normalizedAngle);

        //insert logic here to increase PD torque based on ragDollRotationStrength
        //Also logic to increase dampening to avoid jitter without making it too sluggish 
        //rotation strength can be default value of 1 or from 0 to 10x, try to make this wihtout jitter

        Vector3 torqueAccelerationWorld = (proportionalRotationScaled * rotationMult * angleRadians ) * errorAxisWorld - (derivativeRotationScaled * (Mathf.Sqrt(rotationMult)) * angularVelocityErrorWorld);

        //torqueAccelerationWorld *= rotationMult;

        if (torqueAccelerationWorld.sqrMagnitude > maximumTorqueAcceleration * maximumTorqueAcceleration)
            torqueAccelerationWorld = torqueAccelerationWorld.normalized * maximumTorqueAcceleration;

        childRigidbody.AddTorque(torqueAccelerationWorld, ForceMode.Acceleration);
        if (applyEqualAndOppositeTorque)
        {
            parentRigidbody.AddTorque(-torqueAccelerationWorld, ForceMode.Acceleration);
        }
        //parentRigidbody.AddTorque(-torqueAccelerationWorld, ForceMode.Acceleration);

        if (usePositionDrive)
        {
            Vector3 parentAnchorWorld = parentRigidbody.position + parentRigidbody.rotation * parentAnchorLocal;
            Vector3 childAnchorWorld = childRigidbody.position + childRigidbody.rotation * childAnchorLocal;

            Vector3 parentAnchorVelocityWorld = parentRigidbody.GetPointVelocity(parentAnchorWorld);
            Vector3 childAnchorVelocityWorld = childRigidbody.GetPointVelocity(childAnchorWorld);

            Vector3 positionErrorWorld = parentAnchorWorld - childAnchorWorld;


            if (positionErrorWorld.sqrMagnitude > snapDistance * snapDistance) //teleport if far away
            {
                Vector3 newPos = childRigidbody.position + positionErrorWorld;

                childRigidbody.position = newPos;

                Vector3 dv = parentAnchorVelocityWorld - childAnchorVelocityWorld;
                childRigidbody.linearVelocity += dv;

                childRigidbody.WakeUp();

                return;
            }



            Vector3 velocityErrorWorld = parentAnchorVelocityWorld - childAnchorVelocityWorld;

            float forceMult = positionErrorCurve.Evaluate(velocityErrorWorld.magnitude);

            float proportionalPositionScaled = proportionalGainPosition;
            float derivativePositionScaled = derivativeGainPosition;

            Vector3 forceAccelerationWorld =
              proportionalPositionScaled * positionErrorWorld
              + derivativePositionScaled * velocityErrorWorld;

            if (forceAccelerationWorld.sqrMagnitude > maximumForceAcceleration * maximumForceAcceleration)
                forceAccelerationWorld = forceAccelerationWorld.normalized * maximumForceAcceleration;

            childRigidbody.AddForceAtPosition((forceAccelerationWorld * parentRigidbody.mass )/ childRigidbody.mass, childAnchorWorld, ForceMode.Acceleration);
            if (applyEqualAndOppositeForce)
            {
                parentRigidbody.AddForceAtPosition(((-forceAccelerationWorld * forceTransfereRatio ) * childRigidbody.mass )/ parentRigidbody.mass, parentAnchorWorld, ForceMode.Acceleration) ;
            }

            if(Vector3.Distance(childAnchorWorld, parentAnchorWorld) > 4)
            {
                childRigidbody.MovePosition(parentAnchorWorld);
            }

        }

    }

    //public void BakeAnchorsFromWorldPivot(Vector3 jointPivotWorld)
    //{
    //    if (parentRigidbody == null || childRigidbody == null)
    //    {
    //        Debug.LogWarning("[PdBone] Cannot bake anchors: missing rigidbodies.");
    //        return;
    //    }

    //    parentAnchorLocal = parentRigidbody.transform.InverseTransformPoint(jointPivotWorld);
    //    childAnchorLocal = childRigidbody.transform.InverseTransformPoint(jointPivotWorld);
    //    worldTargetToChild = childRigidbody.rotation * Quaternion.Inverse(targetTransform.rotation);
    //}

    public void BakeAnchorsFromWorldPivot(Vector3 jointPivotWorld)
    {
        if (parentRigidbody == null || childRigidbody == null)
        {
            Debug.LogWarning("[PdBone] Cannot bake anchors: missing rigidbodies.");
            return;
        }

        parentAnchorLocal = Quaternion.Inverse(parentRigidbody.rotation) * (jointPivotWorld - parentRigidbody.position);
        childAnchorLocal = Quaternion.Inverse(childRigidbody.rotation) * (jointPivotWorld - childRigidbody.position);
    }
}

