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

[DefaultExecutionOrder(-5)]
public class HybridCharacterController : NetworkBehaviour, IAnimVarSpeed, IAnimVarDirection, IAnimVarGrounded, IAnimEventListener, IHasPhysicalCore
{
    [Header("Components")]
    public Rigidbody hipsRb;
    public Vector3 hipsOffset;
    public List<NetworkRigidbody3D> networkRigidbody3Ds = new List<NetworkRigidbody3D>();
    public List<NetworkRigidbody3D> networkRagdollRigidbody3Ds = new List<NetworkRigidbody3D>();
    public float totalMass;

    [Header("Armature Retargeter LErp")]
    public float strenght = 1f;
    public float armatureRetargetingLerp = 0;
    public bool retargetRagDoll = false;
    public ArmatureRetargeter armatureRetargeter;

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
    public NetworkAnimator netAnimator;
    public Animator targetAnimator, finalAnimator;
    public BoneMapper boneMapper;
    public Transform spineIKTarget;
    public Vector3 headOffset;
    public Transform armatureHipsRoot;
    private Vector3 armatureHipsStartOffset;
    public ArmatureRetargeter retargeter;

    [Header("PD armature")]
    public List<PDSpring> pDSprings = new List<PDSpring>();
     //this is a new list of "ghost rigidbodies" used only when in ragdoll mode or near

    [Networked] Quaternion initialTorsoRot { get; set; }
    [Networked] Quaternion initialHeadRot { get; set; }

    public List<PdBone> pdBones = new List<PdBone>();
    public List<PdBone> ragDollBones = new List<PdBone>();

    private float pdDesignDt  = 1f / 64f;

    [Header("Network Input")]
    [HideInInspector][Networked] public Vector2 moveInput { get; set; }
    [HideInInspector][Networked] public Quaternion lookRot { get; set; }
    [HideInInspector][Networked] public NetworkButtons _lastButtonsInput { get; set; }
    [Networked] public int LastJumpTick { get; set; }
    public float jumpSuspensionDuration = 0.2f;
    [Networked] private int _jumpCount { get; set; }
    [Networked] public bool sprint { get; set; }
    private int _lastVisibleJump;


    [Header("Network Pos")]
    [SerializeField] public Transform networkedRenderRoot;
    [SerializeField] public Transform smoothedNetworkedRenderRoot;
    [HideInInspector] public Vector3 rendererPos;
    [HideInInspector] public Quaternion rendererRot;
    [HideInInspector] public Vector3 rendererVelocity;
    [HideInInspector] public Vector3 rendererAccel;
    [HideInInspector] public float rendererYawSpeed;
    [HideInInspector] public Vector3 rendererAngularVel;
    [HideInInspector] public Vector3 lastRendererPos;
    [HideInInspector] Quaternion lastRendererRot;

    [HideInInspector] public Vector3 smoothRendererVelocity;
    [HideInInspector] public Vector3 smoothRendererAccel;
    [HideInInspector] public float smoothRendererYawSpeed;
    [HideInInspector] public Vector3 smoothRendererAngularVel;
    [HideInInspector] public Vector3 smoothLastRendererPos;
    [HideInInspector] Quaternion smoothLastRendererRot;

    [HideInInspector] public Vector3 lastFixedPos;
    [HideInInspector] public Vector3 calculatedFixedVel;
    [HideInInspector] public Vector3 lastFixedVel;
    [HideInInspector] public Vector3 calculatedFixedAccel;
    [HideInInspector] public float lastRenderDt;

    private bool _hasSimSample;
    [HideInInspector] Vector3 lastSimRendererPos;
    [HideInInspector] Vector3 currentSimRendererPos;
    [HideInInspector] Vector3 lastSimRendererVelocity;
    [HideInInspector] Vector3 renderedVelocity;
    [HideInInspector] Vector3 simRendererAcceleration;

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

    public XpbdConstraintSolver xpbdSolver;

