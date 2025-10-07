using Fusion;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Analytics;


public enum TargetingMode { ARMATURE, HOLD, PICKUP };
public enum ActiveMeshState { FULLBODY, NORIGHTARM, NOLEFTARM, NOARMS }
public class NetworkedHandsController : NetworkBehaviour
{
    [System.Serializable]
    public class NetHand
    {
        public Transform transform;
        public Transform armatureTarget;
        public Transform shoulderTransform;
        public Transform palmTransform;

        public GameObject visableHandObject;
        public SkinnedMeshRenderer handRenderer;


        public Vector3 currentFrametargetPos;
        public Quaternion currentFrametargetRot;

        //Temporary Targets
        public Transform temporaryTarget;
        public Quaternion? temporaryTargetRotation;

        public Animator animator;
        public AnimatorOverrideController overrideController;
        public void InitHand( bool isL, HybridCharacterController controller, bool hasInputAuth)
        {

            isLeft = isL;
            armLength = Vector3.Distance(transform.position, shoulderTransform.position);
            this.transform.GetComponent<NetworkTransform>().SetAreaOfInterestOverride(controller.GetComponent<NetworkObject>());
            handRenderer = visableHandObject.GetComponentInChildren<SkinnedMeshRenderer>();

            animator = transform.GetComponentInChildren<Animator>();
            overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
            animator.runtimeAnimatorController = overrideController;
        }

        //Sim

        [HideInInspector] public float armLength;
        [HideInInspector] public bool isLeft;
    }

    [SerializeField] public NetHand leftHand, rightHand;

    [Header("Default States")]
    public HandState defaultHandState, grabHandState;

    [Header("Pick Up")]
    public AnimationCurve pickUpItemForceDistace;
    public float pickUpSpringStrength, pickUpDamp;
    public Vector3 pickUpRotOffset = new Vector3(0,90, 0);

    [Header("Cashe Values")]
    public HybridCharacterController characterController;
    public Transform playerRoot;
    public AnimationCurve armDistFalloff;

    [Header("Networking")]
    public bool hasInputAuthority;
    [Networked] public TargetingMode LeftHandMode { get; set; }
    [Networked] public TargetingMode RightHandMode { get; set; }
    [SerializeField] public HandState leftHandState;
    [SerializeField] public HandState rightHandState;

    [HideInInspector][Networked] public Vector3 velocityLeft { get; set; }
    [HideInInspector][Networked] public Vector3 velocityRight { get; set; }
    [HideInInspector][Networked] public Vector3 angularVelocityLeft { get; set; }
    [HideInInspector][Networked] public Vector3 angularVelocityRight { get; set; }
    bool isInitilized = false;

    [Header("Animation/Renderer")]
    public SkinnedMeshRenderer charMeshRenderer;
    public ActiveMeshState activeMeshState;
    public Vector3 pickUpOffset; // small offset used so the palm of the hand goes to target pickup spot

    public void Spawn(Transform _playerRoot, bool _hasInputAuthroity)
    {
        isInitilized = true;
        leftHand.InitHand( true, characterController, _hasInputAuthroity);
        rightHand.InitHand( false, characterController, _hasInputAuthroity);
        leftHandState = leftHandState ?? defaultHandState;
        rightHandState = rightHandState ?? defaultHandState;

        playerRoot = characterController.hipsRb.transform;
        hasInputAuthority= _hasInputAuthroity;
        charMeshRenderer = characterController.modelRenderer;
        activeMeshState = ActiveMeshState.FULLBODY;
        SetHandModelMaterial(charMeshRenderer.material);
    }

    public override void FixedUpdateNetwork()
    {
        if (!isInitilized) return;

        CalculateHandTarget(leftHand);
        CalculateHandTarget(rightHand);

        ApplyHandPhysics(leftHand, out Quaternion newRotL, out Vector3 newPosL);
        ApplyHandPhysics(rightHand, out Quaternion newRotR, out Vector3 newPosR);

        newPosL = LeftHandMode != TargetingMode.PICKUP ? CalculatPosInArmConstraint(leftHand, newPosL): newPosL;
        newPosR= RightHandMode != TargetingMode.PICKUP ? CalculatPosInArmConstraint(rightHand, newPosR): newPosR;

        leftHand.transform.position = newPosL;
        rightHand.transform.position = newPosR; 

        leftHand.transform.rotation = newRotL;
        rightHand.transform.rotation = newRotR;

       
    }

    public override void Render()
    {
        DetermineHandVisability();

        ApplyShowHidArms();

        DeterminHandPose(true);
        DeterminHandPose(false);
    }

