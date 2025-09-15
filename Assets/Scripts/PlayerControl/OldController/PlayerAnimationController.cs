using UnityEngine;
using UnityEngine.Animations.Rigging;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Main script controlling players animations. Works with player movement controller and player animation controller
/// Note this is attatched to the child object of the player containing the skinned mesh not the player root transform, this is so we can control root motion. 
/// </summary>
public class PlayerAnimationController : GenericAnimationController
{
    public Transform playerRoot;

    public CharacterController controller;

    public PlayerMovementController playerMovementController;

    public PlayerCastActionController playerCastActionController;

    public Transform camTransform;

    public float targetSmoothSpeed = 10f;

    public Transform target;

    [Header("Constraints")]
    public MultiAimConstraint headConstraint;
    public MultiAimConstraint spine1Constraint;
    public MultiAimConstraint spine2Constraint;
    public MultiAimConstraint chestConstraint;

    private float headWeightTarget, headWeightRef;
    private float spine1WeightTarget, spine1WeightRef;
    private float spine2WeightTarget, spine2WeightRef;
    private float chestWeightTarget, chestWeightRef;
    public float weightsSmoothing = 0.3f;

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (playerRoot == null) playerRoot = transform.root;
        if (controller == null) controller = playerRoot.GetComponent<CharacterController>();
        if (playerMovementController == null) playerMovementController = transform.parent.GetComponent<PlayerMovementController>();
        if (playerCastActionController == null) playerCastActionController = transform.parent.GetComponent<PlayerCastActionController>();
        if (camTransform == null) camTransform = Camera.main.transform;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void LateUpdate()
    {
        animator.SetFloat("LocomotionSpeedMult", playerMovementController.walkSpeed/4);
        Vector3 localVelocity = transform.InverseTransformDirection(new Vector3(playerMovementController.animatorVector.x, 0, playerMovementController.animatorVector.z).normalized * playerMovementController.currentSpeed);

        animator.SetFloat("XVelocity", localVelocity.x / (playerMovementController.walkSpeed*2));
        animator.SetFloat("ZVelocity", localVelocity.z / (playerMovementController.walkSpeed * 2));
        animator.SetBool("IsGrounded", controller.isGrounded);
        animator.SetFloat("YVelocity", playerMovementController.velocity.y);
        animator.SetBool("IsLockedOn", playerMovementController.isLockedOn);

        if (playerMovementController.isLockedOn)
        {
            if (playerCastActionController.isCasting) //is casting
            {
                if (playerCastActionController.isUpperBodyAction) //upperbody only
                {
                    LookRigSpineAt(controller.transform.position + (target.transform.position - controller.transform.position), targetSmoothSpeed);
                }
                else //fullbody cast
                {
                    ClearAllRigWeights();
                }
            }
            else //not casting
            {
                if (!playerMovementController.isSprinting)
                {
                    LookRigAt((playerMovementController.currentTarget!=null)? playerMovementController.currentTarget.transform.position : playerMovementController.CalculateCameraLookPoint(), 1, 0.8f, 0.6f, 0.3f, false, targetSmoothSpeed);
                }
                else
                {
                    ClearAllRigWeights();
                }
            }
        }
        else
        {
            if (playerCastActionController.isCasting) //is casting
            {
                if (playerCastActionController.isUpperBodyAction) //upperbody only
                {
                    LookRigSpineAt(controller.transform.position + controller.transform.forward*10, targetSmoothSpeed);
                }
                else //fullbody cast
                {
                    ClearAllRigWeights();
                }
            }
            else
            {
                if (!playerMovementController.isSprinting && Vector3.Dot(new Vector3(controller.transform.forward.x, 0f, controller.transform.forward.z).normalized, new Vector3(camTransform.forward.x, 0f, camTransform.forward.z).normalized) > 0f)
                {
                    LookRigAt(playerMovementController.CalculateCameraLookPoint(), 1, 0.2f, 0.2f, 0.1f, true, targetSmoothSpeed);
                }
                else
                {
                    ClearAllRigWeights();
                }
            }
        }

        SmoothWeights();
    }

    void OnAnimatorMove()
    {
        if (animator == null) return;
        if (playerCastActionController.isCasting && !playerCastActionController.isUpperBodyAction)
        {
            Vector3 rootDelta = animator.deltaPosition;

            rootDelta += Vector3.up * playerMovementController.velocity.y * Time.deltaTime;

            controller.Move(rootDelta);
            //Debug.Log("MovingPLayerWithRootMotion, moved" + rootDelta );
            playerRoot.rotation *= animator.deltaRotation;

            playerMovementController.animatorVector = rootDelta / Time.deltaTime;
        }
    }




    public void LookRigAt(Vector3 pos, float headWeight, float chestWeight, float upperSpineWeight, float lowerSpineWeight, bool inclueY, float targetSmoothSpeed)
    {
        headWeightTarget = headWeight;
        chestWeightTarget = chestWeight;
        spine2WeightTarget = upperSpineWeight;
        spine1WeightTarget = lowerSpineWeight;

        Vector3 targetPos = inclueY ? pos : new Vector3(pos.x, transform.position.y + 1.5f, pos.z);

        target.position = Vector3.Lerp(target.position, targetPos, Time.deltaTime * targetSmoothSpeed);
    }

    public void ClearAllRigWeights()
    {
        headWeightTarget = 0;
        chestWeightTarget = 0;
        spine2WeightTarget = 0;
        spine1WeightTarget = 0;
    }

    public void LookRigSpineAt(Vector3 pos, float targetSmoothSpeed)
    {
        headWeightTarget = 0;
        chestWeightTarget = 0;
        spine2WeightTarget = 1;
        spine1WeightTarget = 0.5f;

        Vector3 targetPos = new Vector3(pos.x, pos.y, pos.z);
        target.position = Vector3.Lerp(target.position, targetPos, Time.deltaTime * targetSmoothSpeed);
    }

    void SmoothWeights()
    {
        headConstraint.weight = Mathf.SmoothDamp(headConstraint.weight, headWeightTarget, ref headWeightRef, weightsSmoothing);
        chestConstraint.weight = Mathf.SmoothDamp(chestConstraint.weight, chestWeightTarget, ref chestWeightRef, weightsSmoothing);
        spine1Constraint.weight = Mathf.SmoothDamp(spine1Constraint.weight, spine1WeightTarget, ref spine1WeightRef, weightsSmoothing);
        spine2Constraint.weight = Mathf.SmoothDamp(spine2Constraint.weight, spine2WeightTarget, ref spine2WeightRef, weightsSmoothing);
    }

}
