using UnityEngine;

public class HandAnimationController : MonoBehaviour
{
    public Animator animator;
    private AnimatorOverrideController overrideController;
    private void Awake()
    {
        animator = GetComponent<Animator>();
        overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
        animator.runtimeAnimatorController = overrideController;
    }

    public void ApplyHandStateAnimations(HandState state)
    {
        if (state == null) return;
        if (state.idleClip != null) overrideController["HandIdle"] = state.idleClip;
        if (state.windupClip != null) overrideController["ActionState"] = state.windupClip;
        if (state.holdClip != null) overrideController["LoopActionState"] = state.holdClip;
        if (state.releaseClip != null) overrideController["ReleaseActionState"] = state.releaseClip;
    }

    public void PlayAnimation(string triggerName)
    {
        animator.SetTrigger(triggerName);
    }

    public void PlayAnimationClip(AnimationClip clip)
    {
        overrideController["DummyAction"] = clip;
        animator.runtimeAnimatorController = overrideController;
    }
}
