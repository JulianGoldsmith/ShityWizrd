using Unity.Behavior;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CharacterController))]
public class EnemyMovement : MonoBehaviour
{
    public NavMeshAgent agent;
    public Animator animator;
    public CharacterController controller;

    public float animationRefSpeed = 1.5f;
    public float walkSpeed = 3f, runSpeed = 5f, strafeSpeed =1.5f;

    public float rotationSpeed = 5f;

    public float animationDampTime = 0.1f;

    public Blackboard blackboard;

    private Vector3 verticalVelocity;
    public float gravity = -9.81f;

    public Vector3 previousFramePos;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>(); ;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (agent == null) agent = GetComponent<NavMeshAgent>();

        agent.updatePosition = false;
        agent.updateRotation = false;
    }

    private void Update()
    {
        agent.nextPosition = transform.position;

        UpdateAnimator();
        previousFramePos = transform.position;
    }

    public void RotateInDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSpeed * Time.deltaTime);
        }
        
    }

    public void RotateInMovementDirection()
    {
        var direction = agent.desiredVelocity;
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSpeed * Time.deltaTime);
        }

    }

    public void RotateTowardsPoint(Vector3 point)
    {
        var dir = point - transform.position;
        dir.y = 0; 
        if (dir != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSpeed * Time.deltaTime);
        }
    }

    public void MoveToPoint(Vector3 destination, float speed)
    {
        //agent.speed = speed;
        agent.isStopped = false;
        agent.SetDestination(destination);
        MoveCharacter(GetNavInput(), speed);
    }

    public bool TryFindValidCombatPoint(Vector3 center, float minRadius, float maxRadius, out Vector3 validPoint)
    {
        for (int i = 0; i < 10; i++)
        {
            float randomAngle = Random.Range(0, 360) * Mathf.Deg2Rad;
            float randomDistance = Random.Range(minRadius, maxRadius);
            Vector3 offset = new Vector3(Mathf.Sin(randomAngle), 0, Mathf.Cos(randomAngle)) * randomDistance;
            Vector3 desiredPoint = center + offset;

            if (NavMesh.SamplePosition(desiredPoint, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                validPoint = hit.position;
                return true;
            }
        }
        validPoint = transform.position;
        return false;
    }

    public bool ExecuteCombatOrbit(GameObject target, float idealDistance, float strafeDirection)
    {
        if (target == null)
        {
            StopMovement();
            return true;
        }

        Vector3 directionToTarget = (transform.position - target.transform.position).normalized;
        Vector3 idealPosition = target.transform.position + directionToTarget * idealDistance;
        Vector3 tangent = Vector3.Cross(directionToTarget, Vector3.up) * strafeDirection;
        Vector3 targetPointPOs = idealPosition + tangent * 2f;

        if (NavMesh.SamplePosition(targetPointPOs, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
        {
            //agent.speed = this.strafeSpeed;
            agent.SetDestination(hit.position);
            agent.isStopped = false;
            MoveCharacter(GetNavInput(), strafeSpeed);
            return true;
        }
        else
        {
            Debug.Log("No place on navMesh found ");
            StopMovement();
            return false;
        }
    }

    public void StopMovement()
    {
       
        if (agent.isOnNavMesh) agent.isStopped = true;

        MoveCharacter(Vector3.zero, 0);
    }

    private void MoveCharacter(Vector3 direction, float speed)
    {
        direction.Normalize();
        if (controller.isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f;
        }
        verticalVelocity.y += gravity * Time.deltaTime;

        Vector3 finalMoveVector = (direction * speed) + verticalVelocity;
        if(controller.enabled)
            controller.Move(finalMoveVector * Time.deltaTime);
    }

    private void UpdateAnimator()
    {
        Vector3 worldVelocity = (transform.position - previousFramePos) / Time.deltaTime;
        if (animator == null) return;

        float forwardSpeed = Vector3.Dot(worldVelocity, transform.forward) / animationRefSpeed;
        float rightSpeed = Vector3.Dot(worldVelocity, transform.right) / animationRefSpeed;

        animator.SetFloat("SpeedForward", forwardSpeed, animationDampTime, Time.deltaTime);
        animator.SetFloat("SpeedRight", rightSpeed, animationDampTime, Time.deltaTime);

    }

    public Vector3 GetNavInput()
    {
        Vector3 navInput = agent.desiredVelocity;
        navInput.y = 0;
        
        navInput.Normalize();
        
        return navInput;
    }
}