    private void CalculateHandTarget(NetHand hand)
    {
        HandState state = hand.isLeft? leftHandState: rightHandState;
        if (state == null) return;
        Vector3 targetPos;
        Quaternion targetRot;

        switch (hand.isLeft?LeftHandMode:RightHandMode)
        {
            case TargetingMode.HOLD:
                Vector3 offset = state.handOffsetFromEyes;
                if (hand.isLeft) offset.x *= -1;

                Vector3 eyeOffset = characterController.camController.GetEyePosBasedOnPitch(characterController.lookRot);

                targetPos = playerRoot.position + characterController.camController.localEyeOffset +
                    eyeOffset + (characterController.lookRot * offset);

                Vector3 rotOffset = state.handRotationOffset;
                if (hand.isLeft) { rotOffset.y *= -1; rotOffset.z *= -1; }
                targetRot = characterController.lookRot * Quaternion.Euler(rotOffset);
                
                break;
            case TargetingMode.ARMATURE:
                
                //if (HasStateAuthority)
                //{
                    targetPos = hand.armatureTarget.position;
                    targetRot = hand.armatureTarget.rotation;
                //}
                //else
                //{
                //    targetPos = hand.transform.position; //bit of custom prediction?
                //    targetRot = hand.transform.rotation;
                //}
                break;
            case TargetingMode.PICKUP:

                Vector3 pivotToPalmOffset = hand.transform.position - hand.palmTransform.position;
                targetPos = hand.temporaryTarget.position + pivotToPalmOffset;
                targetRot = (hand.temporaryTarget != null) ? hand.temporaryTarget.rotation : hand.armatureTarget.rotation;
                break;


            default:
                targetPos = hand.armatureTarget.position;
                targetRot = hand.armatureTarget.rotation;
                break;
        }

        hand.currentFrametargetPos = targetPos;
        hand.currentFrametargetRot = targetRot;
    }

    private void ApplyHandPhysics(NetHand hand, out Quaternion newRot, out Vector3 newPos)
    {
        bool inPickUpMode = (hand.isLeft ? LeftHandMode == TargetingMode.PICKUP : RightHandMode == TargetingMode.PICKUP);


        if (inPickUpMode)
        {
            if(Vector3.Distance(hand.currentFrametargetPos, hand.transform.position) < 0.1f)
            {
                newPos = hand.currentFrametargetPos;
                newRot = hand.currentFrametargetRot;
                if (hand.isLeft)
                {
                    velocityLeft = Vector3.zero;
                    angularVelocityLeft = Vector3.zero;
                }
                else
                {
                    velocityRight = Vector3.zero;
                    angularVelocityRight = Vector3.zero;
                }
                return;
            }
        }


        //pos
        var state = hand.isLeft? leftHandState: rightHandState;
        Vector3 targetPos = hand.currentFrametargetPos;
        Quaternion targetRot = hand.currentFrametargetRot;

        Vector3 playerVelocity = characterController.hipsRb.linearVelocity;
        Vector3 inertialForce = -characterController.Acceleration * state.inertiaStrength;

        Vector3 positionError = targetPos - (hand.transform.position);

        if (inPickUpMode)
        {
                float distanceMultiplier = pickUpItemForceDistace.Evaluate(positionError.magnitude / 10);
                Vector3 proportionalForce = positionError * pickUpSpringStrength * distanceMultiplier;

                Vector3 derivativeForce = -((hand.isLeft ? velocityLeft : velocityRight) - playerVelocity) * pickUpDamp;
                if (hand.isLeft)
                    velocityLeft += (proportionalForce + derivativeForce + inertialForce) * Runner.DeltaTime;
                else
                    velocityRight += (proportionalForce + derivativeForce + inertialForce) * Runner.DeltaTime; 
        }
        else //avoid running physics sim in armature mode on client as there is desnc with armatures over net work - follow 
        {
            //if (HasStateAuthority || (hand.isLeft ? LeftHandMode == TargetingMode.HOLD : RightHandMode == TargetingMode.HOLD))
            //{
                float distanceMultiplier = state.forceRelativeToDistance.Evaluate(positionError.magnitude / state.maxDistance);
                Vector3 proportionalForce = positionError * state.positionSpringStrength * distanceMultiplier;

                Vector3 derivativeForce = -((hand.isLeft ? velocityLeft : velocityRight) - playerVelocity) * state.positionSpringDamper;
                if (hand.isLeft)
                    velocityLeft += (proportionalForce + derivativeForce + inertialForce) * Runner.DeltaTime;
                else
                    velocityRight += (proportionalForce + derivativeForce + inertialForce) * Runner.DeltaTime;
            //}
           
        }



        //rot
        Quaternion rotationError = targetRot * Quaternion.Inverse(hand.transform.rotation);
        rotationError.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        Vector3 proportionalTorque = axis.normalized * (angle * Mathf.Deg2Rad) * state.rotationSpringStrength;

        
        Vector3 derivativeTorque = -(hand.isLeft ? angularVelocityLeft : angularVelocityRight) * state.rotationSpringDamper;
        if(hand.isLeft)
            angularVelocityLeft += (proportionalTorque + derivativeTorque) * Runner.DeltaTime;
        else 
            angularVelocityRight += (proportionalTorque + derivativeTorque) * Runner.DeltaTime;

        //apply
        newPos = hand.transform.position + (hand.isLeft ? velocityLeft : velocityRight) * Runner.DeltaTime;
        newRot = Quaternion.AngleAxis((hand.isLeft ? angularVelocityLeft : angularVelocityRight).magnitude * Mathf.Rad2Deg * Runner.DeltaTime, (hand.isLeft ? angularVelocityLeft : angularVelocityRight).normalized) * hand.transform.rotation;
    }

