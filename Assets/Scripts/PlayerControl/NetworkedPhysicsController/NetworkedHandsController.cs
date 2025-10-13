using Fusion;
using UnityEngine;
using UnityEngine.Animations.Rigging;


public enum TargetingMode { ARMATURE, HOLD, PICKUP, DRAGG };
public enum ActiveMeshState { FULLBODY, NORIGHTARM, NOLEFTARM, NOARMS }
public class NetworkedHandsController : NetworkBehaviour
{
    [System.Serializable]
    public class NetHand
    {
        [System.Serializable]
        public class HandPysics
        {
            public Transform transform;
            [HideInInspector] public Vector3 velocity;
            [HideInInspector] public Vector3 angularVelocity;
        }

        public HandPysics transformNet; //the networked hand position

        public HandPysics transformLocal; // the local smoothed transform

        public Transform armatureTarget; //the posisiton of the hand in the animated armature

        public Transform shoulderTransform; //the shoulder

        public Transform palmTransform;

        public GameObject visableHandObject;
        [HideInInspector] public SkinnedMeshRenderer handRenderer;


        public Vector3 localVelocity;
        public Quaternion localAngularVelocity;

        [Header("Dragging Variables")]
       
        public Transform draggingTransform;

        [HideInInspector] public Vector3 localDragPoint;

        public Vector3 cashePalmOffset;

        [HideInInspector] public Vector3 currentFrametargetPos;
        [HideInInspector] public Quaternion currentFrametargetRot;
       
        //Temporary Targets
        [HideInInspector] public Transform temporaryTarget;
        [HideInInspector] public Quaternion? temporaryTargetRotation;

        [HideInInspector] public Animator animator;
        [HideInInspector] public AnimatorOverrideController overrideController;

        public TwoBoneIKConstraint armatureHandIK;
        [HideInInspector] public Transform casheDefaultHandIKTarget, temporaryArmtureHandTarget;
        public void InitHand( bool isL, HybridCharacterController controller, bool hasInputAuth)
        {

            isLeft = isL;
            armLength = Vector3.Distance(transformNet.transform.position, shoulderTransform.position);
            this.transformNet.transform.GetComponent<NetworkTransform>().SetAreaOfInterestOverride(controller.GetComponent<NetworkObject>());
            handRenderer = visableHandObject.GetComponentInChildren<SkinnedMeshRenderer>();

            animator = transformNet.transform.GetComponentInChildren<Animator>();
            overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
            animator.runtimeAnimatorController = overrideController;
            casheDefaultHandIKTarget = armatureHandIK.data.target;
            temporaryArmtureHandTarget = new GameObject($"tmpHandTransform {isLeft}").transform;
            temporaryArmtureHandTarget.parent = transformNet.transform;

            cashePalmOffset = transformNet.transform.position - palmTransform.position;
        }

        //Sim

        [HideInInspector] public float armLength;
        [HideInInspector] public bool isLeft;

        public bool enabled = true;
    }

    [SerializeField] public NetHand leftHand, rightHand;

    [Header("Default States")]
    public HandState defaultHandState, grabHandState;
    public NetworkedInventoryManager inventoryManager;

    [Header("Pick Up")]
    public AnimationCurve pickUpItemForceDistace;
    public float pickUpSpringStrength, pickUpDamp;
    public Vector3 pickUpItemRotOffset = new Vector3(0,90, 0);
    public Vector3 dragTargetOffset = new Vector3(0, 0, 0.35f);
    [Networked] public float DragDistance { get; set; }
    public Vector3 dragHandModelPosOffset = new Vector3(0, -0.2f, 0);

    public float dragStength = 50, dragDamp = 10, dragRange = 10;
    public AnimationCurve dragStrengthCurve;
    public float dragRotationalStrength = 50;
    public AnimationCurve dragRotationStrengthCurve = AnimationCurve.Linear(0, 0, 1, 1);
    public AnimationCurve dragPitchToHeightModifierCurve; 

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

    public bool handsEnabled = true;


    bool isInitilized = false;

    [Header("Animation/Renderer")]
    public SkinnedMeshRenderer charMeshRenderer;
    public ActiveMeshState activeMeshState;
    public Vector3 pickUpOffset; // small offset used so the palm of the hand goes to target pickup spot
    public RigBuilder rigBuilder;

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

