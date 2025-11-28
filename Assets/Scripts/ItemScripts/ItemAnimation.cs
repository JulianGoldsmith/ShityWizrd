using UnityEngine;

[CreateAssetMenu(fileName = "NewItemAnimation", menuName = "Items/Item Animation")]
public class ItemAnimation : ScriptableObject
{
    public AnimationClip clip;

    // Helper to get duration easily
    public float duration => clip != null ? clip.length : 0f;

    // Optional: Speed multiplier if you want to tweak feel without re-animating
    public float speedMultiplier = 1.0f;

    public float castPointTime = 0.2f;

    public float castEndTime = 0.4f;
    public bool IsFinished(float realTimePassed)
    {
        if (clip == null) return true; 

        return (realTimePassed * speedMultiplier) >= clip.length;
    }
    public bool HasPassedCastPoint(float realTimePassed)
    {
        if (clip == null) return true;

        return (realTimePassed * speedMultiplier) >= castPointTime;
    }
    public bool HasPassedEndPoint(float realTimePassed)
    {
        if (clip == null) return true;

        return (realTimePassed * speedMultiplier) >= castEndTime;
    }
    public bool IsInActiveWindow(float realTime)
    {
        float animTime = realTime * speedMultiplier;
        return animTime >= castPointTime && animTime < castEndTime;
    }
}

[System.Serializable]
public struct ItemAnimationSample
{
    public Vector3 localOffset;
    public Vector3 localEuler;
    public float force;

    public Vector3 worldPosition;
    public Quaternion worldRotation;
}
public struct EyePosAndLookDir
{
    public Vector3 EyePosition;
    public Vector3 Forward;
    public Vector3 Up;
    public Vector3 Right;

    public EyePosAndLookDir(Vector3 eyePos, Vector3 forward, Vector3 up)
    {
        EyePosition = eyePos;
        Forward = forward.normalized;
        Up = up.normalized;
        Right = Vector3.Cross(Up, Forward).normalized;
    }
}
