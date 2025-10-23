using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;

public class AnimationIkController : MonoBehaviour
{
    [SerializeField] private bool enableFootIK = true;

    public float footOffset;

    [SerializeField] private LayerMask groundLayer;

    public float leftIKFootWeight, rightIKFootWeight;
    public float footExtensionLenght;

    public HybridCharacterController characterController;

    public Animator animator;

    private void OnAnimatorIK(int layerIndex)
    {
        
        if (!enableFootIK || !characterController.IsGrounded)
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
        float weightToApply = animator.GetFloat(foot == AvatarIKGoal.LeftFoot ? "IKLeftFootWeight" : "IKRightFootWeight");
        animator.SetIKPositionWeight(foot, weightToApply);
        animator.SetIKRotationWeight(foot, weightToApply);

        Transform footTransform = animator.GetBoneTransform(foot == AvatarIKGoal.LeftFoot ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
        Vector3 animatedFootPosition = footTransform.position;


        RaycastHit hit;
        Ray ray = new Ray(animatedFootPosition + Vector3.up * 0.5f, Vector3.down);
        if (Physics.Raycast(ray, out hit, 0.5f * footExtensionLenght + footOffset, groundLayer))
        {
            Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.green * weightToApply);

            animator.SetIKPosition(foot, hit.point + new Vector3(0, footOffset, 0));

            Quaternion footRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, hit.normal), hit.normal);
            animator.SetIKRotation(foot, footRotation);
        }
        else
        {
            Debug.DrawRay(ray.origin, ray.direction * 1.5f, Color.red);
        }
    }
}
