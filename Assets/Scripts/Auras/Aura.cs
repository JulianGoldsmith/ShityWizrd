using UnityEngine;

public abstract class Aura : ScriptableObject
{
    public AURA_ID unique_label;
    public float duration;
    public abstract void OnApply(AuraContainer container);
    public abstract void OnTick(AuraContainer container);
    public abstract void OnExpire(AuraContainer container);
}

public enum AURA_ID
{
    NULL = 0,

    ANTIGRAVITY = 1,

    MAX_N
}