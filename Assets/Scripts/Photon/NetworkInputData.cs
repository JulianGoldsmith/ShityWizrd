using Fusion;
using UnityEngine;

public enum EInputButton
{
    LEFT_CLICK = 0,
    RIGHT_CLICK = 1,
    JUMP = 2,
    PICKUP = 3,
    DROP = 4,
    SPRINT = 5,
    ADD = 6, 
    SUBTRACT = 7,
}
public struct NetworkInputData : INetworkInput
{
    public Vector3 direction;
    public Quaternion lookRotation;
    public NetworkButtons buttons;
    public Vector2 yawpitch;
    public float scroll;
}