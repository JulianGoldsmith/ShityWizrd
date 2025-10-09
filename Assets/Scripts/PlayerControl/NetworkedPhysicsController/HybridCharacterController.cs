using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class HybridCharacterController : NetworkBehaviour
{
    [Header("Components")]
    public Rigidbody hipsRb;
    public Vector3 hipsOffset;

    [Header("Grounded Settings")]
    public float groundCheckDistance = 0.6f;
    public LayerMask groundLayer;
    [Networked] public bool IsGrounded { get; set; }

    [Header("Movement Settings")]
    public float maxSpeed = 3f;
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

    [Header("Network Input")]
    [HideInInspector][Networked] public Vector2 moveInput { get; set; }
    [HideInInspector][Networked] public Quaternion lookRot { get; set; }
    [HideInInspector][Networked] public NetworkButtons _lastButtonsInput { get; set; }
    [Networked] private int _jumpCount { get; set; }
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
    public SkinnedMeshRenderer modelRenderer;

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;
    public RagDollCameraController camController;

    [Header("Hands")]
    public NetworkedHandsController handController;

    private Vector3 previousVelocity;
    public Vector3 Acceleration { get; private set; }

    bool FIRSTFRAME = true;

    public override void Spawned()
    {
        _lastVisibleJump = _jumpCount;

        //general setup
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
       

        //material setup
        if (Object.HasStateAuthority)
        {
            isHost = Object.HasInputAuthority;
            bodyRot = hipsRb.transform.rotation;
        }
        modelRenderer.material = isHost ? hostMat : clientMat;
        
        if (HasInputAuthority)
        {
            modelRenderer.enabled = false;
        }

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
        foreach(var spring in pDSprings)
        {
            spring.Init();
        }

        //hands
        handController.Spawn(networkedRenderRoot, HasInputAuthority);

        if (HasInputAuthority)
        {
            GameController.Instance.playerInput = this.GetComponent<PlayerInput>();
        }
    }

   
    public override void FixedUpdateNetwork()
    {
        //Debug.Log($"NetworkUpdate for - Is Local = {HasInputAuthority} + {this.GetComponent<NetworkObject>().InputAuthority} + {isHost}");
        DetectVariablesChangedOnNetwork();
        if (GetInput(out NetworkInputData data))
        {
            data.direction.Normalize();
            moveInput = new Vector2(data.direction.x, data.direction.z);
            lookRot = IsFinite(data.lookRotation) ? data.lookRotation : Quaternion.identity;
            

            if (data.buttons.WasPressed(_lastButtonsInput, EInputButton.JUMP))
            {
                ApplyJump(); //animation is applied in Render -> Update Animations()
                _jumpCount++;
            }
            _lastButtonsInput = data.buttons;
        }

  
        ApplyLookRotation(); //sets the hips rb to the rotation of the camera - currently hard sets this with no forces. 

        UpdateHips(); //applys suspention forces to the hips. 
        ApplyHipsMovement(); //applys input to aim for a target velocity along  x,z basically a PD to velocity

        UpdateTorsoAndHead();

        UpdateHands();

        Acceleration = (hipsRb.linearVelocity - previousVelocity) / Runner.DeltaTime;
        previousVelocity = hipsRb.linearVelocity;
    }

    public override void Render()
    {
        //transform.position = hipsRb.transform.position;
        //transform.rotation = hipsRb.transform.rotation;
        CasheMovement();
        UpdateAnimator();
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

        targetAnimator.SetFloat("forwardSpeed", localVel.z / (maxSpeed * 2), 0.1f, Time.deltaTime);
        targetAnimator.SetFloat("rightSpeed", localVel.x / (maxSpeed * 2), 0.1f, Time.deltaTime);
        targetAnimator.SetFloat("RotationSpeed", rendererYawSpeed, 0.1f, Time.deltaTime);
        
        //animationController.UpdateSpineIkTarget(lookRot);
        finalAnimator.SetFloat("forwardSpeed", localVel.z / (maxSpeed * 2), 0.1f, Time.deltaTime);
        finalAnimator.SetFloat("rightSpeed", localVel.x / (maxSpeed * 2), 0.1f, Time.deltaTime);
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

        UpdateSpineIK();
    }

    public void UpdateSpineIK()
    {
        Vector3 lookDir;

        if (HasInputAuthority)
        {
            lookDir = cameraTransform.forward;
        }
        else
        {
            lookDir = lookRot * Vector3.forward;
        }

        spineIKTarget.position = (rendererPos + headOffset) + (lookDir * 5);
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

       // Debug.Log($"Look ROtation Running - Is Local = {HasInputAuthority} + {this.GetComponent<NetworkObject>().InputAuthority}");

        var _lookRot = HasInputAuthority ? cameraTransform.rotation : lookRot;

        Vector3 flatLookDir = Vector3.ProjectOnPlane(_lookRot * Vector3.forward, Vector3.up).normalized;
        Vector3 flatBodyDir = Vector3.ProjectOnPlane(networkedRenderRoot.rotation * Vector3.forward, Vector3.up).normalized;

        Quaternion targetBodyRot = networkedRenderRoot.rotation;

        Vector3 targetLookDir;

        bool isMoveing = moveInput.sqrMagnitude > 0.01f;
        if (isMoveing) 
        {
            //targetBodyRot = Quaternion.LookRotation(flatLookDir); // RotateToLookDirectionOnPlane(_lookRot);
            targetLookDir = flatLookDir;
        }
        else 
        {
            //targetBodyRot = RotateToLookDirectionOnPlane(_lookRot);
            /*
            float angleDiff = Vector3.SignedAngle(flatBodyDir, flatLookDir, Vector3.up);

            float turnRateMultiplier = turnBufferCurve.Evaluate(Mathf.Abs(angleDiff)/180);

            float turnAngle = turnRateMultiplier * turnStrength;
            //Mathf.Sign(angleDiff) * (Mathf.Abs(angleDiff) - lookBufferAngle);*/
            //targetBodyRot = Quaternion.LookRotation(flatLookDir);

            // targetBodyRot = RotateToLookDirectionOnPlane(_lookRot);
            //targetBodyRot = RotateToLookDirectionOnPlaneBuffered(flatBodyDir, flatLookDir, targetBodyRot);
            targetLookDir = GetBufferedTargetLookDir(flatBodyDir, flatLookDir);
        }

        float rotDifferenceInDegrees = Vector3.SignedAngle(flatBodyDir, targetLookDir, Vector3.up);  

        rotDifferenceInDegrees /= 180;


        float angularVelocityY = hipsRb.angularVelocity.y;

        float proportionalTorque = rotDifferenceInDegrees * turnStrength;
        float derivativeTorque = angularVelocityY * turnDamping;

        Vector3 torque = Vector3.up * (proportionalTorque - derivativeTorque);

        hipsRb.AddTorque(torque, ForceMode.Acceleration);
        //hipsRb.MoveRotation(targetBodyRot);
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
        hipsRb.AddForce(jumpForce * Vector3.up, ForceMode.VelocityChange);
        
        //Debug.Log("Jumped");
    }

    public void UpdateHips()
    {
        IsGrounded = Physics.Raycast(hipsRb.transform.position, Vector3.down, out RaycastHit hitInfo, groundCheckDistance, groundLayer);
        Debug.DrawRay(hipsRb.transform.position, Vector3.down * groundCheckDistance, Color.aliceBlue);
       
    }

    private void ApplyHipsMovement()
    {

        //Debug.Log($"Apply movement - Is Local = {HasInputAuthority} + {this.GetComponent<NetworkObject>().InputAuthority}");
        Vector2 _moveInput =   moveInput;
        Quaternion _lookRot = lookRot;
        _moveInput.Normalize();
        Vector3 camForward = _lookRot * Vector3.forward;
        camForward.y = 0;
        camForward.Normalize();

        Vector3 camRight = _lookRot * Vector3.right;
        camRight.y = 0;
        camRight.Normalize();

        Vector3 moveDirection = (camForward * _moveInput.y + camRight * _moveInput.x);
        Vector3 targetVelocity = moveDirection * maxSpeed;

        Vector3 currentVelocity = new Vector3(hipsRb.linearVelocity.x, 0, hipsRb.linearVelocity.z);
        Vector3 velocityError = targetVelocity - currentVelocity;

        float forceMagnitude = _moveInput.sqrMagnitude > 0.01f ? acceleration : braking;
        Vector3 correctiveForce = velocityError * forceMagnitude;

        hipsRb.AddForce(correctiveForce, ForceMode.Acceleration);
    }

    private void UpdateTorsoAndHead()
    {
        foreach(var springs in pDSprings)
        {
            springs.UpdateBoneDrive();
        }
    }

    private void UpdateHands()
    {

    }

    public Quaternion GetLookRot()
    {
        return HasInputAuthority ? cameraTransform.rotation : lookRot;
    }

    public Vector3 GetEyePos()
    {
        return hipsRb.transform.position + camController.localEyeOffset + camController.GetEyePosBasedOnPitch(HasInputAuthority?cameraTransform.rotation:lookRot);
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

        private Quaternion startJointRotation;
        private Quaternion startTargetRotation;

        public void Init()
        {
            startJointRotation = joint.transform.localRotation;
            startTargetRotation = target.localRotation;
        }

        public void UpdateBoneDrive()
        {
            //Quaternion currentTargetLocalRot = target.localRotation;
            //Quaternion deltaRotation = currentTargetLocalRot * Quaternion.Inverse(startTargetRotation);

            joint.SetTargetRotationLocal(target.localRotation, startJointRotation);
            float angleErr = Mathf.Abs(Quaternion.Angle(joint.transform.rotation, target.rotation)) /180;
            float forceMult = angularErrorMultiplier.Evaluate(angleErr);

            var drive = joint.slerpDrive;
            drive.positionSpring = angluarSpringForce * forceMult;
            drive.positionDamper = angularSpringDamp;
            joint.slerpDrive = drive;
        }
    }
}

