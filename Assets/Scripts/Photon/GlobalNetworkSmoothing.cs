using UnityEngine;

public class GlobalNetworkSmoothing : MonoBehaviour
{
    public static GlobalNetworkSmoothing Instance { get; private set; }

    [Header("Global Network Presentation Smoothing")]
    public bool disableAllSmoothing;
    [Min(0f)] public float halfLife = 0.035f;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static float GetBlend(float deltaTime, bool enableLocalSmoothing)
    {
        if (!enableLocalSmoothing || Instance == null || Instance.disableAllSmoothing || Instance.halfLife <= 0f) return 1f;
        return 1f - Mathf.Pow(0.5f, Mathf.Max(deltaTime, 0f) / Instance.halfLife);
    }

    public static Vector3 Smooth(Vector3 current, Vector3 target, float deltaTime, bool enableLocalSmoothing)
    {
        return Vector3.Lerp(current, target, GetBlend(deltaTime, enableLocalSmoothing));
    }

    public static Quaternion Smooth(Quaternion current, Quaternion target, float deltaTime, bool enableLocalSmoothing)
    {
        return Quaternion.Slerp(current, target, GetBlend(deltaTime, enableLocalSmoothing));
    }
}