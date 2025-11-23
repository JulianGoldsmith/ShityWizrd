using UnityEngine;

[DefaultExecutionOrder(+20)]
public class StretchyArmIK : MonoBehaviour
{
    [System.Serializable]
    public class BoneData
    {
        public Transform bone;         
        public Transform childForAim;  

        [Header("Baked (T-Pose)")]
        public Vector3 restAimWS;      
        public Vector3 restUpWS;      
        public Quaternion restRotWS;   

        [Header("Flipper")]
        public bool invertAim;      
        public bool flipRoll180;

        public bool IsValid => bone != null;
    }

    [Header("Arm bones (final armature)")]
    public BoneData shoulder = new BoneData();
    public BoneData elbow = new BoneData();
    public BoneData wrist = new BoneData();

    [Header("IK targets")]
    public Transform target; 
    public Transform hint;    

    [Header("Settings")]
    [Range(0f, 1f)] public float weight = 1f;
    public float maxStretchFactor = 2f;
    public bool alignWristRotation = true;

    [ContextMenu("Bake Rest Data (T-Pose)")]
    public void BakeRestData()
    {
        BakeBone(shoulder);
        BakeBone(elbow);
        BakeBone(wrist);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log("[StretchyArmIK] Baked rest data for shoulder/elbow/wrist.");
    }

    private void BakeBone(BoneData data)
    {
        if (data.bone == null) return;

        Vector3 aim;
        if (data.childForAim != null)
            aim = data.childForAim.position - data.bone.position;
        else if (data.bone.childCount > 0)
            aim = data.bone.GetChild(0).position - data.bone.position;
        else
            aim = data.bone.up;

        aim.Normalize();
        if (data.invertAim)
            aim = -aim;

        Vector3 up = data.bone.forward;  


        up.Normalize();

        data.restAimWS = aim;
        data.restUpWS = up;
        data.restRotWS = BuildYUpRotation(aim, up);
    }

    void LateUpdate()
    {
        if (weight <= 0f) return;
        if (!shoulder.IsValid || !elbow.IsValid || !wrist.IsValid) return;
        if (target == null) return;

        Vector3 p0 = shoulder.bone.position; 
        Vector3 p1 = elbow.bone.position;    
        Vector3 p2 = wrist.bone.position;    
        Vector3 pt = target.position;        


        float upperLen = Vector3.Distance(p0, p1);
        float lowerLen = Vector3.Distance(p1, p2);
        float totalLen = upperLen + lowerLen;
        if (upperLen < 1e-5f || lowerLen < 1e-5f) return;

        Vector3 toTarget = pt - p0;
        float distToTarget = toTarget.magnitude;
        Vector3 dirToTarget = distToTarget > 1e-5f
            ? toTarget / distToTarget
            : (shoulder.restAimWS.sqrMagnitude > 0.0f ? shoulder.restAimWS : shoulder.bone.up);

        float allowedDist = totalLen * maxStretchFactor;  
                                                                         
        if (maxStretchFactor <= 1f)
            allowedDist = totalLen;

        float finalDist = Mathf.Min(distToTarget, allowedDist);
        Vector3 finalTargetPos = p0 + dirToTarget * finalDist;

        Vector3 bendNormal;
        if (hint != null)
        {
            Vector3 toHint = hint.position - p0;
            bendNormal = Vector3.Cross(toHint, dirToTarget);
            if (bendNormal.sqrMagnitude < 1e-6f)
                bendNormal = Vector3.Cross(Vector3.up, dirToTarget);
        }
        else
        {
            bendNormal = Vector3.Cross(Vector3.up, dirToTarget);
            if (bendNormal.sqrMagnitude < 1e-6f)
                bendNormal = Vector3.Cross(shoulder.bone.right, dirToTarget);
        }
        bendNormal.Normalize();

        float c = Mathf.Min(finalDist, totalLen - 1e-4f);

        float a = upperLen;
        float b = lowerLen;

        float cosA0 = (a * a + c * c - b * b) / (2f * a * c);
        cosA0 = Mathf.Clamp(cosA0, -1f, 1f);
        float angle0 = Mathf.Acos(cosA0);

        Quaternion shoulderRotToElbow = Quaternion.AngleAxis(Mathf.Rad2Deg * angle0, bendNormal);
        Vector3 elbowDir = shoulderRotToElbow * dirToTarget;
        Vector3 desiredElbowPos = p0 + elbowDir * upperLen;

        bool isStretchingPastReal = distToTarget > totalLen;
        if (isStretchingPastReal && finalDist >= totalLen)
        {

            desiredElbowPos = p0 + dirToTarget * upperLen;
        }


        Quaternion shoulderTargetRotWS =
        BuildYUpRotation((desiredElbowPos - p0).normalized, bendNormal);
        if (shoulder.flipRoll180)
        {
            Vector3 aimY = (desiredElbowPos - p0).normalized;
            shoulderTargetRotWS = Quaternion.AngleAxis(180f, aimY) * shoulderTargetRotWS;
        }
        shoulder.bone.rotation = Quaternion.Slerp(shoulder.bone.rotation, shoulderTargetRotWS, weight);

        // elbow pos
        elbow.bone.position = Vector3.Lerp(elbow.bone.position, desiredElbowPos, weight);

        Vector3 elbowToFinal = (finalTargetPos - desiredElbowPos).normalized;
        Quaternion elbowTargetRotWS =
            BuildYUpRotation(elbowToFinal, bendNormal);
        if (elbow.flipRoll180)
        {
            elbowTargetRotWS = Quaternion.AngleAxis(180f, elbowToFinal) * elbowTargetRotWS;
        }
        elbow.bone.rotation = Quaternion.Slerp(elbow.bone.rotation, elbowTargetRotWS, weight);

        wrist.bone.position = Vector3.Lerp(wrist.bone.position, finalTargetPos, weight);

        if (alignWristRotation)
        {
            wrist.bone.rotation = Quaternion.Slerp(wrist.bone.rotation, target.rotation, weight);
        }
        else
        {
            Quaternion wristTargetRotWS =
                BuildYUpRotation(elbowToFinal, bendNormal);
            if (wrist.flipRoll180)
            {
                wristTargetRotWS = Quaternion.AngleAxis(180f, elbowToFinal) * wristTargetRotWS;
            }
            wrist.bone.rotation = Quaternion.Slerp(wrist.bone.rotation, wristTargetRotWS, weight);
        }
    }

    private static Quaternion BuildYUpRotation(Vector3 aimDir, Vector3 upDir)
    {
        aimDir.Normalize();
        upDir.Normalize();

        Vector3 x = Vector3.Cross(aimDir, upDir);
        if (x.sqrMagnitude < 1e-6f)
        {
            upDir = Vector3.up;
            x = Vector3.Cross(aimDir, upDir);
        }
        x.Normalize();

        Vector3 z = Vector3.Cross(x, aimDir);

        Matrix4x4 m = new Matrix4x4();
        m.SetColumn(0, new Vector4(x.x, aimDir.x, z.x, 0f));
        return MatrixToQuaternion(x, aimDir, z);
    }

    private static Quaternion MatrixToQuaternion(Vector3 x, Vector3 y, Vector3 z)
    {
        Matrix4x4 m = new Matrix4x4();
        m.SetColumn(0, new Vector4(x.x, x.y, x.z, 0f));
        m.SetColumn(1, new Vector4(y.x, y.y, y.z, 0f));  
        m.SetColumn(2, new Vector4(z.x, z.y, z.z, 0f));
        m.SetColumn(3, new Vector4(0f, 0f, 0f, 1f));
        return m.rotation;
    }
}