    private Vector3 CalculatPosInArmConstraint(NetHand hand, Vector3 newPos)
    {
        Vector3 shoulderToHand = newPos - hand.shoulderTransform.position;
        float distance = shoulderToHand.magnitude;
        float dampStartDistance = hand.armLength * (0.97f); //this prevents arm snapping but probably needs a proper method

        if (distance > dampStartDistance)
        {
            float overshootDistance = distance - dampStartDistance;

            float dampZoneSize = hand.armLength - dampStartDistance;

            float normalizedOvershoot = Mathf.Clamp01(overshootDistance / dampZoneSize);

            float dampEffect = armDistFalloff.Evaluate(normalizedOvershoot);

            float newDist = dampStartDistance + (dampZoneSize * dampEffect);

            newPos = hand.shoulderTransform.position + (shoulderToHand.normalized * dampStartDistance /*newDist*/);
           //Debug.Log("HandPastLimit");
        }

        return newPos;
    }

    public void SetHandTarget_ToArmature(bool isLeft)
    {
        var hand = isLeft ? leftHand : rightHand;


        if (isLeft)
        {
            leftHandState = defaultHandState;
            LeftHandMode = TargetingMode.ARMATURE;
        }
        else
        {
            rightHandState = defaultHandState;
            RightHandMode = TargetingMode.ARMATURE;
        }

        SetHandPose(isLeft, defaultHandState.idleClip);
    }

    public void SetHandTarget_ToHold(bool isLeft, HandState holdState)
    {
        var hand = isLeft ? leftHand : rightHand;

        if (isLeft)
        {
            leftHandState = holdState;
            LeftHandMode = TargetingMode.HOLD;
            
        }
        else
        {
            rightHandState = holdState;
            RightHandMode = TargetingMode.HOLD;

        }
        SetHandPose(isLeft, holdState.holdClip);
    }

    public void SetHandTarget_ToWorldPoint(bool isLeft, Transform worldTarget)
    {
        var hand = isLeft ? leftHand : rightHand;


        if (isLeft)
        {
            leftHandState = grabHandState;
            LeftHandMode = TargetingMode.PICKUP;
        }
        else
        {
            rightHandState = grabHandState;
            RightHandMode = TargetingMode.PICKUP;
        }

        hand.temporaryTarget = worldTarget;

    }

    public void SetHandTarget_ToPickUpPoint(bool isLeft, Transform worldTarget, HandState itemHandState)
    {
        var hand = isLeft ? leftHand : rightHand;
        hand.temporaryTarget = worldTarget;

        if (isLeft) LeftHandMode = TargetingMode.PICKUP;
        else RightHandMode = TargetingMode.PICKUP;

        SetHandPose(isLeft, itemHandState.targetPickUpClip);
    }

    //public void AttachItemToHand(bool isLeft, Item item)
    //{
    //    Rigidbody itemRb = item.GetComponent<Rigidbody>();
    //    itemRb.isKinematic = true;
    //    itemRb.GetComponent<Collider>().enabled = false;

    //    var hand = isLeft ? leftHand : rightHand;

    //    Transform snapPoint = hand.palmTransform;
        
    //    item.transform.SetParent(snapPoint);
    //}

    //public void DetachItemFromHand(bool isLeft)
    //{
    //    var hand = isLeft ? leftHand : rightHand;
    //}



    #region attatch / detatch / show / hide arms 

