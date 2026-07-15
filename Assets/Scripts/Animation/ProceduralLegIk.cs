using UnityEngine;

[DefaultExecutionOrder(+20)]
public class ProceduralLegIK : MonoBehaviour
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

    [Header("Leg Bones")]
    public BoneData upperLeg = new BoneData();
    public BoneData lowerLeg = new BoneData();
    public BoneData foot = new BoneData();

    [Header("IK Settings")]
    public Transform kneeHint; // Place a transform slightly in front of the knee
    [Range(0f, 1f)] public float masterWeight = 1f;
    public float maxStretchFactor = 1.01f; // Legs usually shouldn't stretch as much as arms

    [Header("Raycast & Grounding")]
    public LayerMask groundLayer;
    public float footOffset = 0.1f;
    public float raycastHeightOrigin = 0.5f;
    public float raycastDistance = 1.0f;

    [Header("Procedural Weighting (No Anim Curves Needed)")]
    [Tooltip("If true, automatically lowers IK weight when the animation lifts the foot to take a step.")]
    public bool useProceduralWeighting = true;
    public float stepHeightThreshold = 0.2f;
    private float _currentProceduralWeight = 1f;

    // Reference to character forward for aligning foot rotation
    private Transform _characterRoot;

    private void Start()
    {
        _characterRoot = transform.root;
    }

    [ContextMenu("Bake Rest Data (T-Pose)")]
    public void BakeRestData()
    {
        BakeBone(upperLeg);
        BakeBone(lowerLeg);
        BakeBone(foot);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log("[ProceduralLegIK] Baked rest data for leg.");
    }

    private void BakeBone(BoneData data)
    {
        if (data.bone == null) return;

        Vector3 aim;
        if (data.childForAim != null) aim = data.childForAim.position - data.bone.position;
        else if (data.bone.childCount > 0) aim = data.bone.GetChild(0).position - data.bone.position;
        else aim = data.bone.up;

        aim.Normalize();
        if (data.invertAim) aim = -aim;

        Vector3 up = data.bone.forward;
        up.Normalize();

        data.restAimWS = aim;
        data.restUpWS = up;
        data.restRotWS = BuildYUpRotation(aim, up);
    }

    void LateUpdate()
    {
        if (masterWeight <= 0f) return;
        if (!upperLeg.IsValid || !lowerLeg.IsValid || !foot.IsValid) return;

        // 1. Where did the Retargeter / Animation put the foot?
        Vector3 animatedFootPos = foot.bone.position;
        Quaternion animatedFootRot = foot.bone.rotation;

        Vector3 targetPos = animatedFootPos;
        Quaternion targetRot = animatedFootRot;

        // 2. Environmental Raycast
        Ray ray = new Ray(animatedFootPos + Vector3.up * raycastHeightOrigin, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, raycastHeightOrigin + raycastDistance, groundLayer))
        {
            // Set the physical target
            targetPos = hit.point + new Vector3(0, footOffset, 0);

            // Align rotation to slope
            targetRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(_characterRoot.forward, hit.normal), hit.normal);
        }

        // 3. Procedural Step Weighting (Replaces Animator Curves)
        float activeWeight = masterWeight;
        if (useProceduralWeighting)
        {
            // How high is the animated foot above the physical floor hit?
            float footHeight = animatedFootPos.y - hit.point.y;

            // If the foot is lifted (stepping), fade out the IK so the walk animation plays naturally
            float targetWeight = footHeight > stepHeightThreshold ? 0f : 1f;
            _currentProceduralWeight = Mathf.MoveTowards(_currentProceduralWeight, targetWeight, Time.deltaTime * 10f);

            activeWeight *= _currentProceduralWeight;
        }

        if (activeWeight <= 0.01f) return;

        // 4. THE SOLVER (Law of Cosines - Identical to ArmIK)
        Vector3 p0 = upperLeg.bone.position;
        Vector3 p1 = lowerLeg.bone.position;
        Vector3 p2 = foot.bone.position;

        float upperLen = Vector3.Distance(p0, p1);
        float lowerLen = Vector3.Distance(p1, p2);
        float totalLen = upperLen + lowerLen;
        if (upperLen < 1e-5f || lowerLen < 1e-5f) return;

        Vector3 toTarget = targetPos - p0;
        float distToTarget = toTarget.magnitude;
        Vector3 dirToTarget = distToTarget > 1e-5f ? toTarget / distToTarget : (upperLeg.restAimWS.sqrMagnitude > 0.0f ? upperLeg.restAimWS : upperLeg.bone.up);

        float allowedDist = totalLen * maxStretchFactor;
        if (maxStretchFactor <= 1f) allowedDist = totalLen;

        float finalDist = Mathf.Min(distToTarget, allowedDist);
        Vector3 finalTargetPos = p0 + dirToTarget * finalDist;

        // Calculate Knee Bend Direction
        Vector3 bendNormal;
        if (kneeHint != null)
        {
            Vector3 toHint = kneeHint.position - p0;
            bendNormal = Vector3.Cross(toHint, dirToTarget);
            if (bendNormal.sqrMagnitude < 1e-6f) bendNormal = Vector3.Cross(Vector3.up, dirToTarget);
        }
        else
        {
            bendNormal = Vector3.Cross(upperLeg.bone.right, dirToTarget);
            if (bendNormal.sqrMagnitude < 1e-6f) bendNormal = Vector3.Cross(Vector3.up, dirToTarget);
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

        // Apply Upper Leg Rotation
        Quaternion shoulderTargetRotWS = BuildYUpRotation((desiredElbowPos - p0).normalized, bendNormal);
        if (upperLeg.flipRoll180)
        {
            Vector3 aimY = (desiredElbowPos - p0).normalized;
            shoulderTargetRotWS = Quaternion.AngleAxis(180f, aimY) * shoulderTargetRotWS;
        }
        upperLeg.bone.rotation = Quaternion.Slerp(upperLeg.bone.rotation, shoulderTargetRotWS, activeWeight);

        // Apply Lower Leg Pos/Rot
        lowerLeg.bone.position = Vector3.Lerp(lowerLeg.bone.position, desiredElbowPos, activeWeight);
        Vector3 elbowToFinal = (finalTargetPos - desiredElbowPos).normalized;

        Quaternion elbowTargetRotWS = BuildYUpRotation(elbowToFinal, bendNormal);
        if (lowerLeg.flipRoll180) elbowTargetRotWS = Quaternion.AngleAxis(180f, elbowToFinal) * elbowTargetRotWS;
        lowerLeg.bone.rotation = Quaternion.Slerp(lowerLeg.bone.rotation, elbowTargetRotWS, activeWeight);

        // Apply Foot Pos/Rot
        foot.bone.position = Vector3.Lerp(foot.bone.position, finalTargetPos, activeWeight);
        foot.bone.rotation = Quaternion.Slerp(foot.bone.rotation, targetRot, activeWeight);
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