    public void LateUpdate() //think of this as local 
    {
        if (!isInitilized) return;
        if (!hasInputAuthority) return;

        CalculateHandTarget(leftHand, true);
        CalculateHandTarget(rightHand, true);

        ApplyHandPhysics(leftHand, leftHand.transformLocal, out Quaternion newRotLLocal, out Vector3 newPosLLocal, false);
        ApplyHandPhysics(rightHand, rightHand.transformLocal, out Quaternion newRotRLocal, out Vector3 newPosRLocal, false);

        newPosLLocal = (LeftHandMode != TargetingMode.PICKUP && LeftHandMode != TargetingMode.DRAGG) ? CalculatPosInArmConstraint(leftHand, newPosLLocal) : newPosLLocal;
        newPosRLocal = (RightHandMode != TargetingMode.PICKUP && RightHandMode != TargetingMode.DRAGG) ? CalculatPosInArmConstraint(rightHand, newPosRLocal) : newPosRLocal;

        leftHand.transformLocal.transform.position = newPosLLocal;
        rightHand.transformLocal.transform.position = newPosRLocal;
        leftHand.transformLocal.transform.rotation = newRotLLocal;
        rightHand.transformLocal.transform.rotation = newRotRLocal;

    }

    public override void FixedUpdateNetwork()
    {
        if (!isInitilized) return;
        if (GetInput(out NetworkInputData data) && HasStateAuthority)
        {
            DragDistance += data.scroll;
            if(DragDistance > 20) DragDistance = 20;
            if (DragDistance < -0.85f) DragDistance = -0.85f;
        }

        //ValidateHandTarget(leftHand);
        //ValidateHandTarget(rightHand);

        CalculateHandTarget(leftHand, false);
        CalculateHandTarget(rightHand, false);  //networkd and local are the same

        //Networked hand update
        ApplyHandPhysics(leftHand, leftHand.transformNet,  out Quaternion newRotLNet, out Vector3 newPosLNet, true);
        ApplyHandPhysics(rightHand, rightHand.transformNet, out Quaternion newRotRNet, out Vector3 newPosRNet, true);

        newPosLNet = (LeftHandMode != TargetingMode.PICKUP && LeftHandMode != TargetingMode.DRAGG) ? CalculatPosInArmConstraint(leftHand, newPosLNet): newPosLNet;
        newPosRNet= (RightHandMode != TargetingMode.PICKUP && RightHandMode != TargetingMode.DRAGG) ? CalculatPosInArmConstraint(rightHand, newPosRNet): newPosRNet;

        leftHand.transformNet.transform.position = newPosLNet;
        rightHand.transformNet.transform.position = newPosRNet; 
        leftHand.transformNet.transform.rotation = newRotLNet;
        rightHand.transformNet.transform.rotation = newRotRNet;

        //Local hand physics
    } //only on the client with input auth or host 

    public override void Render()
    {
        DetermineHandVisability();

        ApplyShowHidArms();

        DeterminHandPose(true);
        DeterminHandPose(false);

        SwitchToLocalOrNetHands(leftHand, LeftHandMode, HasInputAuthority, HasStateAuthority);
        SwitchToLocalOrNetHands(rightHand, RightHandMode, HasInputAuthority, HasStateAuthority);
    }

    private void ValidateHandTarget(NetHand hand)
    {
        TargetingMode targetingMode = hand.isLeft ? LeftHandMode : RightHandMode;

        var inventoryManager = GetComponent<NetworkedInventoryManager>();

        switch (targetingMode)
        {
            case TargetingMode.HOLD:
                if(inventoryManager.currentItemInHand == null)
                {
                    if(hand.isLeft)
                        LeftHandMode = TargetingMode.ARMATURE;
                    else
                        RightHandMode = TargetingMode.ARMATURE;
                }
                break;


            case TargetingMode.PICKUP:
                if (inventoryManager.potentialItemToPickup == null)
                {
                    if (hand.isLeft)
                        LeftHandMode = TargetingMode.ARMATURE;
                    else
                        RightHandMode = TargetingMode.ARMATURE;
                }
                break;


            case TargetingMode.DRAGG:
                break;


            case TargetingMode.ARMATURE:
                break;
            
        }
    }

