using UnityEngine;
using UnityEngine.Animations.Rigging;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

public class GenericAnimationController : MonoBehaviour
{
    public Animator animator;
    [SerializeField]
    public AnimatorOverrideController overrideController;

    public CastActionController castActionController;

    public event Action<string> OnAnimationEventTriggered;

    private void Start()
    {
        //overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
        animator.runtimeAnimatorController = overrideController;
    }

    public void PlayAnimationActionState(AnimationClip clip, bool isUpperBody)
    {
        castActionController.isUpperBodyAction = isUpperBody;
        if (clip == null || overrideController == null) return;

        overrideController["DummyAction"] = clip;
        animator.runtimeAnimatorController = overrideController;

        if (isUpperBody)
        {
            animator.SetTrigger("PlayUpperBodyAction");
        }
        else
        {
            Debug.Log("Playe fullbody anim");
            animator.SetTrigger("PlayFullBodyAction");
        }
    }

    public void EnterLoopStateAfterDelay(AnimationClip clip, bool isUpperBody, float delay)
    {
        StartCoroutine(EnterAnimationLoopStateAfterDelay(clip, isUpperBody, delay));
    }

    private IEnumerator EnterAnimationLoopStateAfterDelay(AnimationClip clip, bool isUpperBody, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (castActionController.isCasting)
        {
            EnterAnimationLoopState(clip, isUpperBody);
        }

    }
    public void EnterAnimationLoopState(AnimationClip clip, bool isUpperBody)
    {
        castActionController.isUpperBodyAction = isUpperBody;
        if (clip == null || overrideController == null) return;

        // Use the AOC to replace the dummy animation
        overrideController["DummyChargeLoop"] = clip;
        animator.runtimeAnimatorController = overrideController;

        if (isUpperBody)
        {
            animator.SetBool("IsLoopingUpperBodyAction", true);
        }
        else
        {
            animator.SetBool("IsLoopingFullBodyAction", true);
        }

    }

    public void OnAnimationEvent(string eventName)
    {
        Debug.Log($"Animation Event Fired: {eventName}");
        OnAnimationEventTriggered?.Invoke(eventName);
    }
}
