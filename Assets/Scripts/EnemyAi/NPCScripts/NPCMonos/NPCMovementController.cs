using UnityEngine;
using UnityEngine.AI;

public class NPCMovementController : MonoBehaviour
{
    public NavMeshAgent agent;
    public NPCActiveRagdollController controller;
    public Transform agentTransform;

    public Vector3 previousFramePos;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<NPCActiveRagdollController>(); ;

        if (agent == null) agent = GetComponent<NavMeshAgent>();

        agent.updatePosition = false;
        agent.updateRotation = false;
    }

    private void Update()
    {
        agent.nextPosition = agentTransform.position;
       // Debug.Log("Agent pos = " + agent.nextPosition);
        previousFramePos = agentTransform.position;
    }

    public void RotateInDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.01f)
        {
            controller.SetLookDir(direction);
        }
    }

    public void RotateInMovementDirection()
    {
        var direction = agent.desiredVelocity;
        if (direction.sqrMagnitude > 0.01f)
        {
            controller.SetLookDir(direction);
        }
    }

    public void RotateTowardsPoint(Vector3 point)
    {
        var dir = point - (controller.coreRB.position - Vector3.up * controller.rideHeight);
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            controller.SetLookDir(dir);
        }
    }

    public void MoveToPoint(Vector3 destination, float speed)
    {
        //agent.speed = speed;
        agent.isStopped = false;
        agent.SetDestination(destination);
        MoveCharacter(GetNavInput(), speed);
    }

    public void StopMovement()
    {

        if (agent.isOnNavMesh) agent.isStopped = true;
        MoveCharacter(Vector3.zero, 0);
    }

    private void MoveCharacter(Vector3 direction, float speed)
    {
        direction.Normalize();

        controller.SetMoveInput(direction, speed);
    }

    public Vector3 GetNavInput()
    {
        Vector3 navInput = agent.desiredVelocity;
        

        if (navInput.sqrMagnitude < 0.01f)
        {
            //Debug.LogWarning($"[GetNavInput] agent.desiredVelocity is ZERO. " +
            //    $"hasPath: {agent.hasPath}, " +
            //    $"pathPending: {agent.pathPending}, " +
            //    $"isStopped: {agent.isStopped}, " +
            //    $"pathStatus: {agent.pathStatus}, " +
            //    $"remainingDistance: {agent.remainingDistance}", this);
        }
        else
        {
           // Debug.Log($"[GetNavInput] agent.desiredVelocity is {navInput.normalized}", this);
        }
        //navInput.y = 0;
        navInput.Normalize();

        return navInput;
    }
}
