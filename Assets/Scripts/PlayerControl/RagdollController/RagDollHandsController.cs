using Fusion;
using System;
using System.Linq;
using UnityEngine;

public class RagDollHandsController : MonoBehaviour
{
    [Serializable]
    public class Hand
    {
        [Tooltip("The actual hands")]
        public Rigidbody physicsProxy;

        [Tooltip("The target")]
        public Transform handTarget;
        public Transform defaultAnchor;
        public Transform temporaryTarget;

        public Quaternion? temporaryTargetRotation;

        public HandState currentHandState;

        [HideInInspector] public Vector3 previousHandPosition;
        [HideInInspector] public Vector3 fixedUpdateHandDifVector;
        public float handBobTimer = 0f;

        public bool lockedToTarget = false;

        public HandAnimationController animationManager;



        public  void InitHand(bool isLeft, GameObject handContainer, GameObject camTargetContainer)
        {
            physicsProxy  = handContainer.GetComponentsInChildren<Rigidbody>(true).FirstOrDefault(x => x.name == (isLeft ? "LeftHand": "RightHand"));
            handTarget = camTargetContainer.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == (isLeft ? "LeftHandAnchor" : "RightHandAnchor"));
            previousHandPosition = handTarget.position;
            handBobTimer = isLeft? Mathf.PI : 0f;
            defaultAnchor = handTarget;
            animationManager = physicsProxy.GetComponentInChildren<HandAnimationController>();
            animationManager.animator = physicsProxy.GetComponentInChildren<Animator>();
        }
    }

    private GameObject handsContainer;
    private GameObject handsCameraTargetContainer;

    public Hand leftHand;
    public Hand rightHand;

    public NetworkedRagdollController controller;
    public Rigidbody characterRb;
    public Transform networkedRenderTarget;

    public Transform cameraTrans;

    public HandState defaultHandState, grabHandState;

    public GameObject handsPrefab;
    public GameObject targetToAttatchToCamera;

    bool isLocalAuthority;

    private Vector3 camPosNow, camPosPrev;
    private Quaternion camRotNow, camRotPrev;
    private bool _comCached = false;
    private Vector3 _comLocalOffset;

    public void OnSpawned(bool _isLocalAuthority,  Rigidbody rb, Transform renderTarget, NetworkedRagdollController c)
    {
        controller = c;
        isLocalAuthority = _isLocalAuthority;
        characterRb = rb;
        networkedRenderTarget = renderTarget;

        if (isLocalAuthority)
        {
            handsContainer = GameObject.Instantiate(handsPrefab, transform.position, transform.rotation);
            handsCameraTargetContainer = GameObject.Instantiate(targetToAttatchToCamera, Camera.main.transform.position, Camera.main.transform.rotation, Camera.main.transform);

            leftHand.InitHand(true, handsContainer, handsCameraTargetContainer);
            rightHand.InitHand(false, handsContainer, handsCameraTargetContainer);

            SetHandState(leftHand, defaultHandState);
            SetHandState(rightHand, defaultHandState);

            cameraTrans = Camera.main.transform;
            camPosNow = camPosPrev = cameraTrans.position;
            camRotNow = camRotPrev = cameraTrans.rotation;
        }

    }


    private void LateUpdate()
    {
        if (!isLocalAuthority) return;
            
        UpdateHandTarget(rightHand, false);
        UpdateHandTarget(leftHand, true);
        
    }

    public  void FixedUpdate()
    {
        if (!isLocalAuthority) return;

        CasheCameraPosAndRot();

        ApplyHandPhysics(rightHand);
        ApplyHandPhysics(leftHand);
        
    }

    private void UpdateHandTarget(Hand hand, bool isLeft)
    {
        if (hand.handTarget == null) return;

        if (hand.temporaryTarget != null)
        {
            if (hand.lockedToTarget)
            {
                hand.physicsProxy.transform.position = hand.temporaryTarget.position;
                hand.physicsProxy.transform.rotation = hand.temporaryTarget.rotation;
            }

            hand.handTarget.position = hand.temporaryTarget.position;
            hand.handTarget.rotation = hand.temporaryTarget.rotation;
            return;
        }


        Vector3 handBobOffset = CalculateBobOffset(hand);

        Vector3 handOffsetFromShoulder = hand.currentHandState.handOffsetFromShoulder;
        // mirror the X offset for the left hand
        Vector3 offset = new Vector3(
            isLeft ? -handOffsetFromShoulder.x : handOffsetFromShoulder.x,
            handOffsetFromShoulder.y,
            handOffsetFromShoulder.z
        );

        offset += handBobOffset;

        Vector3 targetPosition = cameraTrans.position + (cameraTrans.rotation * (offset));
        hand.handTarget.position = targetPosition;


        if (hand.temporaryTargetRotation.HasValue)
        {
            hand.handTarget.rotation = hand.temporaryTargetRotation.Value;
        }
        else
        {
            Vector3 finalRotationOffset = hand.currentHandState.handRotationOffset;
            if (isLeft)
            {
                finalRotationOffset.y *= -1;
                finalRotationOffset.z *= -1;
            }
            Quaternion offsetRotation = Quaternion.Euler(finalRotationOffset);
            hand.handTarget.rotation = cameraTrans.rotation * offsetRotation;
        }
    }

    private void CasheCameraPosAndRot()
    {
        camPosPrev = camPosNow;
        camRotPrev = camRotNow;
        camPosNow = cameraTrans.position;
        camRotNow = cameraTrans.rotation;
    }

    private void ApplyHandPhysics(Hand hand)
    {


        if (hand.physicsProxy == null || hand.handTarget == null) return;

        Rigidbody rb = hand.physicsProxy;

        if (hand.lockedToTarget)
        {
            rb.MovePosition(hand.handTarget.position);
            rb.MoveRotation(hand.handTarget.rotation);
            return;
        }


        Vector3 vCam = (camPosNow - camPosPrev) / Mathf.Max(Time.fixedDeltaTime, 1e-6f);
        Vector3 wCam = ComputeAngularVelocity(camRotPrev, camRotNow, Time.fixedDeltaTime);


       
        Vector3 xT = hand.handTarget.position;

        Vector3 r = xT - camPosNow;

        Vector3 vT = vCam + Vector3.Cross(wCam, r);

        Vector3 x = rb.position;
        Vector3 v = rb.linearVelocity;

        Vector3 posError = xT - x;
        float distance = posError.magnitude;
        float normDist = Mathf.Clamp01(distance / hand.currentHandState.maxDistance);
        float mult = hand.currentHandState.forceRelativeToDistance.Evaluate(normDist) * hand.currentHandState.maxStrengthMultiplier;

        float Kp = hand.currentHandState.positionSpringStrength * mult;
        float Kd = hand.currentHandState.positionSpringDamper;

        Vector3 accel = Kp * posError + Kd * (vT - v);

        // mass-independent
        rb.AddForce(accel, ForceMode.Acceleration);


        Quaternion q = rb.rotation;
        Quaternion qT = hand.handTarget.rotation;

        Quaternion dq = qT * Quaternion.Inverse(q);
        dq.ToAngleAxis(out float angDeg, out Vector3 axis);
        if (float.IsNaN(axis.x) || axis.sqrMagnitude < 1e-12f) axis = Vector3.up;
        if (angDeg > 180f) angDeg -= 360f;

        Vector3 angError = axis.normalized * (angDeg * Mathf.Deg2Rad);
        Vector3 w = rb.angularVelocity;

        float Kr = hand.currentHandState.rotationSpringStrength;
        float Dr = hand.currentHandState.rotationSpringDamper;

        Vector3 angAccel = Kr * angError + Dr * (wCam - w);

        rb.AddTorque(angAccel, ForceMode.Acceleration);
        hand.fixedUpdateHandDifVector = (rb.position - hand.previousHandPosition) / Time.fixedDeltaTime;
        hand.previousHandPosition = rb.position;

    }

    private Vector3 CalculateBobOffset(Hand hand)
    {
        hand.handBobTimer += characterRb.linearVelocity.magnitude * Time.deltaTime * hand.currentHandState.handBobSpeed;

        float bobZ = Mathf.Sin(hand.handBobTimer) * hand.currentHandState.handBobAmountX;
        float bobY = (Mathf.Cos(hand.handBobTimer * 2f) * 0.5f + 0.5f) * hand.currentHandState.handBobAmountY;

        return new Vector3(0, bobY, bobZ);
    }

    public void SetTemporaryTarget(Hand hand, Transform temporaryTarget)
    {
        hand.temporaryTarget = temporaryTarget;
        SetHandState(hand, grabHandState);
    }

    public void ClearTemporaryTarget(Hand hand)
    {
        hand.temporaryTarget = null;
        SetHandState(hand, defaultHandState);
    }

    public void LockHandToTarget(Hand hand, Transform target)
    {
        if (hand == null) return;
        hand.lockedToTarget = true;
        hand.temporaryTarget = target;
        hand.physicsProxy.isKinematic = true;
    }

    public void ReleaseHand(Hand hand)
    {
        if (hand == null) return;
        hand.lockedToTarget = false;
        hand.temporaryTarget = null;
        hand.physicsProxy.isKinematic = false;
    }

    public Vector3 CalculateHandOffset(Hand hand, bool isLeft)
    {
        Vector3 handOffsetFromShoulder = hand.currentHandState.handOffsetFromShoulder;
        Vector3 offset = new Vector3(
            isLeft ? -handOffsetFromShoulder.x : handOffsetFromShoulder.x,
            handOffsetFromShoulder.y,
            handOffsetFromShoulder.z
        );

        offset = cameraTrans.position + (cameraTrans.rotation * (offset));
        return offset;
    }

    public void SetTemporaryRotation(Hand hand, Vector3 worldDirection)
    {
        if (hand == null) return;

        Vector3 currentUp = Vector3.up;

        Quaternion aimRotation = Quaternion.LookRotation(worldDirection, currentUp);

        hand.temporaryTargetRotation = aimRotation;
    }

    public void ClearTemporaryRotation(Hand hand)
    {
        hand.temporaryTargetRotation = null;
    }

    public void SetHandState(Hand hand, HandState newState)
    {
        hand.currentHandState = newState;

        if (hand.animationManager != null)
        {
            hand.animationManager.ApplyHandStateAnimations(newState);
        }
    }


    private Vector3 ComputeAngularVelocity(Quaternion previousRot, Quaternion currentRot, float dt)
    {
        if (dt <= 1e-6f) return Vector3.zero;
        
        Quaternion rotationChaneg = currentRot * Quaternion.Inverse(previousRot);
        rotationChaneg.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (float.IsNaN(axis.x) || axis.sqrMagnitude < 1e-12f) return Vector3.zero;

        if (angleDeg > 180f) angleDeg -= 360f;
        float angleRad = angleDeg * Mathf.Deg2Rad;
        return axis.normalized * (angleRad / dt); // ω in rad/s
    }
}


