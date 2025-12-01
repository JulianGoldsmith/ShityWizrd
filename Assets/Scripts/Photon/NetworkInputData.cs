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
    SELF_BONK = 8,
    UN_SELF_BONK = 9,
    TEST_COUNT = 10,
}
public struct NetworkInputData : INetworkInput
{
    public Vector3 direction;
    public Quaternion lookRotation;
    public NetworkButtons buttons;
    public Vector2 yawpitch;
    public float scroll;

    public Vector3 dragTargetPos; 
    public Vector3 dragFacingDir;
}