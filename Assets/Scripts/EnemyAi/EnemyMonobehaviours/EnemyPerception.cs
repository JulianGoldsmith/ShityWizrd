using Unity.Behavior; 
using Unity.Properties;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class EnemyPerception : MonoBehaviour
{
    [SerializeField] private BehaviorGraphAgent agent;

    [SerializeField] private string targetVariableName = "Target";

    [SerializeField] private string lastSeenPositionVariableName = "TargetLastSeenPosition";

    public float perceptionRadius = 10f;
    [Range(0, 360)]
    public float perceptionAngle = 180f;

    public string targetTag = "Player";
    public LayerMask targetLayer;
    public LayerMask obstacleLayer;
    public Vector3 eyeOffset = new Vector3(0, 1.5f, 0);

    private GameObject previousFrameTarget = null;

    private void Update()
    {
        GameObject currentTargetInSight = FindVisibleTarget();

       
        UpdateBlackboard(currentTargetInSight);
       


        if (currentTargetInSight != null)
        {
            if (agent.GetVariable(variableName: targetVariableName, out BlackboardVariable<GameObject> target))
            {
                if (target != null)
                    Debug.DrawLine(transform.position + eyeOffset, currentTargetInSight.transform.position, Color.cyan);
            }
            
        }
        if (agent.GetVariable(lastSeenPositionVariableName, out BlackboardVariable<Vector3> lastSeenPos) && lastSeenPos.Value != Vector3.zero)
        {
            DrawDebugStar(lastSeenPos, 0.5f, Color.magenta);
        }
    }

    private void UpdateBlackboard(GameObject newTarget)
    {

        if (newTarget != previousFrameTarget)
        {
            agent.SetVariableValue(targetVariableName, newTarget);
        }

        if (newTarget != null)
        {
            agent.SetVariableValue(lastSeenPositionVariableName, newTarget.transform.position);
        }

        previousFrameTarget = newTarget;
    }

    private GameObject FindVisibleTarget()
    {
        Collider[] targetsInRadius = Physics.OverlapSphere(transform.position, perceptionRadius, targetLayer);

        foreach (var targetCollider in targetsInRadius)
        {
            if (!targetCollider.CompareTag(targetTag)) continue;

            GameObject potentialTarget = targetCollider.transform.parent.gameObject;
            if (potentialTarget == null) continue; 
 
            Vector3 directionToTarget = (potentialTarget.transform.position - transform.position).normalized;

            if (Vector3.Angle(transform.forward, directionToTarget) < perceptionAngle / 2)
            {
                float distanceToTarget = Vector3.Distance(transform.position, potentialTarget.transform.position);
                Vector3 eyePosition = transform.position + eyeOffset;

                if (!Physics.Raycast(eyePosition, directionToTarget, distanceToTarget, obstacleLayer))
                {
                    
                    return potentialTarget;
                }
            }
        }
        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, perceptionRadius);

        Gizmos.color = Color.red;
        Vector3 forward = transform.forward;
        Vector3 coneLeft = Quaternion.Euler(0, -perceptionAngle / 2, 0) * forward * perceptionRadius;
        Vector3 coneRight = Quaternion.Euler(0, perceptionAngle / 2, 0) * forward * perceptionRadius;

        Gizmos.DrawLine(transform.position, transform.position + coneLeft);
        Gizmos.DrawLine(transform.position, transform.position + coneRight);


        
    }

    private void OnDrawGizmos()
    {
        
    }

    private void DrawDebugStar(Vector3 position, float size, Color color)
    {
        Debug.DrawRay(position, Vector3.up * size, color);
        Debug.DrawRay(position, Vector3.down * size, color);
        Debug.DrawRay(position, Vector3.left * size, color);
        Debug.DrawRay(position, Vector3.right * size, color);
        Debug.DrawRay(position, Vector3.forward * size, color);
        Debug.DrawRay(position, Vector3.back * size, color);
    }

   
}