    private void CalculateHandTarget(NetHand hand, bool localHand)
    {
        HandState state = hand.isLeft ? leftHandState : rightHandState;

        if (state == null) return;

        Vector3 targetPos;
        Quaternion targetRot;

        switch (hand.isLeft ? LeftHandMode : RightHandMode)
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

                targetPos = hand.armatureTarget.position;
                targetRot = hand.armatureTarget.rotation;

                break;



            case TargetingMode.PICKUP:

                Transform target = hand.temporaryTarget;

                if (inventoryManager.potentialItemToPickup.TryGetComponent<EquipableItem>(out EquipableItem equipable))
                {
                    target = equipable.primaryHandle;
                }

                Vector3 pivotToPalmOffset = hand.transformNet.transform.position - hand.palmTransform.position;
                targetPos = target.position + pivotToPalmOffset;
                targetRot = target.rotation;

                break;




            case TargetingMode.DRAGG:
                //targetPos = hand.draggingTransform.TransformPoint(hand.localDragPoint);
                if ((hand.draggingTransform == null && inventoryManager.potentialItemToPickup == null)
                    || (inventoryManager.localHandPosOnItem == null && hand.localDragPoint == null))
                {
                    targetPos = hand.armatureTarget.position;
                    targetRot = hand.armatureTarget.rotation;
                    if (HasStateAuthority)
                    {

                        if (hand.isLeft)
                            LeftHandMode = TargetingMode.ARMATURE;
                        else
                            RightHandMode = TargetingMode.ARMATURE;
                    }
                }

                Transform item = hand.draggingTransform;
                Vector3 localPointOnItem = hand.localDragPoint;
                if (inventoryManager.potentialItemToPickup != null) //if they exsist use the overwrite with the network variables
                {
                    item = inventoryManager.potentialItemToPickup.gameObject.transform;
                    localPointOnItem = inventoryManager.localHandPosOnItem;
                }

                targetPos = item.TransformPoint(localPointOnItem);

                Vector3 forward = (item.position - targetPos).normalized;

                targetRot = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0, -180, 0);

                Vector3 palmoffset =  (-forward * 0.1f);  

                targetPos += palmoffset;

                break;

