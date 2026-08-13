using Fusion;
using UnityEngine;

public enum SmoothingSchedule
{
    Render,
    AfterRender,
    Update, 
    LateUpdate
}

[DefaultExecutionOrder(175)]
public class VisualNetworkSmoothing : NetworkBehaviour, IAfterRender
{
    [Header("Local Control")]
    public bool enableLocalSmoothing = true;

    [Header("Transforms")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Transform smoothedTransform;

    [Header("Schedule")]
    [SerializeField] private SmoothingSchedule smoothingSchedule = SmoothingSchedule.AfterRender;

    [Header("Correction")]
    [SerializeField, Min(0f)] private float snapDistance = 2f;
    [SerializeField, Range(0f, 180f)] private float snapAngle = 90f;

    private Vector3 _localPositionOffset;
    private Quaternion _localRotationOffset;
    private Vector3 _localScaleRatio;

    private Vector3 _smoothedPosition;
    private Quaternion _smoothedRotation;
    private Vector3 _smoothedLocalScale;

    private bool _initialized;
    private bool _wasVisible;

    public override void Spawned()
    {
        CacheOffsets();
        SnapToTarget();
    }

    public override void Render()
    {
        if (smoothingSchedule == SmoothingSchedule.Render) Smooth();
    }

    public void AfterRender()
    {
        if (smoothingSchedule == SmoothingSchedule.AfterRender) Smooth();
    }

    private void LateUpdate()
    {
        if (smoothingSchedule == SmoothingSchedule.LateUpdate) Smooth();
    }

    private void CacheOffsets()
    {
        if (targetTransform == null || smoothedTransform == null) return;

        _localPositionOffset = targetTransform.InverseTransformPoint(smoothedTransform.position);
        _localRotationOffset = Quaternion.Inverse(targetTransform.rotation) * smoothedTransform.rotation;
        _localScaleRatio = Divide(smoothedTransform.localScale, targetTransform.localScale);
    }

    private void Smooth()
    {
        if (targetTransform == null || smoothedTransform == null) return;

        bool visible = smoothedTransform.gameObject.activeInHierarchy;

        if (!visible)
        {
            _wasVisible = false;
            return;
        }

        Vector3 targetPosition = targetTransform.TransformPoint(_localPositionOffset);
        Quaternion targetRotation = targetTransform.rotation * _localRotationOffset;
        Vector3 targetScale = Vector3.Scale(targetTransform.localScale, _localScaleRatio);

        if (!_initialized || !_wasVisible || Vector3.Distance(_smoothedPosition, targetPosition) > snapDistance || Quaternion.Angle(_smoothedRotation, targetRotation) > snapAngle)
        {
            SnapToTarget();
            _wasVisible = true;
            return;
        }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        _smoothedPosition = GlobalNetworkSmoothing.Smooth(_smoothedPosition, targetPosition, dt, enableLocalSmoothing);
        _smoothedRotation = targetRotation;
        _smoothedLocalScale = targetScale;

        smoothedTransform.SetPositionAndRotation(_smoothedPosition, _smoothedRotation);
        smoothedTransform.localScale = _smoothedLocalScale;

        _wasVisible = true;
    }

    public void SnapToTarget()
    {
        if (targetTransform == null || smoothedTransform == null) return;

        _smoothedPosition = targetTransform.TransformPoint(_localPositionOffset);
        _smoothedRotation = targetTransform.rotation * _localRotationOffset;
        _smoothedLocalScale = Vector3.Scale(targetTransform.localScale, _localScaleRatio);

        smoothedTransform.SetPositionAndRotation(_smoothedPosition, _smoothedRotation);
        smoothedTransform.localScale = _smoothedLocalScale;

        _initialized = true;
        _wasVisible = smoothedTransform.gameObject.activeInHierarchy;
    }

    private Vector3 Divide(Vector3 value, Vector3 divisor)
    {
        return new Vector3(
            divisor.x == 0f ? value.x : value.x / divisor.x,
            divisor.y == 0f ? value.y : value.y / divisor.y,
            divisor.z == 0f ? value.z : value.z / divisor.z
        );
    }
}
