using UnityEngine;
using Fusion;

public class ProxyInputExtrapolation : NetworkBehaviour
{

    [Header("Proxy Extrapolation")]
    private int _lastInputTick;
    private Vector2 _previousSmoothedInput;
    private Vector2 _lastReceivedAuthoritativeInput;
    public float inputSmoothingFactor = 0.1f;

}
