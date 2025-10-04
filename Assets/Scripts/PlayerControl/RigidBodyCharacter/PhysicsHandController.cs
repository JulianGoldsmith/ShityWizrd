using System; 
using UnityEngine;
using Fusion;

public class PhysicsHandController : NetworkBehaviour
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
    }

    public Hand leftHand;
    public Hand rightHand;

    private Animator characterAnimator;
    private Rigidbody controller;

    //public Transform cameraTrans;
    Quaternion lookRotation;
    public Transform offset_anchor;

    public HandState defaultHandState, grabHandState;

    public override void Spawned()
    {
        // TODO
        // THIS IS HACKY AND NEEDS TO BE DONE PROPERLY LATER.
        NetworkObject hands = null;
        GameObject[] allhands = GameObject.FindGameObjectsWithTag("Hands");

        NetworkObject player = transform.parent.GetComponent<NetworkObject>();

        for (int i = 0; i < allhands.Length; i++)
        {
            // Check if the same input authority, if so it's a match and
            // use that.
            // Bit of a hack, need to fix/cleanup later.
            if (allhands[i].GetComponent<NetworkObject>().InputAuthority == player.InputAuthority)
            {
                hands = allhands[i].GetComponent<NetworkObject>();
                break;
            }
        }
        if (hands == null)
        {
            Debug.LogError("Hands not found");
            return;
        }

        offset_anchor = transform.parent.Find("Eyes");

        GameObject objlefthand = hands.transform.Find("LeftHand").gameObject;
        leftHand.physicsProxy = objlefthand.GetComponent<Rigidbody>();
        if (HasInputAuthority)
            leftHand.handTarget = GameObject.Find("LeftHandAnchor").transform;
        else
            leftHand.handTarget = offset_anchor;

        leftHand.defaultAnchor = leftHand.handTarget;
        leftHand.animationManager = objlefthand.GetComponentInChildren<HandAnimationController>();

        GameObject objrighthand = hands.transform.Find("RightHand").gameObject;
        rightHand.physicsProxy = objrighthand.GetComponent<Rigidbody>();
        if (HasInputAuthority)
            rightHand.handTarget = GameObject.Find("RightHandAnchor").transform;
        else
            rightHand.handTarget = offset_anchor;

        rightHand.defaultAnchor = rightHand.handTarget;
        rightHand.animationManager = objrighthand.GetComponentInChildren<HandAnimationController>();


        CharacterAnimationController cac = GetComponent<CharacterAnimationController>();
        cac.leftHandHolder = objlefthand.transform;
        cac.rightHandHolder = objrighthand.transform;
        

        InventoryManager iMan = GetComponentInParent<InventoryManager>();
        iMan.snapPoint = rightHand.physicsProxy.transform.Find("snappoint");

        Init();

        if (!HasInputAuthority)
            return;

        //cameraTrans = Camera.main.transform;

        CharacterCameraController ccc = Camera.main.GetComponent<CharacterCameraController>();
        ccc.inputController = GetComponentInParent<PlayerInputController>();
        ccc.characterMovementController = GetComponentInParent<CharacterMovementController>();
        ccc.animationController = cac;
        ccc.target = transform.parent;
        ccc.firstPersonAnchor = transform.parent.Find("Eyes");

        object[] sgcs = GameObject.FindObjectsOfTypeAll(typeof(SpellGraphController));
        if (sgcs.Length > 0)
        {
            (sgcs[0] as SpellGraphController).inventory = GetComponentInParent<InventoryManager>();
        }
    }

    private void Init() 
    {
        characterAnimator = GetComponent<Animator>();
        controller = characterAnimator.gameObject.GetComponentInParent<Rigidbody>();

        leftHand.previousHandPosition = leftHand.handTarget.position;
        rightHand.previousHandPosition = rightHand.handTarget.position;
        rightHand.handBobTimer = Mathf.PI;
        SetHandState(leftHand, defaultHandState);
        SetHandState(rightHand, defaultHandState);
        rightHand.handTarget = rightHand.defaultAnchor;
        leftHand.handTarget = leftHand.defaultAnchor;
    }

    [Networked] Vector3 left_hand_target { get; set; }
    [Networked] Vector3 right_hand_target { get; set; }

    private void LateUpdate()
    {
        if (!GameController.Instance.isEditorActive)
        {
            UpdateHandTarget(rightHand, false);
            UpdateHandTarget(leftHand, true);
        }
    }
    bool initialized_position = false;
    private void FixedUpdate()
    {
        if(!initialized_position)
        {            
            rightHand.physicsProxy.MovePosition(rightHand.handTarget.position);
            leftHand.physicsProxy.MovePosition(leftHand.handTarget.position);
            initialized_position = true;
        }



        if (!GameController.Instance.isEditorActive)
        {
            ApplyHandPhysics(rightHand,false);
            ApplyHandPhysics(leftHand, true);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            lookRotation = data.lookRotation;
        }
    }

    private void UpdateHandTarget(Hand hand, bool isLeft)
    {
        // CHANGED TO BE BASED ON INPUT LOOK DIRECTION
        // RATHER THAN CAMERA.
        // Should be consistent, but better for networking.
        if (hand.handTarget == null) return;

        if (hand.temporaryTarget != null)
        {
            if (hand.lockedToTarget)
            {
                hand.physicsProxy.transform.position = hand.temporaryTarget.position;
                hand.physicsProxy.transform.rotation = hand.temporaryTarget.rotation;
            }

            //hand.handTarget.position = hand.temporaryTarget.position;


            if (isLeft)
                left_hand_target = hand.temporaryTarget.position;
            else
                right_hand_target = hand.temporaryTarget.position;

            //hand.handTarget.rotation = hand.temporaryTarget.rotation;
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

        Vector3 targetPosition = offset_anchor.position + (lookRotation * (offset));

        if (isLeft)
            left_hand_target = targetPosition;
        else
            right_hand_target = targetPosition;

        //hand.handTarget.position = targetPosition;

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
            hand.handTarget.rotation = lookRotation * offsetRotation;
        }
    }

    private void ApplyHandPhysics(Hand hand, bool isLeft)
    {
        if (hand.physicsProxy == null || hand.handTarget == null) return;

        Rigidbody rb = hand.physicsProxy;
        Rigidbody body = controller;

        if(isLeft)
            rb.MovePosition(left_hand_target);
        else
            rb.MovePosition(right_hand_target);

        rb.MoveRotation(hand.handTarget.rotation);
        return;

        if (hand.lockedToTarget)
        {
            rb.MovePosition(hand.handTarget.position);
            rb.MoveRotation(hand.handTarget.rotation);
            return; 
        }


        // target positions (world)
        Vector3 x = rb.position;
        Vector3 xT = hand.handTarget.position;

        Vector3 r = xT - body.worldCenterOfMass;
        Vector3 vT = body.linearVelocity + Vector3.Cross(body.angularVelocity, r);

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
        if (float.IsNaN(axis.x)) axis = Vector3.up;
        if (angDeg > 180f) angDeg -= 360f;
        Vector3 angError = axis.normalized * (angDeg * Mathf.Deg2Rad); // radians

        Vector3 wT = body.angularVelocity;

        Vector3 w = rb.angularVelocity;

        float Kr = hand.currentHandState.rotationSpringStrength; 
        float Dr = hand.currentHandState.rotationSpringDamper;   

        Vector3 angAccel = Kr * angError + Dr * (wT - w);

        rb.AddTorque(angAccel, ForceMode.Acceleration);
        hand.fixedUpdateHandDifVector = (rb.position - hand.previousHandPosition) / Time.fixedDeltaTime;
        hand.previousHandPosition = rb.position;
        
    }

    private Vector3 CalculateBobOffset(Hand hand)
    {
        hand.handBobTimer += controller.linearVelocity.magnitude * Time.deltaTime * hand.currentHandState.handBobSpeed;

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

        offset = offset_anchor.position + (lookRotation * (offset));
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
}

