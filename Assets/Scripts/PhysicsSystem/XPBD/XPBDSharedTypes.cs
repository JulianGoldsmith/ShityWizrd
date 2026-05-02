using UnityEngine;
using Fusion;

// --- SHARED DATA CLASSES ---
public class XPBDState
{
    public Rigidbody rb;
    public Vector3 p_prev;
    public Quaternion q_prev;
    public Vector3 p;
    public Quaternion q;
    public Vector3 v;
    public Vector3 w;
    public float invMass;
    public Vector3 invInertiaLocal;
    public Quaternion qInertia;
    public bool isKinematic;
}

[System.Serializable]
public struct NetworkTempJoint : INetworkStruct
{
    public NetworkId parentId;
    public NetworkId childId;

    public Vector3 parentAnchorLocal;
    public Vector3 childAnchorLocal;

    public Quaternion targetLocalRotation;

    public float distanceCompliance;
    public float distanceDamping;
    public float muscleCompliance;
    public float muscleDamping;
}

public class HydratedTempJoint
{
    public NetworkTempJoint networkedData;
    public Rigidbody parentRb;
    public Rigidbody childRb;
    public Vector3 lambdaPosition;
    public Vector3 lambdaRotation;

    public bool IsValid() => parentRb != null && childRb != null;

    public void Clear()
    {
        parentRb = null;
        childRb = null;
        networkedData = default(NetworkTempJoint);
        lambdaPosition = Vector3.zero;
        lambdaRotation = Vector3.zero;
    }
}

[System.Serializable]
public struct NetworkGrabJoint : INetworkStruct
{
    public NetworkId grabberId; // The Player's Network Object
    public NetworkId itemId;    // The Object being grabbed

    public Vector3 localGrabOffset; // Where on the item we grabbed
    public float grabDistance;      // Distance from the camera

    public Quaternion targetLocalRotation; // <-- Added: The snapshot of how the item aligns to the camera

    public float grabStrength;      // Used for Compliance (Stiffness)
    public float grabDamping;       // <-- Added: Prevents the grabbed object from infinitely wobbling
    public float dragResistance;    // Used for Inverse Mass Scaling (How easily the player is dragged)
}

// --- LOCAL RUNTIME DATA (Fast Access) ---
public class HydratedGrabJoint
{
    public NetworkGrabJoint networkedData;

    // Cached References
    public HybridCharacterController grabberController;
    public Rigidbody torsoRb;
    public Rigidbody itemRb;

    // XPBD Memory
    public Vector3 lambdaPosition;
    public Vector3 lambdaRotation; // <-- Added: Needed for 3D orthogonal rotation solver

    public bool IsValid() => grabberController != null && itemRb != null && torsoRb != null;

    public void Clear()
    {
        grabberController = null;
        torsoRb = null;
        itemRb = null;
        networkedData = default(NetworkGrabJoint);
        lambdaPosition = Vector3.zero;
        lambdaRotation = Vector3.zero; // <-- Added: Reset memory
    }
}

// --- STATIC MATH HELPERS ---
public static class XPBDMath
{
    public static Vector3 ApplyInvInertiaWorld(Vector3 v, Quaternion q, Quaternion qInertia, Vector3 invInertiaLocal)
    {
        Quaternion R = q * qInertia;
        Vector3 localV = Quaternion.Inverse(R) * v;
        localV.x *= invInertiaLocal.x;
        localV.y *= invInertiaLocal.y;
        localV.z *= invInertiaLocal.z;
        return R * localV;
    }

    public static void ApplyDeltaRotation(XPBDState state, Vector3 deltaRot)
    {
        float angle = deltaRot.magnitude;
        if (angle < 1e-6f) return;

        Quaternion qRot = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, deltaRot / angle);
        state.q = qRot * state.q;
    }

    public static Vector3 GetDeltaTheta(Quaternion qPrev, Quaternion qCurr)
    {
        Quaternion dq = qCurr * Quaternion.Inverse(qPrev);
        dq.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        if (Mathf.Abs(angle) < 1e-6f || axis.sqrMagnitude < 1e-6f) return Vector3.zero;
        return axis.normalized * (angle * Mathf.Deg2Rad);
    }
}