            default:
                targetPos = hand.armatureTarget.position;
                targetRot = hand.armatureTarget.rotation;
                break;
        }

        hand.currentFrametargetPos = targetPos;
        hand.currentFrametargetRot = targetRot;
    }

    private void ApplyHandPhysics(NetHand hand, NetHand.HandPysics handObject, out Quaternion newRot, out Vector3 newPos, bool inFixedTime)
    {
        float dt = inFixedTime ? Runner.DeltaTime : Time.deltaTime;

        bool inPickUpMode = (hand.isLeft ? LeftHandMode == TargetingMode.PICKUP : RightHandMode == TargetingMode.PICKUP);
        bool inDraggMode = (hand.isLeft ? LeftHandMode == TargetingMode.DRAGG : RightHandMode == TargetingMode.DRAGG);

        if (inPickUpMode )
        {
            if(Vector3.Distance(hand.currentFrametargetPos, handObject.transform.position) < 0.1f)
            {
                newPos = hand.currentFrametargetPos;
                newRot = hand.currentFrametargetRot;

                handObject.velocity = Vector3.zero;
                handObject.angularVelocity = Vector3.zero;

                return;
            }
        }


        //pos
        var state = hand.isLeft? leftHandState: rightHandState;

        Vector3 targetPos = hand.currentFrametargetPos;
        Quaternion targetRot = hand.currentFrametargetRot;

        Vector3 playerVelocity = characterController.hipsRb.linearVelocity;
        Vector3 inertialForce = -characterController.Acceleration * state.inertiaStrength;

        
        Vector3 positionError = targetPos - (handObject.transform.position);

        if (inPickUpMode || inDraggMode)
        {
            float distanceMultiplier = pickUpItemForceDistace.Evaluate(positionError.magnitude / 10);
            Vector3 proportionalForce = positionError * pickUpSpringStrength * distanceMultiplier;

            Vector3 derivativeForce = -(handObject.velocity - playerVelocity) * pickUpDamp;
            handObject.velocity += (proportionalForce + derivativeForce + inertialForce) * dt;
        }
        else 
        {
            float distanceMultiplier = state.forceRelativeToDistance.Evaluate(positionError.magnitude / state.maxDistance);
            Vector3 proportionalForce = positionError * state.positionSpringStrength * distanceMultiplier;

            Vector3 derivativeForce = -(handObject.velocity - playerVelocity) * state.positionSpringDamper;
            handObject.velocity += (proportionalForce + derivativeForce + inertialForce) * dt;
        }



        //rot
        Quaternion rotationError = targetRot * Quaternion.Inverse(handObject.transform.rotation);
        rotationError.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        Vector3 proportionalTorque = axis.normalized * (angle * Mathf.Deg2Rad) * state.rotationSpringStrength;

        
        Vector3 derivativeTorque = -(handObject.angularVelocity) * state.rotationSpringDamper;
        handObject.angularVelocity += (proportionalTorque + derivativeTorque) * dt;

        //apply
        newPos = handObject.transform.position + handObject.velocity * dt;
        newRot = Quaternion.AngleAxis(handObject.angularVelocity.magnitude * Mathf.Rad2Deg * dt, (handObject.angularVelocity).normalized) * handObject.transform.rotation;
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

            leftHand.draggingTransform = null;
            leftHand.temporaryTarget = null;
        }
        else
        {
            rightHandState = defaultHandState;
            RightHandMode = TargetingMode.ARMATURE;

            rightHand.draggingTransform = null;
            rightHand.temporaryTarget = null;
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
        hand.temporaryTarget = worldTarget;

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

       

    }

    public void SetHandTarget_ToPickUpPoint(bool isLeft, Transform worldTarget, HandState itemHandState)
    {
        var hand = isLeft ? leftHand : rightHand;
        hand.temporaryTarget = worldTarget;

        if (isLeft) LeftHandMode = TargetingMode.PICKUP;
        else RightHandMode = TargetingMode.PICKUP;

        SetHandPose(isLeft, itemHandState.targetPickUpClip);
    }

    public void SetHandTarget_ToDraggPoint(bool isLeft, DraggableItem item, Vector3 hitPoint) //sets the 
    {
        var hand = isLeft ? leftHand : rightHand;
        hand.draggingTransform = item.transform;
        hand.localDragPoint = hand.draggingTransform.InverseTransformPoint(hitPoint);

        if (isLeft) LeftHandMode = TargetingMode.DRAGG;
        else RightHandMode = TargetingMode.DRAGG;

        SetHandPose(isLeft, defaultHandState.idleClip);
    }



    #region attatch / detatch / show / hide arms / switch to local

    private void DetermineHandVisability()
    {
        float buffer = 1.3f;
        bool isLeftDetached = Vector3.Distance(leftHand.transformNet.transform.position, leftHand.shoulderTransform.position) > (leftHand.armLength * buffer);
        bool isRightDetached = Vector3.Distance(rightHand.transformNet.transform.position, rightHand.shoulderTransform.position) > rightHand.armLength * buffer;

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
        float alpha = 0.5f;

        if (!handsEnabled)
        {
            leftHand.handRenderer.enabled = false;
            rightHand.handRenderer.enabled = false;
            return;
        }


        switch (activeMeshState)
        {
            case ActiveMeshState.FULLBODY:
                propBlock.SetFloat("_RightArmAlpha", 1f);
                propBlock.SetFloat("_LeftArmAlpha", 1f);
                SetHandArmatureTargetToReachOrItem(false, false);
                SetHandArmatureTargetToReachOrItem(true, false);
                leftHand.handRenderer.enabled = hasInputAuthority ? true : false;
                rightHand.handRenderer.enabled = hasInputAuthority ? true : false;
                break;
            case ActiveMeshState.NORIGHTARM:
                propBlock.SetFloat("_RightArmAlpha", alpha);
                propBlock.SetFloat("_LeftArmAlpha", 1f);
                SetHandArmatureTargetToReachOrItem(false, true);
                SetHandArmatureTargetToReachOrItem(true, false);
                leftHand.handRenderer.enabled = hasInputAuthority ? true : false;
                rightHand.handRenderer.enabled = hasInputAuthority ? true : true;
                break;
            case ActiveMeshState.NOLEFTARM:
                propBlock.SetFloat("_RightArmAlpha", 1f);
                propBlock.SetFloat("_LeftArmAlpha", alpha);
                SetHandArmatureTargetToReachOrItem(false, false);
                SetHandArmatureTargetToReachOrItem(true, true);
                leftHand.handRenderer.enabled = hasInputAuthority ? true : true;
                rightHand.handRenderer.enabled = hasInputAuthority ? true : false;
                break;
            case ActiveMeshState.NOARMS:
                propBlock.SetFloat("_RightArmAlpha", alpha);
                propBlock.SetFloat("_LeftArmAlpha", alpha);
                SetHandArmatureTargetToReachOrItem(false, true);
                SetHandArmatureTargetToReachOrItem(true, true);
                leftHand.handRenderer.enabled = hasInputAuthority ? true : true;
                rightHand.handRenderer.enabled = hasInputAuthority ? true : true;
                break;
        }

        charMeshRenderer.SetPropertyBlock(propBlock);
    }

    private void SwitchToLocalOrNetHands(NetHand hand, TargetingMode tgMode, bool localPlayer, bool isServer)
    {
        if (!localPlayer)
        {
            hand.visableHandObject.transform.parent = hand.transformNet.transform;
        }
        else
        {
            switch (tgMode)
            {
                case (TargetingMode.PICKUP):
                    hand.visableHandObject.transform.parent = hand.transformNet.transform;
                    hand.visableHandObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0, -90, -90));
                    SwitchNetHandToRemoteTimeFrame(hand, true);
                    break;
                case TargetingMode.HOLD:
                case TargetingMode.ARMATURE:
                    hand.visableHandObject.transform.parent = hand.transformLocal.transform;
                    hand.visableHandObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0, -90, -90));
                    SwitchNetHandToRemoteTimeFrame(hand, false);
                    break;
                case (TargetingMode.DRAGG):
                    hand.visableHandObject.transform.parent = hand.transformLocal.transform;
                    hand.visableHandObject.transform.SetLocalPositionAndRotation(dragHandModelPosOffset, Quaternion.Euler(0, -90, -90));
                    SwitchNetHandToRemoteTimeFrame(hand, false);
                    break;
            }
        }
        //this is an offset for the model

    }

    private void SwitchNetHandToRemoteTimeFrame(NetHand hand, bool _enabel)
    {
        hand.transformNet.transform.GetComponent<NetworkObject>().ForceRemoteRenderTimeframe = _enabel;
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
                Collider[] nearbyItems = Physics.OverlapSphere(hand.transformNet.transform.position, 0.5f, characterController.GetComponentInChildren<NetworkedInventoryManager>().itemLayer);
                Transform closestTarget = null;
                float minDistance = float.MaxValue;
                foreach (var itemCollider in nearbyItems)
                {
                    float distance = Vector3.Distance(hand.transformNet.transform.position, itemCollider.transform.position);
                    if (distance < minDistance && itemCollider.GetComponent<EquipableItem>() != null)
                    {
                        minDistance = distance;
                        closestTarget = itemCollider.transform;
                    }
                }
                if (closestTarget != null)
                {
                    var item = closestTarget.GetComponent<EquipableItem>();
                    SetHandPose(isLeft, item.heldHandState.targetPickUpClip);
                }
                break;
            case TargetingMode.HOLD:
                SetHandPose(isLeft, defaultHandState.idleClip); /////////////////
                break;
            case TargetingMode.ARMATURE:
            case TargetingMode.DRAGG:
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
                //Debug.Log($"SetHand to {pose.name}");
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

    public void DisableHands()
    {
        handsEnabled = false;
    }

    public void EnableHands()
    {
        handsEnabled = true;
    }

    public Vector3 pickUpWristRotOffset;
    public void SetHandArmatureTargetToReachOrItem(bool isLeft, bool isReach) //orients the armature hand to either the "hand object" or direction to object. 
    {
        var hand = isLeft ? leftHand : rightHand;

        if (isReach)
        {
            Vector3 reachDir = (hand.transformNet.transform.position-hand.shoulderTransform.position).normalized;
          
            TargetingMode tM = isLeft? LeftHandMode: RightHandMode;
            reachDir *= hand.armLength * ((tM == TargetingMode.DRAGG) ? 0.85f : 1.2f);
            hand.casheDefaultHandIKTarget.position = hand.shoulderTransform.position + reachDir;
            hand.casheDefaultHandIKTarget.rotation = Quaternion.LookRotation(reachDir, Vector3.up) * Quaternion.Euler(pickUpWristRotOffset);
        }
        else 
        {
            hand.casheDefaultHandIKTarget.localPosition = Vector3.zero;
            hand.casheDefaultHandIKTarget.localRotation = Quaternion.identity;
        }
    }
}
