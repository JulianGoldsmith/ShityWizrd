using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

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

    [Header("Grounded Settings")]
    public float groundCheckDistance = 1f;
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

    [Header("Animation")]
    public Animator targetAnimator, finalAnimator;
    public BoneMapper boneMapper;
    public Transform spineIKTarget;
    public Vector3 headOffset;

    [Header("PD armature")]
    public List<PDSpring> pDSprings = new List<PDSpring>();
    [Networked] Quaternion initialTorsoRot {  get; set; }
    [Networked] Quaternion initialHeadRot { get; set; }

    [Header("Network Input")]
    [HideInInspector][Networked] public Vector2 moveInput { get; set; }
    [HideInInspector][Networked] public Quaternion lookRot { get; set; }
    [HideInInspector][Networked] public NetworkButtons _lastButtonsInput { get; set; }
    [Networked] private int _jumpCount { get; set; }
    [Networked] public bool sprint { get; set; }
    private int _lastVisibleJump;


    [Header("Network Pos")]
    [SerializeField] public Transform networkedRenderRoot;
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

        boneMapper.Spawn(false, targetAnimator.transform, finalAnimator.transform);

        for(int i = 0; i < pDSprings.Count;  i++)
        {
            var spring = pDSprings[i];
            spring.Init(hipsRb.transform, HasInputAuthority);
        }
        if (HasStateAuthority)
        {
            initialTorsoRot = pDSprings[1].startJointRotation;
            initialHeadRot = pDSprings[0].startJointRotation;
        }
        

        //hands
        handController.Spawn(networkedRenderRoot, HasInputAuthority);

        if (HasInputAuthority)
        {
            GameController.Instance.playerInput = this.GetComponent<PlayerInput>();
        }


        this.cameraAnchorTransform.parent = null;

        bonkController = this.GetComponent<CharacterBonkController>();
    }

   
    public override void FixedUpdateNetwork()
    {

        if (_teleportRequested)
        {
            ExecuteTeleport(_teleportTargetPos, _teleportTargetRot);

            _teleportRequested = false;

            return;
        }


        if (disableCC > 0)
        {
            disableCC--; // Count down the timer
            return;      // Skip ALL simulation logic for this tick
        }

        //Debug.Log($"NetworkUpdate for - Is Local = {HasInputAuthority} + {this.GetComponent<NetworkObject>().InputAuthority} + {isHost}");
        DetectVariablesChangedOnNetwork();
        if (GetInput(out NetworkInputData data))
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
                if(bonkController.BonkedState != BONKEDSTATE.BONKED)
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

        ApplyLookRotation(); //sets the hips rb to the rotation of the camera - currently hard sets this with no forces. 

        UpdateHips(); //applys suspention forces to the hips. 
        ApplyHipsMovement(); //applys input to aim for a target velocity along  x,z basically a PD to velocity

        UpdateTorsoAndHead();

        UpdateSpineIK();

        Acceleration = (hipsRb.linearVelocity - previousVelocity) / Runner.DeltaTime;
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
            // For the local player, the networkedRenderRoot is jittery due to prediction/reconciliation.
            // We MUST smoothly Lerp the camera anchor towards it to hide the jitter.
            cameraAnchorTransform.position = Vector3.Lerp(cameraAnchorTransform.position, targetPos, dt * cameraAnchorSmoothSpeed);
            cameraAnchorTransform.rotation = Quaternion.Slerp(cameraAnchorTransform.rotation, targetRot, dt * cameraAnchorSmoothSpeed);
        }
        else
        {
            // For remote players, the networkedRenderRoot is already interpolated by Fusion.
            // We can just snap the anchor directly to it.
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

        //update Armature Positions - updates to the render target for smoothing
        targetAnimator.gameObject.transform.position = targetPos;
        targetAnimator.gameObject.transform.rotation = targetRot;
        finalAnimator.gameObject.transform.position = targetPos;
        finalAnimator.gameObject.transform.rotation = targetRot;

        Vector3 localVel = networkedRenderRoot.transform.InverseTransformDirection(rendererVelocity);

        targetAnimator.SetFloat("forwardSpeed", localVel.z / (maxWalkSpeed * 2), 0.1f, Time.deltaTime);
        targetAnimator.SetFloat("rightSpeed", localVel.x / (maxWalkSpeed * 2), 0.1f, Time.deltaTime);
        targetAnimator.SetFloat("RotationSpeed", rendererYawSpeed, 0.1f, Time.deltaTime);
        
        //animationController.UpdateSpineIkTarget(lookRot);
        finalAnimator.SetFloat("forwardSpeed", localVel.z / (maxWalkSpeed * 2), 0.1f, Time.deltaTime);
        finalAnimator.SetFloat("rightSpeed", localVel.x / (maxWalkSpeed * 2), 0.1f, Time.deltaTime);
        finalAnimator.SetFloat("RotationSpeed", rendererYawSpeed, 0.1f, Time.deltaTime);

        //Jumping 
        targetAnimator.SetBool("IsGrounded", IsGrounded);
        finalAnimator.SetBool("IsGrounded", IsGrounded);
        if (_jumpCount > _lastVisibleJump)
        {
            targetAnimator.SetTrigger("Jump");
            finalAnimator.SetTrigger("Jump");
            _lastVisibleJump = _jumpCount;
        }

        
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

        if(HasStateAuthority)
            spineIKTarget.position = (networkedRenderRoot.position + headOffset) + (lookDir * 5);
    }

    public void CasheMovement()
    {
        if (!networkedRenderRoot) return;
        if (Time.deltaTime <= 1e-6f ) return;
     
        // Read the smoothed proxy pose here
        rendererPos = networkedRenderRoot.position;
        rendererRot = networkedRenderRoot.rotation;

         float dtRender = Mathf.Max(Time.deltaTime, 1e-6f);

        // Approximate render-space velocity (good enough for PD damping)
        var p = networkedRenderRoot.position;
        rendererVelocity = (p - lastRendererPos) / (Time.deltaTime);
        lastRendererPos = p;


       

        Quaternion deltaRotation = rendererRot * Quaternion.Inverse(lastRendererRot);
        deltaRotation.ToAngleAxis(out float angleInDegrees, out Vector3 axis);
        if (angleInDegrees > 180f)
        {
            angleInDegrees -= 360f;
        }

        Vector3 angularVelocityInRadians = axis.normalized * (angleInDegrees * Mathf.Deg2Rad / Time.deltaTime);
        rendererAngularVel = angularVelocityInRadians*0.57f;
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

        var _lookRot = HasInputAuthority ? cameraTransform.rotation : lookRot;
        //var _lookRot = lookRot;
        Vector3 flatLookDir = Vector3.ProjectOnPlane(_lookRot * Vector3.forward, Vector3.up).normalized;
        Vector3 flatBodyDir = Vector3.ProjectOnPlane(networkedRenderRoot.rotation * Vector3.forward, Vector3.up).normalized;

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

        float proportionalTorque = rotDifferenceInDegrees * turnStrength;
        float derivativeTorque = angularVelocityY * turnDamping;

        Vector3 torque = Vector3.up * (proportionalTorque - derivativeTorque);

        hipsRb.AddTorque(torque, ForceMode.Acceleration);
    }

    public Vector3 GetBufferedTargetLookDir(Vector3 _flatBodyDir, Vector3 _flatLookDir)
    {
        float rotDifferenceInDegrees = Vector3.SignedAngle(_flatBodyDir, _flatLookDir, Vector3.up);

        float rotDifference01 = rotDifferenceInDegrees/180f;

        rotDifferenceInDegrees = rotDifferenceInDegrees * turnBufferCurve.Evaluate(Mathf.Abs(rotDifference01));

        Quaternion rotation = Quaternion.AngleAxis(rotDifferenceInDegrees, Vector3.up);

        return rotation * _flatBodyDir;
    }

    public void ApplyJump()
    {
        if(IsGrounded)
            hipsRb.AddForce(jumpForce * Vector3.up, ForceMode.VelocityChange);

        //Debug.Log("Jumped");
    }

    public void UpdateHips()
    {
        // IsGrounded = Physics.Raycast(hipsRb.transform.position, Vector3.down, out RaycastHit hitInfo, groundCheckDistance, groundLayer);

        Vector3 castOrigin = hipsRb.transform.position;
        IsGrounded = Physics.SphereCast(castOrigin, suspensionCastRadius, Vector3.down, out RaycastHit hitInfo, groundCheckDistance, groundLayer);
        Debug.DrawRay(hipsRb.transform.position, Vector3.down * groundCheckDistance, Color.aliceBlue);

        if (IsGrounded)
        {

            float currentDistance = hitInfo.distance;

            float heightError = rideHeight - currentDistance;

            float springForce = heightError * rideSpringStrength;

            float currentVerticalVelocity = Vector3.Dot(hipsRb.linearVelocity, Vector3.up);

            float damperForce = -currentVerticalVelocity * rideSpringDamper;

            Vector3 finalForce = Vector3.up * (springForce + damperForce);

            hipsRb.AddForce(Vector3.up * (springForce + damperForce), ForceMode.Acceleration);

            if(hitInfo.transform.TryGetComponent<Rigidbody>(out Rigidbody hitRb))
            {
                hitRb.AddForce(hipsRb.mass * -0.98f * Vector3.up, ForceMode.Acceleration);
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
        Vector3 targetVelocity = moveDirection * (sprint? maxSprintSpeed :  maxWalkSpeed);

        Vector3 currentVelocity = new Vector3(hipsRb.linearVelocity.x, 0, hipsRb.linearVelocity.z);
        Vector3 velocityError = targetVelocity - currentVelocity;

        float forceMagnitude = _moveInput.sqrMagnitude > 0.01f ? acceleration : braking;
        Vector3 correctiveForce = velocityError * forceMagnitude;

        hipsRb.AddForce(correctiveForce, ForceMode.Acceleration);
    }

    private void UpdateTorsoAndHead()
    {
     
        for (int i = 0; i < pDSprings.Count; i++) { 
            var springs = pDSprings[i];
            springs.UpdateBoneDrive(i == 1 ? initialTorsoRot: initialHeadRot);
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

        foreach (var spring in pDSprings)
        {
            if (spring.nrb == null) return;

            Vector3 newWorldPos = targetPos ;
            Quaternion newWorldRot = rotation ;

            spring.nrb.Teleport(newWorldPos, newWorldRot);
            spring.nrb.Rigidbody.linearVelocity = Vector3.zero;
            spring.nrb.Rigidbody.angularVelocity = Vector3.zero;
        }

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
    }

    static bool IsFinite(Quaternion q) => float.IsFinite(q.x) && float.IsFinite(q.y) && float.IsFinite(q.z) && float.IsFinite(q.w);
    static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

    [System.Serializable]
    public class PDSpring
    {
        public ConfigurableJoint joint;
        public Transform target;

        public float angluarSpringForce = 100f;
        public float angularSpringDamp = 50f;

        public AnimationCurve angularErrorMultiplier = AnimationCurve.Linear(0f, 1f, 1f, 10f);

        public Quaternion startJointRotation;
        private Quaternion startTargetRotation;

        public bool wasKinematicOnDisable;
        public Transform ragdollEquivelent;

        [HideInInspector] public NetworkRigidbody3D nrb;
        [HideInInspector] public Vector3 localOffsetFromHips;
        [HideInInspector] public Quaternion localRotationFromHips;

        public void Init(Transform hipsTransform,bool hasStateAuth)
        {
            startJointRotation = joint.transform.localRotation;
            startTargetRotation = target.localRotation;

            localOffsetFromHips = hipsTransform.InverseTransformPoint(joint.transform.position);
            localRotationFromHips = Quaternion.Inverse(hipsTransform.rotation) * joint.transform.rotation;
            //joint.gameObject.GetComponent<NetworkObject>().RemoveInputAuthority();

            nrb = joint.GetComponent<NetworkRigidbody3D>();
        }

        public void UpdateBoneDrive(Quaternion startRot)
        {
            //Quaternion currentTargetLocalRot = target.localRotation;
            //Quaternion deltaRotation = currentTargetLocalRot * Quaternion.Inverse(startTargetRotation);

            joint.SetTargetRotationLocal(target.localRotation, startRot);
            float angleErr = Mathf.Abs(Quaternion.Angle(joint.transform.rotation, target.rotation)) /180;
            float forceMult = angularErrorMultiplier.Evaluate(angleErr);

            var drive = joint.slerpDrive;
            drive.positionSpring = angluarSpringForce * forceMult;
            drive.positionDamper = angularSpringDamp;
            joint.slerpDrive = drive;
        }
    }
}