    private void DetermineHandVisability()
    {
        float buffer = 1.3f;
        bool isLeftDetached = Vector3.Distance(leftHand.transform.position, leftHand.shoulderTransform.position) > (leftHand.armLength * buffer);
        bool isRightDetached = Vector3.Distance(rightHand.transform.position, rightHand.shoulderTransform.position) > rightHand.armLength * buffer;

        SetArmState(isRightDetached, isLeftDetached);
    }

    public void SetArmState(bool rightHandDetached, bool leftHandDetached)
    {
        if (rightHandDetached && leftHandDetached)
        {
            activeMeshState = ActiveMeshState.NOARMS;
        }
        else if (rightHandDetached)
        {
            activeMeshState = ActiveMeshState.NORIGHTARM;
        }
        else if (leftHandDetached)
        {
            activeMeshState = ActiveMeshState.NOLEFTARM;
        }
        else
        {
            activeMeshState = ActiveMeshState.FULLBODY;
        }
    }

    private void ApplyShowHidArms()
    {
        var propBlock = new MaterialPropertyBlock();
        charMeshRenderer.GetPropertyBlock(propBlock);

        switch (activeMeshState)
        {
            case ActiveMeshState.FULLBODY:
                propBlock.SetFloat("_RightArmAlpha", 1f);
                propBlock.SetFloat("_LeftArmAlpha", 1f);
                leftHand.handRenderer.enabled = hasInputAuthority ? true : false;
                rightHand.handRenderer.enabled = hasInputAuthority ? true : false;
                break;
            case ActiveMeshState.NORIGHTARM:
                propBlock.SetFloat("_RightArmAlpha", 0f);
                propBlock.SetFloat("_LeftArmAlpha", 1f);
                leftHand.handRenderer.enabled = hasInputAuthority ? true : false;
                rightHand.handRenderer.enabled = hasInputAuthority ? true : true;
                break;
            case ActiveMeshState.NOLEFTARM:
                propBlock.SetFloat("_RightArmAlpha", 1f);
                propBlock.SetFloat("_LeftArmAlpha", 0f);
                leftHand.handRenderer.enabled = hasInputAuthority ? true : true;
                rightHand.handRenderer.enabled = hasInputAuthority ? true : false;
                break;
            case ActiveMeshState.NOARMS:
                propBlock.SetFloat("_RightArmAlpha", 0f);
                propBlock.SetFloat("_LeftArmAlpha", 0f);
                leftHand.handRenderer.enabled = hasInputAuthority ? true : true;
                rightHand.handRenderer.enabled = hasInputAuthority ? true : true;
                break;
        }

        charMeshRenderer.SetPropertyBlock(propBlock);
    }

    #endregion

    #region animations

    public void DeterminHandPose(bool isLeft)
    {
        var hand = isLeft ? leftHand : rightHand;
        var targetingMode = isLeft? LeftHandMode: RightHandMode;
        switch (targetingMode)
        {
            case TargetingMode.PICKUP: //////////THis is probably very jankey and could be dont alot better
                Collider[] nearbyItems = Physics.OverlapSphere(hand.transform.position, 0.5f, characterController.GetComponentInChildren<NetworkedInventoryManager>().itemLayer);
                Transform closestTarget = null;
                float minDistance = float.MaxValue;
                foreach (var itemCollider in nearbyItems)
                {
                    float distance = Vector3.Distance(hand.transform.position, itemCollider.transform.position);
                    if (distance < minDistance && itemCollider.GetComponent<Item>() != null)
                    {
                        minDistance = distance;
                        closestTarget = itemCollider.transform;
                    }
                }
                if (closestTarget != null)
                {
                    var item = closestTarget.GetComponent<Item>();
                    SetHandPose(isLeft, item.heldHandState.targetPickUpClip);
                }
                break;
            case TargetingMode.HOLD:
                SetHandPose(isLeft, defaultHandState.idleClip); /////////////////
                break;
            case TargetingMode.ARMATURE:
                SetHandPose(isLeft, defaultHandState.idleClip);
                break;

        }

    }

    public void SetHandPose(bool isLeft, AnimationClip pose)
    {
        var hand = isLeft ? leftHand : rightHand;

        if (pose != hand.overrideController["DummyPose"])
        {
            if (hand.overrideController != null && pose != null)
            {
                hand.overrideController["DummyPose"] = pose;
                // Debug.Log($"SetPose to pickup {pose.name}");
                Debug.Log($"SetHand to {pose.name}");
            }
        }
    }

    #endregion

    #region visuals

    public void SetHandModelMaterial(Material mat)
    {
        leftHand.handRenderer.material = mat;
        rightHand.handRenderer.material = mat;
    }
    #endregion
}
