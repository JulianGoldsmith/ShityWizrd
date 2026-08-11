using UnityEngine;

public static class NetworkSmoothing
{
    public const float HalfLife = 0.1f;

    public static float ExponentialBlend(float deltaTime)
    {
        if (deltaTime <= 0f) return 0f;
        return 1f - Mathf.Pow(0.5f, deltaTime / HalfLife);
    }

    public static Vector3 ExponentialSmooth(Vector3 current, Vector3 target, float deltaTime)
    {
        return Vector3.Lerp(current, target, ExponentialBlend(deltaTime));
    }

   

}
