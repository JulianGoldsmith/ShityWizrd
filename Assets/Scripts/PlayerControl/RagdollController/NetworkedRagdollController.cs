using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Fusion.NetworkBehaviour;

public class NetworkedRagdollController : NetworkBehaviour
{
    [Header("Component References")]
    
    [SerializeField] private Transform animatedPelvisRoot;

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;
    public RagDollCameraController camController;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    public RagDollAnimationController animationController;

    [Header("LocalRagdoll")]
    public LocalActiveRagDoll localRagdoll;
    [SerializeField] private Rigidbody ragdollHipsRigidbody;
    public Rigidbody networkToLocalSpring;

    [Header("Networking")]
    [Networked] public Vector2 moveInput { get; set; }
    [Networked] public Quaternion lookRot { get; set; }
    [SerializeField] public Transform networkedRenderRoot;
    public Vector3 rendererPos;
    public Quaternion rendererRot;
    public Vector3 rendererVelocity;
    public Vector3 lastRendererPos;
    public float lastrenderTime;
    private ChangeDetector _changeDetector;

    [Header("Movement Parameters")]
    public float maxSpeed = 8f;
    [SerializeField] private float acceleration = 200f;
    [SerializeField] private float braking = 100f;
    [SerializeField] private float jumpForce = 20f;

    [Header("Ground Check & Suspension")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float rideHeight = 1.0f;
    [SerializeField] private float groundCheckDistance = 1.5f;
    [SerializeField] private float suspensionForce = 500f;
    [SerializeField] private float suspensionDamping = 30f;
    [SerializeField] private bool isGrounded;
    public bool IsGrounded => isGrounded;

    [Header("PhysicsHands")]
    public RagDollHandsController handsController;

    [Networked] public bool isHost { get; set; }
    public bool cashIsHost = false; 
    public Material hostMat, clientMat;
    public SkinnedMeshRenderer modelRenderer;

    [Header("Hips Follow PD Controller")]
    [SerializeField] private Vector3 targetHipsOffset = new Vector3(0, -0.2f, 0); // Offset from root to where hips should be

    private Rigidbody rootRigidbody;

    public override void Spawned()
    {
        //general setup
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        rootRigidbody = GetComponent<Rigidbody>();
        if (cameraTransform == null) cameraTransform = Camera.main.transform;
        lastrenderTime = Time.time;

        //material setup
        if (Object.HasStateAuthority)
        {
            isHost = Object.HasInputAuthority; 
        }
        modelRenderer.material = isHost ? hostMat : clientMat;

        //Camera Setup
        if (HasInputAuthority)
        {
            camController.enabled = true;
            camController.Spawned();
        }
        else
        {
            camController.enabled = false;
        }

        //HandsSetup
        handsController.OnSpawned(HasInputAuthority, rootRigidbody, networkedRenderRoot, this);
    }

    public override void FixedUpdateNetwork()
    {
        DetectVariablesChangedOnNetwork();
        if (GetInput(out NetworkInputData data))
        {
            data.direction.Normalize();
            moveInput = new Vector2(data.direction.x, data.direction.z);
            lookRot = data.lookRotation;
        }

        //if (HasStateAuthority)
        //{
            ApplyLookRotation(data);
            ApplySuspensionForce();
            HandleGroundedMovement(data);
        //}
    }

    public override void Render()
    {
       if(isHost != cashIsHost)
        {
            cashIsHost = isHost;
            modelRenderer.material = isHost ? hostMat : clientMat;
        }

        CasheMovement();
        UpdateAnimator();
        UpdateLocalRagDollPoisition();
    }

    public void DetectVariablesChangedOnNetwork()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(isHost):
                    modelRenderer.material = isHost? hostMat: clientMat;
                    break;
            }
        }
    }

    public void CasheMovement()
    {
        if (!networkedRenderRoot) return;

        // Read the smoothed proxy pose here
        rendererPos = networkedRenderRoot.position;
        rendererRot = networkedRenderRoot.rotation;

        // Approximate render-space velocity (good enough for PD damping)
        var p = networkedRenderRoot.position;
        rendererVelocity = (p - lastRendererPos) / (Time.deltaTime);
        lastRendererPos = p;
        lastrenderTime = Time.time;
    }

    void UpdateAnimator()
    {
        if (!animator) return;

        //locomotion 
        Vector3 localVel = transform.InverseTransformDirection(rendererVelocity);
        animator.SetFloat("forwardSpeed", localVel.z / (maxSpeed*2), 0.1f, Time.deltaTime);
        animator.SetFloat("rightSpeed", localVel.x / (maxSpeed*2), 0.1f, Time.deltaTime);

        //spine
        animationController.UpdateSpineIkTarget(lookRot);

    }

    void UpdateLocalRagDollPoisition()
    {
        /*networkToLocalSpring.transform.position = networkedRenderRoot.position + targetHipsOffset;
        networkToLocalSpring.transform.rotation = networkedRenderRoot.rotation;

        networkToLocalSpring.MovePosition(networkedRenderRoot.position + targetHipsOffset);
        networkToLocalSpring.MoveRotation(networkedRenderRoot.rotation);*/

        ragdollHipsRigidbody.transform.position = networkedRenderRoot.position + targetHipsOffset;
        ragdollHipsRigidbody.transform.rotation = networkedRenderRoot.rotation;

        ragdollHipsRigidbody.MovePosition(networkedRenderRoot.position + targetHipsOffset);
        ragdollHipsRigidbody.MoveRotation(networkedRenderRoot.rotation);
        //localRagdoll.UpdateHipsFollowPD(networkedRenderRoot);
    }

    #region networked RB

    void ApplyLookRotation(NetworkInputData data)
    {
        Quaternion lookRotation = data.lookRotation;
        Vector3 eulerAngles = lookRotation.eulerAngles;
        Quaternion targetRotation = Quaternion.Euler(0, eulerAngles.y, 0);
        rootRigidbody.MoveRotation(targetRotation);
    }

    void HandleGroundedMovement(NetworkInputData data)
    {
        Vector2 _moveInput = new Vector2(data.direction.x, data.direction.z);
        _moveInput.Normalize(); 
        Vector3 camForward = data.lookRotation * Vector3.forward;
        camForward.y = 0;
        camForward.Normalize();

        Vector3 camRight = data.lookRotation * Vector3.right;
        camRight.y = 0;
        camRight.Normalize();

        Vector3 moveDirection = (camForward * _moveInput.y + camRight * _moveInput.x);
        Vector3 targetVelocity = moveDirection * maxSpeed;

        Vector3 currentVelocity = new Vector3(rootRigidbody.linearVelocity.x, 0, rootRigidbody.linearVelocity.z);
        Vector3 velocityError = targetVelocity - currentVelocity;

        float forceMagnitude = _moveInput.sqrMagnitude > 0.01f ? acceleration : braking;
        Vector3 correctiveForce = velocityError * forceMagnitude;

        rootRigidbody.AddForce(correctiveForce, ForceMode.Acceleration);
    }

    void ApplySuspensionForce()
    {
        isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hitInfo, groundCheckDistance, groundLayer);

        if (isGrounded)
        {
            float heightDiff = rideHeight - hitInfo.distance;
            float upwardForce = heightDiff * suspensionForce;

            float currentVerticalVelocity = Vector3.Dot(rootRigidbody.linearVelocity, hitInfo.normal);
            float dampingForce = currentVerticalVelocity * suspensionDamping;

            Vector3 totalForce = (upwardForce - dampingForce) * Vector3.up;
            rootRigidbody.AddForce(totalForce, ForceMode.Acceleration);
        }
    }

    #endregion

}
