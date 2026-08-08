using UnityEngine;

[System.Serializable]
public class ItemAnimation
{
    public AnimationClip clip;

    [Min(0.01f)]
    public float speedMultiplier = 1f;
}