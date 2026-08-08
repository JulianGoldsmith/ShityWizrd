using UnityEngine;

[CreateAssetMenu(fileName = "NPCAnimationAction", menuName = "AI Commands/Actions/Animation")]
public class NPCAnimationAction : NPCAction
{
    [Header("Animation")]
    [SerializeField] private string animationTrigger;

    [Header("Timing")]
    [SerializeField, TickDuration(1)] private int durationTicks = 32;

    public override bool IsImplemented => true;

    public override bool TryDeriveActionContext(in NetworkNPCActionData actionData, int currentTick, out DerivedNPCActionContext context)
    {
        context = default;

        if (!actionData.IsValid) return false;
        if (currentTick < actionData.startTick) return false;

        context = CreateDerivedContext(actionData, currentTick, durationTicks);
        return true;
    }

    public override void Tick(NPCActionManager manager, in DerivedNPCActionContext context)
    {
        if (context.IsComplete) return;
        if (!context.IsActionStart) return;
        if (manager.networkAnimator == null) return;
        if (string.IsNullOrWhiteSpace(animationTrigger)) return;

        manager.networkAnimator.SetTrigger(animationTrigger);
    }

    private void OnValidate()
    {
        durationTicks = Mathf.Max(1, durationTicks);
    }
}