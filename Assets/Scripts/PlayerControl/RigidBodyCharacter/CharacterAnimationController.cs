using System.Collections.Generic;
using UnityEngine;

public class CharacterAnimationController : GenericAnimationController
{

    private Character character;

    [SerializeField] private bool enableFootIK = true;

    public float footOffset;
    
    [SerializeField] private LayerMask groundLayer;

    public float leftIKFootWeight, rightIKFootWeight;
    public float footExtensionLenght;

    [SerializeField] private List<SkinnedMeshRenderer> firstPersonMeshes;
    [SerializeField] private List<SkinnedMeshRenderer> thirdPersonMeshes;


    [Header("Arm IK")]
    [SerializeField] private bool enableArmIK = true;
    [SerializeField] public Transform leftHandHolder;
    [SerializeField] public Transform rightHandHolder;

    private void Awake()
    {
        character = GetComponentInParent<Character>();
        character = GetComponentInParent<Character>();
    }

    private void Update()
    {
        UpdateAnimationParameters();
    }

    private void UpdateAnimationParameters()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(character.Rigidbody.linearVelocity);

        float forward = localVelocity.z;
        float right = localVelocity.x;

        animator.SetFloat("forwardSpeed", forward/ (character.GetComponent<CharacterMovementController>().walkSpeed * 2));
        animator.SetFloat("rightSpeed", right / (character.GetComponent<CharacterMovementController>().walkSpeed * 2));
    }

    private void OnAnimatorIK(int layerIndex)
    {

        if (enableArmIK && leftHandHolder != null && rightHandHolder != null)
        {
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandHolder.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandHolder.rotation);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandHolder.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rightHandHolder.rotation);

            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1f);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1f);
        }

        //Debug.Log("OnAnimatorIK is being called!");
        if (!enableFootIK || !character.IsGrounded)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0);

            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0);
            animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0);
            return;
        }



        AdjustFoot(AvatarIKGoal.LeftFoot, leftIKFootWeight, 1);
        AdjustFoot(AvatarIKGoal.RightFoot, rightIKFootWeight, 1);

    }

    private void AdjustFoot(AvatarIKGoal foot, float positionWeight, float rotationWeight)
    {
        float weightToApply = animator.GetFloat( foot == AvatarIKGoal.LeftFoot ? "IKLeftFootWeight": "IKRightFootWeight");
        animator.SetIKPositionWeight(foot, 1-weightToApply);
        animator.SetIKRotationWeight(foot, 1 - weightToApply);

        Transform footTransform = animator.GetBoneTransform(foot == AvatarIKGoal.LeftFoot ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
        Vector3 animatedFootPosition = footTransform.position;


        RaycastHit hit;
        Ray ray = new Ray(animatedFootPosition + Vector3.up*0.5f, Vector3.down);
        if (Physics.Raycast(ray, out hit, 0.5f *footExtensionLenght + footOffset, groundLayer))
        {
            Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.green*weightToApply);

            animator.SetIKPosition(foot, hit.point + new Vector3(0, footOffset, 0));

            Quaternion footRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, hit.normal), hit.normal);
            animator.SetIKRotation(foot, footRotation);
        }
        else
        {
            Debug.DrawRay(ray.origin, ray.direction * 1.5f, Color.red);
        }
    }

    public void ToggleViewChanged(CharacterCameraController.CameraState newState)
    {
        bool isFirstPerson = newState == CharacterCameraController.CameraState.FirstPerson;

        foreach (var mesh in firstPersonMeshes)
        {
            if (mesh != null) mesh.enabled = isFirstPerson;
        }

        foreach (var mesh in thirdPersonMeshes)
        {
            if (mesh != null) mesh.enabled = !isFirstPerson;
        }
    }
}


