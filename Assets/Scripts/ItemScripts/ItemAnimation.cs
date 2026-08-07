using UnityEngine;

[CreateAssetMenu(fileName = "NewItemAnimation", menuName = "Items/Item Animation")]
public class ItemAnimation : ScriptableObject
{
    public AnimationClip clip;

    [Min(0.01f)]
    public float speedMultiplier = 1f;

    public float PlaybackDuration
    {
        get
        {
            if (clip == null) return 0f;
            return clip.length / Mathf.Max(0.01f, speedMultiplier);
        }
    }

    private void OnValidate()
    {
        speedMultiplier = Mathf.Max(0.01f, speedMultiplier);
    }
}

public struct EyePosAndLookDir
{
    public Vector3 EyePosition;
    public Vector3 Forward;
    public Vector3 Up;
    public Vector3 Right;

    public EyePosAndLookDir(Vector3 eyePosition, Vector3 forward, Vector3 up)
    {
        EyePosition = eyePosition;
        Forward = forward.normalized;
        Up = up.normalized;
        Right = Vector3.Cross(Up, Forward).normalized;
    }
}