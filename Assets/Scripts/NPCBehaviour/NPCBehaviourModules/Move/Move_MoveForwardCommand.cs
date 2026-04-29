using UnityEngine;

[CreateAssetMenu(menuName = "AI Commands/Move/Move Forward")]
public class Move_ForwardCommand : NPCCommand
{
    public override void ActiveTick(ref NPCCommandData data, NPCBehaviourManager manager, NPCActiveRagdollController muscle)
    {
        Vector3 direction = data.VectorData;
        float speed = data.FloatData;

        if (direction.sqrMagnitude < 0.01f)
        {
            direction = manager.transform.forward;
        }

        direction.y = 0;
        direction.Normalize();

        muscle.SetMovementTarget(direction * speed);
        muscle.SetLookDirection(direction);
    }
}