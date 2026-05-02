using System.Collections.Generic;
using UnityEngine;
using Fusion;

public class XPBDGlobalManager : NetworkBehaviour
{
    [Header("Global Settings")]
    public int iterations = 4;
    public bool enableSolver = true;

    public List<XPBDPosAndRotSolver> registeredRagdolls = new List<XPBDPosAndRotSolver>();

    [Networked, Capacity(32)]
    public NetworkArray<NetworkTempJoint> NetworkedTempJoints { get; }
    private HydratedTempJoint[] _hydratedTempJoints = new HydratedTempJoint[32];

    [Networked, Capacity(16)]
    public NetworkArray<NetworkGrabJoint> NetworkedGrabJoints { get; }
    private HydratedGrabJoint[] _hydratedGrabJoints = new HydratedGrabJoint[16];

    private int _lastGrabCount = -1;

    private Dictionary<Rigidbody, XPBDState> _globalStates = new Dictionary<Rigidbody, XPBDState>();

    [Header("Grab Curve")]
    [Tooltip("0 = 0 distance error, 1 = max dragRange error. Value is Compliance Multiplier (Higher = Softer/Weaker).")]
    public AnimationCurve distanceComplianceCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 10f)
    );

    [Header("Recoil Curve")]
    [Tooltip("X-Axis: Tension Force (Newtons). Y-Axis: Recoil Multiplier (0 to 1).")]
    public AnimationCurve recoilTensionCurve = new AnimationCurve(
        new Keyframe(0f, 0f),    // 0 Newtons = 0% Recoil (Tiny objects feel weightless)
        new Keyframe(500f, 1f)   // 500+ Newtons = 100% Recoil (Heavy objects drag you)
    );

    public float dragRange = 10f;

    public override void Spawned()
    {
        Runner.SetIsSimulated(this.Object, true);
        base.Spawned();

        for (int i = 0; i < _hydratedGrabJoints.Length; i++)
            _hydratedGrabJoints[i] = new HydratedGrabJoint();

        for (int i = 0; i < _hydratedTempJoints.Length; i++)
        {
            _hydratedTempJoints[i] = new HydratedTempJoint();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!enableSolver) return;
        float dt = Runner.DeltaTime;
        if (dt <= 0f) return;

        SyncHydratedTmpJoints();
        SyncHydratedGrabs();

        _globalStates.Clear();

        foreach (var ragdoll in registeredRagdolls)
        {
            ragdoll.InitializeStates(dt, _globalStates);
        }

        foreach (var grab in _hydratedTempJoints)
        {
            AddStateIfMissing(grab.parentRb, dt);
            AddStateIfMissing(grab.childRb, dt);
            grab.lambdaPosition = Vector3.zero;
            grab.lambdaRotation = Vector3.zero;
        }

        foreach (var grab in _hydratedGrabJoints)
        {
            if (grab.IsValid())
            {
                AddStateIfMissing(grab.torsoRb, dt);
                AddStateIfMissing(grab.itemRb, dt);

                grab.lambdaPosition = Vector3.zero;
                grab.lambdaRotation = Vector3.zero;
            }
        }

        for (int i = 0; i < iterations; i++)
        {
            foreach (var ragdoll in registeredRagdolls)
            {
                ragdoll.SolveConstraints(dt, _globalStates);
            }

            foreach (var tmp in _hydratedTempJoints)
            {
                if (tmp.IsValid())
                {
                    SolveTempDistance(tmp, dt, _globalStates);
                    SolveTempRotation(tmp, dt, _globalStates);
                }
            }

            foreach (var grab in _hydratedGrabJoints)
            {
                if (grab.IsValid())
                {
                    SolveGrabDistance(grab, dt, _globalStates);
                    SolveGrabRotation(grab, dt, _globalStates);
                }
            }
        }

        DeriveAllVelocities(dt);
        ApplyAllToUnity();
    }

    private void SyncHydratedTmpJoints()
    {
        for (int i = 0; i < NetworkedTempJoints.Length; i++)
        {
            var netJoint = NetworkedTempJoints[i];
            var localJoint = _hydratedTempJoints[i];

            if (!netJoint.parentId.IsValid || !netJoint.childId.IsValid)
            {
                if (localJoint.IsValid())
                    localJoint.Clear();
                continue;
            }

            if (localJoint.networkedData.parentId != netJoint.parentId ||
                localJoint.networkedData.childId != netJoint.childId)
            {
                localJoint.networkedData = netJoint;

                if (Runner.TryFindObject(netJoint.parentId, out var pObj))
                    localJoint.parentRb = pObj.GetComponent<Rigidbody>();
                else
                    localJoint.parentRb = null;

                if (Runner.TryFindObject(netJoint.childId, out var cObj))
                    localJoint.childRb = cObj.GetComponent<Rigidbody>();
                else
                    localJoint.childRb = null;

                localJoint.lambdaPosition = Vector3.zero;
                localJoint.lambdaRotation = Vector3.zero;
            }
            else
            {
                localJoint.networkedData = netJoint;
            }
        }
        
    }

    private void SyncHydratedGrabs()
    {
        // Loop through the fixed size arrays (Capacity 16)
        for (int i = 0; i < NetworkedGrabJoints.Length; i++)
        {
            var netGrab = NetworkedGrabJoints[i];
            var localGrab = _hydratedGrabJoints[i];

            // 1. If the network slot is empty, clear our local slot
            if (!netGrab.grabberId.IsValid || !netGrab.itemId.IsValid)
            {
                if (localGrab.IsValid())
                    localGrab.Clear();
                continue;
            }

            // 2. Mismatch detected! (New Grab, or Rollback Correction)
            if (localGrab.networkedData.grabberId != netGrab.grabberId ||
                localGrab.networkedData.itemId != netGrab.itemId)
            {
                localGrab.networkedData = netGrab;

                // Lookup the Player/Controller
                if (Runner.TryFindObject(netGrab.grabberId, out var grabberObj))
                {
                    localGrab.grabberController = grabberObj.GetComponent<HybridCharacterController>();
                    localGrab.torsoRb = localGrab.grabberController?.hipsRb;
                }
                else
                {
                    localGrab.grabberController = null;
                    localGrab.torsoRb = null;
                }

                // Lookup the Item
                if (Runner.TryFindObject(netGrab.itemId, out var itemObj))
                    localGrab.itemRb = itemObj.GetComponent<Rigidbody>();
                else
                    localGrab.itemRb = null;

                // Reset XPBD lambdas for the fresh grab
                localGrab.lambdaPosition = Vector3.zero;
                localGrab.lambdaRotation = Vector3.zero;
            }
            else
            {
                // 3. Exact Match. Just update the struct variables (Strength, Distance, etc.)
                localGrab.networkedData = netGrab;
            }
        }
    }

    private void AddStateIfMissing(Rigidbody rb, float dt)
    {
        if (rb == null || _globalStates.ContainsKey(rb)) return;

        XPBDState state = new XPBDState
        {
            rb = rb,
            isKinematic = rb.isKinematic,
            invMass = rb.isKinematic ? 0f : 1f / rb.mass,
            invInertiaLocal = rb.isKinematic ? Vector3.zero : new Vector3(1f / rb.inertiaTensor.x, 1f / rb.inertiaTensor.y, 1f / rb.inertiaTensor.z),
            qInertia = rb.inertiaTensorRotation
        };

        state.p_prev = rb.position;
        state.q_prev = rb.rotation;

        if (!state.isKinematic)
        {
            state.p = rb.position + rb.linearVelocity * dt;
            Vector3 angVel = rb.angularVelocity;
            float angle = angVel.magnitude;
            state.q = (angle > 1e-6f) ? Quaternion.AngleAxis(angle * Mathf.Rad2Deg * dt, angVel / angle) * rb.rotation : rb.rotation;
        }
        else
        {
            state.p = rb.position;
            state.q = rb.rotation;
        }

        _globalStates[rb] = state;
    }

    // --- TEMPORARY JOINT MATH (Matches Ragdoll Math exactly) ---

    private void SolveTempDistance(HydratedTempJoint grab, float dt, Dictionary<Rigidbody, XPBDState> states)
    {
        var pState = states[grab.parentRb];
        var cState = states[grab.childRb];

        if (pState.isKinematic && cState.isKinematic) return;

        Vector3 r0 = pState.q * grab.networkedData.parentAnchorLocal;
        Vector3 r1 = cState.q * grab.networkedData.childAnchorLocal;

        Vector3 p0 = pState.p + r0;
        Vector3 p1 = cState.p + r1;
        Vector3 dir = p1 - p0;

        float alpha = grab.networkedData.distanceCompliance / (dt * dt);
        float betaTilde = 0.5f * dt * grab.networkedData.distanceDamping;
        float gamma = (alpha * betaTilde) / dt;

        Vector3 dx0 = pState.p - pState.p_prev;
        Vector3 dx1 = cState.p - cState.p_prev;
        Vector3 dw0 = XPBDMath.GetDeltaTheta(pState.q_prev, pState.q);
        Vector3 dw1 = XPBDMath.GetDeltaTheta(cState.q_prev, cState.q);

        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };

        for (int i = 0; i < 3; i++)
        {
            Vector3 cAxis = axes[i];
            float C = Vector3.Dot(dir, cAxis);

            Vector3 gradP0 = -cAxis;
            Vector3 gradP1 = cAxis;

            // Note: Both objects get rotational feedback here!
            Vector3 gradQ0 = Vector3.Cross(r0, gradP0);
            Vector3 gradQ1 = Vector3.Cross(r1, gradP1);

            float w0 = pState.isKinematic ? 0f : pState.invMass + Vector3.Dot(gradQ0, XPBDMath.ApplyInvInertiaWorld(gradQ0, pState.q, pState.qInertia, pState.invInertiaLocal));
            float w1 = cState.isKinematic ? 0f : cState.invMass + Vector3.Dot(gradQ1, XPBDMath.ApplyInvInertiaWorld(gradQ1, cState.q, cState.qInertia, cState.invInertiaLocal));

            float wSum = w0 + w1;
            if (wSum < 1e-6f) continue;

            float dC = Vector3.Dot(gradP0, dx0) + Vector3.Dot(gradP1, dx1) + Vector3.Dot(gradQ0, dw0) + Vector3.Dot(gradQ1, dw1);

            float currentLambda = i == 0 ? grab.lambdaPosition.x : (i == 1 ? grab.lambdaPosition.y : grab.lambdaPosition.z);
            float deltaLambda = -(C + alpha * currentLambda + gamma * dC) / ((1f + gamma) * wSum + alpha);

            if (i == 0) grab.lambdaPosition.x += deltaLambda;
            else if (i == 1) grab.lambdaPosition.y += deltaLambda;
            else grab.lambdaPosition.z += deltaLambda;

            if (!pState.isKinematic)
            {
                pState.p += pState.invMass * deltaLambda * gradP0;
                XPBDMath.ApplyDeltaRotation(pState, XPBDMath.ApplyInvInertiaWorld(deltaLambda * gradQ0, pState.q, pState.qInertia, pState.invInertiaLocal));
            }
            if (!cState.isKinematic)
            {
                cState.p += cState.invMass * deltaLambda * gradP1;
                XPBDMath.ApplyDeltaRotation(cState, XPBDMath.ApplyInvInertiaWorld(deltaLambda * gradQ1, cState.q, cState.qInertia, cState.invInertiaLocal));
            }
        }
    }

    private void SolveTempRotation(HydratedTempJoint grab, float dt, Dictionary<Rigidbody, XPBDState> states)
    {
        var pState = states[grab.parentRb];
        var cState = states[grab.childRb];

        if (pState.isKinematic && cState.isKinematic) return;

        // Uses the BAKED SNAPSHOT from the Network Struct!
        Quaternion targetQ = pState.q * grab.networkedData.targetLocalRotation;
        Quaternion qError = targetQ * Quaternion.Inverse(cState.q);

        if (qError.w < 0f) { qError.x = -qError.x; qError.y = -qError.y; qError.z = -qError.z; qError.w = -qError.w; }

        Vector3 v = new Vector3(qError.x, qError.y, qError.z);
        float sinHalfAngle = v.magnitude;
        if (sinHalfAngle < 1e-6f) return;

        Vector3 axis = v / sinHalfAngle;
        float angleRad = 2f * Mathf.Atan2(sinHalfAngle, qError.w);

        float alpha = grab.networkedData.muscleCompliance / (dt * dt);
        float betaTilde = 0.5f * dt * grab.networkedData.muscleDamping;
        float gamma = (alpha * betaTilde) / dt;

        Vector3 rotVec = axis * angleRad;
        Vector3[] orthogonalAxes = { Vector3.right, Vector3.up, Vector3.forward };

        for (int i = 0; i < 3; i++)
        {
            Vector3 cAxis = orthogonalAxes[i];
            float C = Vector3.Dot(rotVec, cAxis);

            float w0 = pState.isKinematic ? 0f : Vector3.Dot(cAxis, XPBDMath.ApplyInvInertiaWorld(cAxis, pState.q, pState.qInertia, pState.invInertiaLocal));
            float w1 = cState.isKinematic ? 0f : Vector3.Dot(-cAxis, XPBDMath.ApplyInvInertiaWorld(-cAxis, cState.q, cState.qInertia, cState.invInertiaLocal));
            float wSum = w0 + w1;
            if (wSum < 1e-6f) continue;

            float dC = Vector3.Dot(cAxis, XPBDMath.GetDeltaTheta(pState.q_prev, pState.q)) + Vector3.Dot(-cAxis, XPBDMath.GetDeltaTheta(cState.q_prev, cState.q));
            float currentLambda = i == 0 ? grab.lambdaRotation.x : (i == 1 ? grab.lambdaRotation.y : grab.lambdaRotation.z);

            float deltaLambda = -(C + alpha * currentLambda + gamma * dC) / ((1f + gamma) * wSum + alpha);

            if (i == 0) grab.lambdaRotation.x += deltaLambda; else if (i == 1) grab.lambdaRotation.y += deltaLambda; else grab.lambdaRotation.z += deltaLambda;

            if (!pState.isKinematic) XPBDMath.ApplyDeltaRotation(pState, XPBDMath.ApplyInvInertiaWorld(deltaLambda * cAxis, pState.q, pState.qInertia, pState.invInertiaLocal));
            if (!cState.isKinematic) XPBDMath.ApplyDeltaRotation(cState, XPBDMath.ApplyInvInertiaWorld(deltaLambda * -cAxis, cState.q, cState.qInertia, cState.invInertiaLocal));
        }
    }

    private void SolveGrabDistance(HydratedGrabJoint grab, float dt, Dictionary<Rigidbody, XPBDState> states)
    {
        var pState = states[grab.torsoRb];
        var cState = states[grab.itemRb];

        if (pState.isKinematic && cState.isKinematic) return;

        // 1. TARGETS (Stateless)
        Vector3 eyePos = grab.grabberController.GetEyePosSim();
        Vector3 lookDir = grab.grabberController.lookRot * Vector3.forward;
        Vector3 worldHoldPos = eyePos + (lookDir * grab.networkedData.grabDistance);

        Vector3 eyePos_prev = grab.grabberController.GetPreviousEyePosSim(pState.p_prev);
        Vector3 lookDir_prev = grab.grabberController.previousLookRot * Vector3.forward;
        Vector3 worldHoldPos_prev = eyePos_prev + (lookDir_prev * grab.networkedData.grabDistance);

        Vector3 r0 = worldHoldPos - pState.p;
        Vector3 r1 = cState.q * grab.networkedData.localGrabOffset;

        Vector3 p0 = pState.p + r0;
        Vector3 p1 = cState.p + r1;
        Vector3 dir = p1 - p0;

        float dist = dir.magnitude;
        // NOTE: We no longer return early if dist < 1e-6f. 
        // We must run the loop even at 0 distance to apply 3D Velocity Damping!

        // 2. STRENGTH & EFFORT CURVES
        float massMultiplier = Mathf.Clamp(cState.rb.mass, 1f, 10f);
        float scaledStrength = grab.networkedData.grabStrength * massMultiplier;

        float normalizedDist = Mathf.Clamp01(dist / Mathf.Max(0.01f, dragRange));
        float stretchMultiplier = distanceComplianceCurve.Evaluate(normalizedDist);

        float baseCompliance = 1f / Mathf.Max(0.01f, scaledStrength);
        float alpha = (baseCompliance * stretchMultiplier) / (dt * dt);
        float betaTilde = 0.5f * dt * grab.networkedData.grabDamping;
        float gamma = (alpha * betaTilde) / dt;

        Vector3 dxTarget = worldHoldPos - worldHoldPos_prev;
        Vector3 dx1 = cState.p - cState.p_prev;
        Vector3 dw1 = XPBDMath.GetDeltaTheta(cState.q_prev, cState.q);

        float w0_solver = 0f; // 1-Way Constraint God-Point
        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };

        // 3. THE 3D ORTHOGONAL SOLVER
        for (int i = 0; i < 3; i++)
        {
            Vector3 cAxis = axes[i];
            float C = Vector3.Dot(dir, cAxis); // The error purely along this axis (X, Y, or Z)

            Vector3 gradP1 = cAxis;
            Vector3 gradQ1 = Vector3.Cross(r1, cAxis);

            float w1 = cState.isKinematic ? 0f : cState.invMass + Vector3.Dot(gradQ1, XPBDMath.ApplyInvInertiaWorld(gradQ1, cState.q, cState.qInertia, cState.invInertiaLocal));
            float wSum = w0_solver + w1;
            if (wSum < 1e-6f) continue;

            // Damping velocity strictly along this axis (This kills the tangential orbit!)
            float dC = Vector3.Dot(-cAxis, dxTarget) + Vector3.Dot(gradP1, dx1) + Vector3.Dot(gradQ1, dw1);

            float currentLambda = i == 0 ? grab.lambdaPosition.x : (i == 1 ? grab.lambdaPosition.y : grab.lambdaPosition.z);
            float deltaLambda = -(C + alpha * currentLambda + gamma * dC) / ((1f + gamma) * wSum + alpha);

            if (i == 0) grab.lambdaPosition.x += deltaLambda;
            else if (i == 1) grab.lambdaPosition.y += deltaLambda;
            else grab.lambdaPosition.z += deltaLambda;

            if (!cState.isKinematic)
            {
                cState.p += cState.invMass * deltaLambda * gradP1;
                XPBDMath.ApplyDeltaRotation(cState, XPBDMath.ApplyInvInertiaWorld(deltaLambda * gradQ1, cState.q, cState.qInertia, cState.invInertiaLocal));
            }
        }

        // 4. THE 1-WAY RECOIL APPLICATION
        if (!pState.isKinematic)
        {
            float dragResist = Mathf.Max(1f, grab.networkedData.dragResistance);
            float effectiveInvMass0 = pState.invMass / dragResist;

            // Calculate exact combined 3D tension Force (Impulse / dt)
            float currentTensionNewtons = grab.lambdaPosition.magnitude / dt;
            float recoilMultiplier = recoilTensionCurve.Evaluate(currentTensionNewtons);

            // The recoil is simply the accumulated 3D lambda force pulling backward on the Torso
            Vector3 rawRecoilShift = -grab.lambdaPosition * effectiveInvMass0;
            Vector3 finalRecoilShift = rawRecoilShift * recoilMultiplier;

            float maxSafeShift = 5f * dt;
            if (finalRecoilShift.sqrMagnitude > maxSafeShift * maxSafeShift)
            {
                finalRecoilShift = finalRecoilShift.normalized * maxSafeShift;
            }

            pState.p += finalRecoilShift;
        }
    }

    private void SolveGrabRotation(HydratedGrabJoint grab, float dt, Dictionary<Rigidbody, XPBDState> states)
    {
        var pState = states[grab.torsoRb];
        var cState = states[grab.itemRb];

        if (pState.isKinematic && cState.isKinematic) return;

        Quaternion targetQ = grab.grabberController.lookRot * grab.networkedData.targetLocalRotation;
        Quaternion targetQ_prev = grab.grabberController.previousLookRot * grab.networkedData.targetLocalRotation;

        Quaternion qError = targetQ * Quaternion.Inverse(cState.q);
        if (qError.w < 0f) { qError.x = -qError.x; qError.y = -qError.y; qError.z = -qError.z; qError.w = -qError.w; }

        Vector3 v = new Vector3(qError.x, qError.y, qError.z);
        float sinHalfAngle = v.magnitude;
        if (sinHalfAngle < 1e-6f) return;

        Vector3 axis = v / sinHalfAngle;
        float angleRad = 2f * Mathf.Atan2(sinHalfAngle, qError.w);

        float alpha = (1f / Mathf.Max(0.01f, grab.networkedData.grabStrength)) / (dt * dt);
        float betaTilde = 0.5f * dt * grab.networkedData.grabDamping;
        float gamma = (alpha * betaTilde) / dt;

        Vector3 rotVec = axis * angleRad;
        Vector3[] orthogonalAxes = { Vector3.right, Vector3.up, Vector3.forward };

        for (int i = 0; i < 3; i++)
        {
            Vector3 cAxis = orthogonalAxes[i];
            float C = Vector3.Dot(rotVec, cAxis);

            // 1-Way Rotational Coupling (w0 = 0f)
            float w0 = 0f;
            float w1 = cState.isKinematic ? 0f : Vector3.Dot(-cAxis, XPBDMath.ApplyInvInertiaWorld(-cAxis, cState.q, cState.qInertia, cState.invInertiaLocal));

            float wSum = w0 + w1;
            if (wSum < 1e-6f) continue;

            Vector3 dwTarget = XPBDMath.GetDeltaTheta(targetQ_prev, targetQ);
            float dC = Vector3.Dot(cAxis, dwTarget) + Vector3.Dot(-cAxis, XPBDMath.GetDeltaTheta(cState.q_prev, cState.q));

            float currentLambda = i == 0 ? grab.lambdaRotation.x : (i == 1 ? grab.lambdaRotation.y : grab.lambdaRotation.z);
            float deltaLambda = -(C + alpha * currentLambda + gamma * dC) / ((1f + gamma) * wSum + alpha);

            if (i == 0) grab.lambdaRotation.x += deltaLambda; else if (i == 1) grab.lambdaRotation.y += deltaLambda; else grab.lambdaRotation.z += deltaLambda;

            if (!cState.isKinematic)
            {
                XPBDMath.ApplyDeltaRotation(cState, XPBDMath.ApplyInvInertiaWorld(deltaLambda * -cAxis, cState.q, cState.qInertia, cState.invInertiaLocal));
            }
        }
    }

    // --- FINAL VELOCITY DERIVATION ---

    private void DeriveAllVelocities(float dt)
    {
        foreach (var kvp in _globalStates)
        {
            var state = kvp.Value;
            if (state.isKinematic) continue;

            state.v = (state.p - state.p_prev) / dt;
            state.w = XPBDMath.GetDeltaTheta(state.q_prev, state.q) / dt;
        }
    }

    private void ApplyAllToUnity()
    {
        foreach (var kvp in _globalStates)
        {
            var state = kvp.Value;
            if (state.isKinematic) continue;

            state.rb.linearVelocity = state.v;
            state.rb.angularVelocity = state.w;
            state.rb.position = state.p;
            state.rb.rotation = state.q;
        }
    }

    public bool AddTempJoint(NetworkTempJoint newJoint)
    {
        for (int i = 0; i < NetworkedTempJoints.Length; i++)
        {
            // An empty slot is one where the parentId hasn't been set (or was cleared)
            if (!NetworkedTempJoints[i].parentId.IsValid)
            {
                NetworkedTempJoints.Set(i, newJoint);
                return true; // Successfully added
            }
        }

        Debug.LogWarning("XPBDGlobalManager: NetworkedTempJoints array is full! Cannot add new joint.");
        return false;
    }
    public bool RemoveTempJoint(NetworkId parentId, NetworkId childId)
    {
        for (int i = 0; i < NetworkedTempJoints.Length; i++)
        {
            var joint = NetworkedTempJoints[i];

            // Check if this slot contains the exact joint we want to remove
            if (joint.parentId == parentId && joint.childId == childId)
            {
                // Overwriting with default struct resets IDs, making .IsValid false
                NetworkedTempJoints.Set(i, default(NetworkTempJoint));
                return true; // Successfully removed
            }
        }

        Debug.LogWarning($"XPBDGlobalManager: Could not find joint between Parent {parentId} and Child {childId} to remove.");
        return false;
    }

    public bool AddGrabJoint(NetworkGrabJoint newJoint)
    {
        for (int i = 0; i < NetworkedGrabJoints.Length; i++)
        {
            // An empty slot is one where the parentId hasn't been set (or was cleared)
            if (!NetworkedGrabJoints[i].grabberId.IsValid)
            {
                NetworkedGrabJoints.Set(i, newJoint);
                return true; // Successfully added
            }
        }

        Debug.LogWarning("XPBDGlobalManager: NetworkedGrabJoints array is full! Cannot add new joint.");
        return false;
    }

    public bool RemoveGrabJoint(NetworkId grabberId, NetworkId itemId)
    {
        for (int i = 0; i < NetworkedGrabJoints.Length; i++)
        {
            var joint = NetworkedGrabJoints[i];

            // Check if this slot contains the exact joint we want to remove
            if (joint.grabberId == grabberId && joint.itemId == itemId)
            {
                // Overwriting with default struct resets IDs, making .IsValid false
                NetworkedGrabJoints.Set(i, default(NetworkGrabJoint));
                return true; // Successfully removed
            }
        }

        Debug.LogWarning($"XPBDGlobalManager: Could not find joint between Parent {grabberId} and Child {itemId} to remove.");
        return false;
    }
}