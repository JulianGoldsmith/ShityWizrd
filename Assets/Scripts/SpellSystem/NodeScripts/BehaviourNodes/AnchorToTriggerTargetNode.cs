using Fusion;
using UnityEngine;

[CreateAssetMenu(fileName = "AnchorToTriggerTarget", menuName = "SpellNodes/Behaviour/Anchor To Trigger Target")]
public class AnchorToTriggerTargetNode : BehaviourNode
{
    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        return new AnchorToTriggerTargetBehaviour
        {
            LocalPositionMemorySlot = context.ClaimVectorSlot(),
            LocalRotationMemorySlot = context.ClaimVectorSlot()
        };
    }

    public override void SetUp(GameObject spellCore, SpellTriggerInfo triggerInfo) { }
}

public class AnchorToTriggerTargetBehaviour : IBehaviour
{
    public int LocalPositionMemorySlot;
    public int LocalRotationMemorySlot;

    public void InitTick(ISpellExecutionCore core)
    {
        if (!core.Context.CurrentTarget.IsValid) return;
        if (!core.Runner.TryFindObject(core.Context.CurrentTarget, out NetworkObject targetObject)) return;

        Rigidbody targetBody = targetObject.GetComponent<Rigidbody>();
        Vector3 localPosition = targetBody.transform.InverseTransformPoint(core.Context.SpawnPosition);
        Quaternion localRotation = Quaternion.Inverse(targetBody.rotation) * core.Rotation;

        core.SetVector(LocalPositionMemorySlot, localPosition);
        core.SetVector(LocalRotationMemorySlot, localRotation.eulerAngles);
    }

    public void Tick(ISpellExecutionCore core, float deltaTime)
    {
        if (!core.Context.CurrentTarget.IsValid) return;
        if (!core.Runner.TryFindObject(core.Context.CurrentTarget, out NetworkObject targetObject)) return;

        Rigidbody targetBody = targetObject.GetComponent<Rigidbody>();
        Rigidbody coreBody = core.SourceObject.GetComponent<Rigidbody>();
        Vector3 position = targetBody.transform.TransformPoint(core.GetVector(LocalPositionMemorySlot));
        Quaternion rotation = targetBody.rotation * Quaternion.Euler(core.GetVector(LocalRotationMemorySlot));

        coreBody.MovePosition(position);
        coreBody.MoveRotation(rotation);
    }

    public void TickVFX(ISpellExecutionCore core) { }

    public void CleanupVFX(ISpellExecutionCore core) { }
}