    [Header("Proxy Extrapolation")]
    [Networked] public int AuthInputTick { get; set; }
    private int _lastInputTick;
    private Vector2 _previousSmoothedInput;
    private Vector2 _lastReceivedAuthoritativeInput;
    public float inputSmoothingFactor = 0.1f; // Your suggested 0.1 per tick
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
        }
        foreach (NetworkRigidbody3D nrb in networkRagdollRigidbody3Ds)
        {
            Runner.SetIsSimulated(nrb.Object, true);
        }

        armatureHipsStartOffset = new Vector3(armatureHipsRoot.transform.localPosition.x,
                armatureHipsRoot.transform.localPosition.y, armatureHipsRoot.transform.localPosition.z);
        netAnimator = GetComponent<NetworkAnimator>();
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
                //ApplyJump(); 
                TriggerJump();
            }

            if (data.buttons.WasPressed(_lastButtonsInput, EInputButton.SELF_BONK))
            {
                if (bonkController.BonkedState != BONKEDSTATE.BONKED)
                {
                    if (TryGetComponent<PlayerPhysicsObject>(out PlayerPhysicsObject PPO))
                    {
                        //GetBonked();
                        PPO.current_bonkedness = -50f;
                    }
                }
                    //GetBonked(); //animation is applied in Render -> Update Animations()
            }

            if (data.buttons.WasPressed(_lastButtonsInput, EInputButton.UN_SELF_BONK))
            {
                if (TryGetComponent<PlayerPhysicsObject>(out PlayerPhysicsObject PPO))
                {
                    PPO.current_bonkedness = 100f;
                }
                //GetUnBonked(); //animation is applied in Render -> Update Animations()
            }
            _lastButtonsInput = data.buttons;

            AuthInputTick = Runner.Tick;
        }


       
        if (bonkController.BonkedState != BONKEDSTATE.BONKED)
        {
            bool isJumping = CheckIsJumpingAndApplyJump();

            ApplyUprightTorque();

            ApplyLookRotation();

            CalculateObservedAccelerationAndVelocity();
            
            if(!isJumping)
                ApplyHipsSuspension();
            
            ApplyHipsHorizontalMovement();

            UpdateAnimator(true);

            UpdateSpineIK();
        }


        if (Runner.DeltaTime > 1e-6f)
        {
            Acceleration = (hipsRb.linearVelocity - previousVelocity) / Runner.DeltaTime;
        }
        else
        {
            Acceleration = Vector3.zero;
        }
        previousVelocity = hipsRb.linearVelocity;

        UpdateJointSolver();

        CalculateVelocityAndAcceleration();
    }

    public override void Render()
    {

        if (disableCC > 0){ return; }

        //UpdateCameraAnchor();
        CasheMovement();

        UpdateAnimator(false);

        if (armatureRetargeter != null)
        {
            armatureRetargeter.SetRagdollBlend(armatureRetargetingLerp);
        }
    }

    private int _lastSimSampleTick;
    //Because of networking / input decay etc.. the player observerd velocity between Last simulation ticks is different to the actual velocity.
    private void CalculateObservedAccelerationAndVelocity()
    {
        if (!Runner.IsLastTick) return;

        currentSimRendererPos = smoothedNetworkedRenderRoot.position;
        int currentTick = Runner.Tick;

        if (!_hasSimSample)
        {
            _hasSimSample = true;
            renderedVelocity = Vector3.zero;
            simRendererAcceleration = Vector3.zero;
            lastSimRendererVelocity = Vector3.zero;

            lastSimRendererPos = currentSimRendererPos;
            _lastSimSampleTick = currentTick;
            return;
        }

        // 2. How many ticks actually passed between this batch and the last batch?
        // (This fixes the frame-drop math explosions)
        int ticksAdvanced = currentTick - _lastSimSampleTick;
        if (ticksAdvanced <= 0) return;

        float timeElapsed = ticksAdvanced * Runner.DeltaTime;

        // 3. Calculate the batch velocity. 
        // This will bounce between ~1.4 (packet arrived) and ~0.6 (decaying).
        Vector3 batchVelocity = (currentSimRendererPos - lastSimRendererPos) / timeElapsed;

        if (batchVelocity.magnitude < 0.05f)
            batchVelocity = Vector3.zero;

        simRendererAcceleration = (batchVelocity - lastSimRendererVelocity) / timeElapsed;

        // 4. THE MAGIC: Extract the true average trajectory.
        // By heavily smoothing the bouncing batch velocities, we get the exact
        // true speed the character is sliding across the screen (1.0m/s).
        renderedVelocity = Vector3.Lerp(renderedVelocity, batchVelocity, 0.15f);

        lastSimRendererVelocity = batchVelocity;
        lastSimRendererPos = currentSimRendererPos;
        _lastSimSampleTick = currentTick;
    }

    private void CalculateVelocityAndAcceleration()
    {
        Vector3 pos = hipsRb.transform.position;
        calculatedFixedVel = (pos - lastFixedPos) / Runner.DeltaTime ;
        calculatedFixedAccel = (calculatedFixedVel - lastFixedVel) / Runner.DeltaTime;
        lastFixedPos = pos;
        lastFixedVel  = calculatedFixedVel;
        lastRenderDt = Time.deltaTime;
    }

    private bool CheckIsJumpingAndApplyJump()
    {
        int ticksSinceJump = Runner.Tick - LastJumpTick;

        int suspensionBlindTicks = Mathf.CeilToInt(jumpSuspensionDuration / Runner.DeltaTime);
        bool isJumpBlindWindow = ticksSinceJump <= suspensionBlindTicks;

        if (ticksSinceJump == 0)
        {
            hipsRb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        return isJumpBlindWindow;
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


    void UpdateAnimator(bool isSim)
    {
        
        if (netAnimator == null) return;
        

        Vector3 flatVel = isSim ? hipsRb.linearVelocity : renderedVelocity;
        flatVel.y = 0f;
        if (flatVel.magnitude < 0.05f)
            flatVel = Vector3.zero;

        Quaternion rot = isSim ? hipsRb.transform.rotation : networkedRenderRoot.transform.rotation;

        Vector3 flatForward = Vector3.ProjectOnPlane(rot * Vector3.forward, Vector3.up).normalized;
        Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward);

        float forwardSpeed = Vector3.Dot(flatVel, flatForward);
        float rightSpeed = Vector3.Dot(flatVel, flatRight);

        float targetForward = forwardSpeed / (maxWalkSpeed * 2f);
        float targetRight = rightSpeed / (maxWalkSpeed * 2f);

        //if (Object.IsProxy)
        //Debug.Log($"rendererVelocity x {rightSpeed} y {forwardSpeed} pos {networkedRenderRoot.position}");
        if (isSim)
        {
            netAnimator.SetSimFloat("VelocityY", targetForward);
            netAnimator.SetSimFloat("VelocityX", targetRight);

            netAnimator.SetSimBool("IsGrounded", IsGrounded);
            netAnimator.SetSimFloat("VerticalVelocity", hipsRb.linearVelocity.y);

            netAnimator.UpdatePhysicsAnimator(out Vector3 rmDeltaPos, out Quaternion rmDeltaRotout, out Vector3 rmAbsPos, out Quaternion rmAbsRot);

            //armatureRetargeter.animatedHipRootMotion = rmDeltaPos / Runner.DeltaTime;
        }
        else
        {
            netAnimator.SetRenderFloat("VelocityY", targetForward, 0.05f, Time.deltaTime);
            netAnimator.SetRenderFloat("VelocityX", targetRight, 0.05f, Time.deltaTime);

            netAnimator.SetRenderBool("IsGrounded", IsGrounded);
            netAnimator.SetRenderFloat("VerticalVelocity", renderedVelocity.y, 0.05f, Time.deltaTime);

            netAnimator.UpdateVisualAnimator(out Vector3 visualRmOffset, out Quaternion visualRmRot);

            if (armatureRetargeter != null)
            {
                armatureRetargeter.animatedHipRootMotion = visualRmOffset;
            }
        }


        UpdateAnimatorPos(isSim);

        /*if (retargeter != null)
        {
            retargeter.animatedHipRootMotion = (new Vector3(armatureHipsRoot.transform.localPosition.x,
                armatureHipsRoot.transform.localPosition.y, armatureHipsRoot.transform.localPosition.z) - armatureHipsStartOffset) * 0.01f;
            retargeter.animatedHipRotation = armatureHipsRoot.transform.localRotation;
        }*/

        //armatureRetargeter.SetRagdollBlend(armatureRetargetingLerp);
    }

    void UpdateAnimatorPos(bool isSim)
    {
        Vector3 targetPos;
        Quaternion targetRot;
        if (!isSim)
        {
            targetPos = smoothedNetworkedRenderRoot.position + hipsOffset;
            targetRot = smoothedNetworkedRenderRoot.rotation;
        }
        else
        {
            targetPos = hipsRb.transform.position + hipsOffset;
            targetRot = hipsRb.transform.rotation;
        }


        if (!IsFinite(targetPos))
        {
            Debug.Log($"<color=red> CRITIAL target RENDERER POS is INFINATE IN UPDATE ANIMATOR? ");
        }
        if (!IsFinite(targetRot))
        {
            Debug.Log($"<color=red> CRITIAL target RENDERER ROT is INFINATE IN UPDATE ANIMATOR? ");
        }

        targetAnimator.gameObject.transform.position = targetPos; 
        targetAnimator.gameObject.transform.rotation = targetRot;

        finalAnimator.gameObject.transform.position = targetPos;
            //+ ((new Vector3(armatureHipsRoot.transform.localPosition.x,
            //armatureHipsRoot.transform.localPosition.y, armatureHipsRoot.transform.localPosition.z)) /100); 
        finalAnimator.gameObject.transform.rotation = targetRot;
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
        var sp = smoothedNetworkedRenderRoot.position;
        var lastRendereVel = rendererVelocity;
        var smoothLastRendereVel = smoothRendererVelocity;

        Vector3 rawVel = (p - lastRendererPos) / dtRender;
        float smoothing = 1f - Mathf.Exp(-12f * dtRender);
        rendererVelocity = Vector3.Lerp(rendererVelocity, rawVel, smoothing);

        if (rendererVelocity.magnitude < 0.1f)
            rendererVelocity = Vector3.zero;

        //rendererVelocity = (p - lastRendererPos) / (Time.deltaTime);
        smoothRendererVelocity = (sp - smoothLastRendererPos) /(Time.deltaTime);
        rendererAccel = (rendererVelocity - lastRendereVel) / Time.deltaTime;
        smoothRendererAccel = (smoothRendererVelocity - smoothLastRendereVel) / Time.deltaTime;
        lastRendererPos = p;
        smoothLastRendererPos = sp;

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

    private int _lastReceivedLocalTick;
    public void DetectVariablesChangedOnNetwork()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(isHost):
                    modelRenderer.material = isHost ? hostMat : clientMat;
                    break;
                case nameof(AuthInputTick):

                    _lastReceivedLocalTick = Runner.Tick;
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
        if (!IsGrounded) return;

        hipsRb.AddForce(jumpForce * Vector3.up, ForceMode.VelocityChange);

        if (netAnimator != null)
        {
            netAnimator.SetTrigger("Jump");
        }
    }

    public void TriggerJump()
    {
        if (IsGrounded && Runner.Tick > LastJumpTick + Mathf.CeilToInt(jumpSuspensionDuration / Runner.DeltaTime))
        {
            LastJumpTick = Runner.Tick;

            if (netAnimator != null)
            {
                netAnimator.SetTrigger("Jump");
            }
        }
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
    public void ApplyHipsSuspension()
    {
        // IsGrounded = Physics.Raycast(hipsRb.transform.position, Vector3.down, out RaycastHit hitInfo, groundCheckDistance, groundLayer);

        Vector3 castOrigin = hipsRb.worldCenterOfMass;

        float distanceToCast = ((rideHeight * hipsRb.transform.localScale.z) + suspensionCastRadius + groundCheckExtraDistance);

        //Debug.DrawRay(castOrigin, Vector3.down* distanceToCast, Color.red);

        IsGrounded = Physics.SphereCast(castOrigin, suspensionCastRadius, Vector3.down, out RaycastHit hitInfo, distanceToCast* hipsRb.transform.localScale.z, groundLayer);
        Debug.DrawRay(castOrigin, Vector3.down * distanceToCast * hipsRb.transform.localScale.z, Color.aliceBlue);

        if (IsGrounded)
        {
            float currentDistance = hitInfo.distance;

            float heightError = (rideHeight * hipsRb.transform.localScale.z) - currentDistance;

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

    private void ApplyHipsHorizontalMovement()
    {

        Vector2 rawInput = moveInput;
        rawInput.Normalize();

        if (Object.IsProxy)
        {
            // deltaTicks is now the exact distance between the current simulation tick
            // and the exact tick the owning player actually pressed the button.
            int deltaTicks = Runner.Tick - _lastReceivedLocalTick;

            // Apply decay
            if (deltaTicks > BasicSpawner.Instance.graceTicks)
            {
                if (deltaTicks >= BasicSpawner.Instance.maxDecayTicks)
                {
                    rawInput = Vector2.zero;
                }
                else
                {
                    float decayProgress = (float)(deltaTicks - BasicSpawner.Instance.graceTicks) /
                                          (BasicSpawner.Instance.maxDecayTicks - BasicSpawner.Instance.graceTicks);
                    float multiplier = BasicSpawner.Instance.decayCurve.Evaluate(decayProgress);
                    rawInput *= multiplier;
                }
            }

            // --- 2. TEMPORAL SMOOTHING ---
            /*rawInput = Vector2.Lerp(_previousSmoothedInput, rawInput, inputSmoothingFactor);
            _previousSmoothedInput = rawInput;*/
        }

        

        //Debug.Log($"Apply movement - Is Local = {HasInputAuthority} + {this.GetComponent<NetworkObject>().InputAuthority}");
        //Vector2 _moveInput = moveInput;
        Quaternion _lookRot = lookRot;
        //_moveInput.Normalize();

        Vector3 camForward = _lookRot * Vector3.forward;
        camForward.y = 0;
        camForward.Normalize();

        Vector3 camRight = _lookRot * Vector3.right;
        camRight.y = 0;
        camRight.Normalize();
        //moveInput = BasicSpawner.Instance.ApplyProxyDecay(Object, moveInput);
        Vector3 moveDirection = (camForward * rawInput.y + camRight * rawInput.x);
        Vector3 targetVelocity = moveDirection * (sprint ? maxSprintSpeed : maxWalkSpeed);

        //this may seem a bit hackey but it seems to solve the overshoot caused by "constant" input prediction (ie predicing continue run)
        

        Vector3 currentVelocity = hipsRb.linearVelocity;
        if (!IsFinite(currentVelocity))
        {
            currentVelocity = Vector3.zero;
        }
        currentVelocity.y = 0;

        Vector3 velocityError = targetVelocity - currentVelocity;

        float forceMagnitude = rawInput.sqrMagnitude > 0.01f ? acceleration : IsGrounded?braking: braking/20f;
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
        //float dt = (float)Runner.DeltaTime;
        //int jointIterations = 1; 

        //for (int iteration = 0; iteration < jointIterations; iteration++)
        //{
        //    float subStep = dt / jointIterations;

        //    if (retargetRagDoll || bonkController.BonkedState == BONKEDSTATE.BONKED)
        //    {
        //        for (int i = 0; i < pdBones.Count; i++)
        //            pdBones[i].Step(subStep, 0, 1f);
        //        for (int i = 0; i < ragDollBones.Count; i++)
        //            ragDollBones[i].Step(subStep, 0, 1f);
        //    }
        //    else
        //    {
        //        for (int i = 0; i < pdBones.Count; i++)
        //            pdBones[i].Step(subStep, strenght, 1f);
        //    }
        //}
    }

    private void UpdateJointSolver()
    {

        if (xpbdSolver == null) return;
        
        float dt = Runner.DeltaTime;
        xpbdSolver.ApplyRotationalPD(bonkController.BonkedState == BONKEDSTATE.ALIVE? strenght: 0f, dt);
        xpbdSolver.Solve(dt, bonkController.BonkedState != BONKEDSTATE.ALIVE);
       // xpbdSolver.ApplyAnchorTorqueFromLambda(1, dt);
    }

    public Quaternion GetLookRot()
    {
        return HasInputAuthority ? cameraTransform.rotation : lookRot;
    }

    public Vector3 GetEyePos()
    {
        //if(HasInputAuthority)
        //    return hipsRb.transform.position + camController.localEyeOffset + camController.GetEyePosBasedOnPitch(HasInputAuthority ? cameraTransform.rotation : lookRot);
        //else
            return networkedRenderRoot.transform.position + camController.localEyeOffset + camController.GetEyePosBasedOnPitch(HasInputAuthority ? cameraTransform.rotation : lookRot);
        //return hipsRb.transform.position + camController.localEyeOffset + camController.GetEyePosBasedOnPitch(HasInputAuthority?cameraTransform.rotation:lookRot);
    }

    public EyePosAndLookDir GetEyePosAndLookDir()
    {
        Vector3 eyePos = GetEyePos();
        Quaternion look = GetLookRot();

        Vector3 fwd = look * Vector3.forward;
        Vector3 up = look * Vector3.up; // or Vector3.up if you want strict world-up

        return new EyePosAndLookDir(eyePos, fwd, up);
    }

    public Vector3 GetEyePosSim()
    {
        return hipsRb.transform.position + camController.localEyeOffset + camController.GetEyePosBasedOnPitch(lookRot);
    }

    public EyePosAndLookDir GetEyePosAndLookDirSim()
    {
        Vector3 eyePos = GetEyePosSim();
        Quaternion look = lookRot;

        Vector3 fwd = look * Vector3.forward;
        Vector3 up = look * Vector3.up;

        return new EyePosAndLookDir(eyePos, fwd, up);
    }

    public Vector3 GetEyePosSmoothed()
    {
        return smoothedNetworkedRenderRoot.position + camController.localEyeOffset + camController.GetEyePosBasedOnPitch(lookRot);
    }

    public EyePosAndLookDir GetEyePosAndLookDirSmoothed()
    {
        Vector3 eyePos = GetEyePosSmoothed();
        Quaternion look = lookRot;

        Vector3 fwd = look * Vector3.forward;
        Vector3 up = look * Vector3.up;

        return new EyePosAndLookDir(eyePos, fwd, up);
    }

    public float GetCurrentSpeed()
    {
        Vector3 flatVel = hipsRb.linearVelocity;
        flatVel.y = 0f; // Ignore vertical falling/jumping
        return flatVel.magnitude;
    } // Or moveInput.magnitude depending on your blend preference
    public Vector2 GetCurrentDirection() => moveInput;
    public bool AnimIsGrounded() => IsGrounded;

    public void OnDeterministicAnimEvent(string eventID, bool isResimulation)
    {
        // We will fill this in later when we do the spellcasting/hitboxes!
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
            nrb.Teleport(targetPos, rotation);
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

    private void OnDrawGizmosSelected()
    {
        if (pdBones == null) return;

        foreach (var b in pdBones)
        {
            if (b != null)
            {
                b.DrawAngularLimitGizmos();
            }
        }
        foreach (var b in ragDollBones)
        {
            if (b != null)
            {
                b.DrawAngularLimitGizmos();
            }
        }
    }

    [ContextMenu("PD Ragdoll/Bake Anchors - Use Each Target Transform")]
    private void ContextBakeAnchorsFromTargets()
    {

        if (pdBones == null || pdBones.Count == 0) { Debug.LogWarning("No PdBones to bake."); return; }

        for (int i = 0; i < pdBones.Count; i++)
        {
            var bone = pdBones[i];
            BakeBone(bone);
        }
        for (int i = 0; i < ragDollBones.Count; i++)
        {
            var bone = ragDollBones[i];
            BakeBone(bone);
        }

        Debug.Log("Baked PD anchors from each bone's targetTransform remember to save");

    }
    private void BakeBone(PdBone bone)
    {
        if (bone == null) return;
        if (bone.targetTransform == null)
        {
            Debug.LogWarning($"PdBone {bone}: targetTransform is missing, skipped.");
            return;
        }
        bone.BakeAnchorsFromWorldPivot(bone.targetTransform.position);
        //if (bone.childRigidbody == null)
        //{
        //    Debug.LogWarning($"PdBone {bone}: childRigidbody is missing, skipped.");
        //    return;
        //}
        bone.BakeAnchorsFromWorldPivot(bone.childRigidbody.transform.position);
    }

    public NetworkObject GetCoreNetworkObject()
    {
        return hipsRb.GetComponent<NetworkObject>();    
    }

    public Transform GetCoreTransform(bool smoothedTrans = false)
    {
        return smoothedTrans ? networkedRenderRoot.transform : hipsRb.transform;
    }

    public Rigidbody GetCoreRigidbody()
    {
        return hipsRb;
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




public interface IHasPhysicalCore
{
    NetworkObject GetCoreNetworkObject();
    Transform GetCoreTransform(bool smoothedTrans = false);
    Rigidbody GetCoreRigidbody();
}