using UnityEngine;

public class CollisionDetectorDebug : MonoBehaviour
{
    void OnCollisionEnter(Collision c)
    {
        if (c.impulse.sqrMagnitude > 400) // >20 N·s
            Debug.LogWarning($"[BIG-IMPULSE] enter from {c.collider.name} | |J|={c.impulse.magnitude} | relVel={c.relativeVelocity.magnitude}");
    }
    void OnCollisionStay(Collision c)
    {
        if (c.impulse.sqrMagnitude > 400)
            Debug.LogWarning($"[BIG-IMPULSE] stay  from {c.collider.name} | |J|={c.impulse.magnitude} | relVel={c.relativeVelocity.magnitude}");
    }
}
