using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

public class EnemyCombat : MonoBehaviour
{
    public float engageDistance = 15f;

    public float idealMaxDistance = 8f;
    public float idealMinDistance = 2f;

    public float minRepositionTime = 3f;
    public float maxRepositionTime = 5f;

    public float rushdownCooldownMax = 15f, rushdownCooldownMin = 3f;

    public float attackRange = 3f; 

    [SerializeField] private BehaviorGraphAgent agent;

    [SerializeField] private string targetVariableName = "Target";

    [SerializeField] private string isInEngageRangeVarName = "IsInEngageRange";
    [SerializeField] private string isInIdealRangeVarName = "IsInIdealCombatRange";
    [SerializeField] private string isInAttackRangeVarName = "IsInAttackRange";

    public Vector3 PickNewCombatPoint(Vector3 targetPosition)
    {
        float randomAngle = Random.Range(0, 360) * Mathf.Deg2Rad;
        float randomDistance = Random.Range(idealMinDistance, idealMaxDistance);

        Vector3 offset = new Vector3(Mathf.Sin(randomAngle), 0, Mathf.Cos(randomAngle)) * randomDistance;

        return targetPosition + offset;
    }

    private void Update()
    {
        if (agent.GetVariable(targetVariableName, out BlackboardVariable<GameObject> target) && target.Value != null)
        {
            DrawDebugCircle(target.Value.transform.position - Vector3.up*1, engageDistance, Color.green);
            DrawDebugCircle(target.Value.transform.position - Vector3.up * 1, idealMinDistance, Color.blue);
            DrawDebugCircle(target.Value.transform.position - Vector3.up * 1, idealMaxDistance, Color.red);
        }
        UpdateCombatRanges();
    }

    void UpdateCombatRanges()
    {
        agent.GetVariable(targetVariableName, out BlackboardVariable<GameObject> target);
        if (target == null || target.Value == null)
        {
            agent.SetVariableValue(isInEngageRangeVarName, false);
            agent.SetVariableValue(isInIdealRangeVarName, false);
            agent.SetVariableValue(isInAttackRangeVarName, false);
            return;
        }
        float distance = Vector3.Distance(transform.position, target.Value.transform.position);
        agent.SetVariableValue(isInEngageRangeVarName, distance <= engageDistance);
        agent.SetVariableValue(isInAttackRangeVarName, distance <= attackRange);
        bool isInIdeal = distance >= idealMinDistance && distance <= idealMaxDistance;
        agent.SetVariableValue(isInIdealRangeVarName, isInIdeal);
    }

    

    private void DrawDebugCircle(Vector3 center, float radius, Color color, int segments = 24)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + Quaternion.Euler(0, 0, 0) * Vector3.forward * radius;

        for (int i = 1; i <= segments; i++)
        {
            float angle = angleStep * i;
            Vector3 nextPoint = center + Quaternion.Euler(0, angle, 0) * Vector3.forward * radius;
            Debug.DrawLine(prevPoint, nextPoint, color);
            prevPoint = nextPoint;
        }
    }
    private void Awake()
    {
        if (agent == null)
        {
            agent = this.GetComponent<BehaviorGraphAgent>();
        }
    }
}
