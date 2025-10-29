using UnityEngine;
using Fusion;
using System.Collections.Generic;

public class AnimationStateController : NetworkBehaviour
{
    private DeterministicNetworkAnimator _detAnimator;

    [Header("Animator")]
    [SerializeField] private Animator _animator;

    [Header("Animation Clips")]
    [Tooltip("The list of all animations. ORDER MATTERS and must match indices.")]
    [SerializeField] private List<AnimationClip> _clips;

    private const int IDLE_INDEX = 0;
    private const int WALK_INDEX = 1;
    private const int CLIP_CAPACITY = 2;

    [Header("Blending Speeds")]
    [SerializeField] private float _blendSpeed = 5.0f;

    [Networked, Capacity(CLIP_CAPACITY)]
    public NetworkArray<float> ClipWeights { get; }

    [Networked, Capacity(CLIP_CAPACITY)]
    public NetworkArray<float> ClipTimes { get; }

    private Vector2 _targetMoveVector;

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();
    }

    public override void Spawned()
    {
        _detAnimator = new DeterministicNetworkAnimator(_animator, _clips);

        if (Object.HasStateAuthority)
        {
            ClipWeights.Set(IDLE_INDEX, 1.0f);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _detAnimator?.DestroyGraph();
    }

    public void SetTargetMovement(Vector2 moveVector)
    {
        _targetMoveVector = moveVector;
    }

    public void SimulateAnimation()
    {
        float[] targetWeights = new float[CLIP_CAPACITY];

        if (Object.HasStateAuthority || Object.HasInputAuthority)
        {
            float moveSpeed = Mathf.Clamp01(_targetMoveVector.magnitude);
            targetWeights[WALK_INDEX] = moveSpeed;
            targetWeights[IDLE_INDEX] = 1.0f - moveSpeed;
        }
        else
        {
            for (int i = 0; i < CLIP_CAPACITY; i++)
            {
                targetWeights[i] = ClipWeights[i];
            }
        }

        ApplyBlendingAndAdvanceTime(targetWeights, _blendSpeed);

        _detAnimator?.ApplyStateToGraph(ClipWeights, ClipTimes);
    }

    private void ApplyBlendingAndAdvanceTime(float[] targetWeights, float blendSpeed)
    {
        float blendDelta = Runner.DeltaTime * _blendSpeed;

        for (int i = 0; i < _clips.Count; i++)
        {
            float newWeight = ClipWeights[i];

            if (Object.HasStateAuthority || Object.HasInputAuthority)
            {
                float currentWeight = ClipWeights[i];
                float targetWeight = targetWeights[i];
                newWeight = Mathf.MoveTowards(currentWeight, targetWeight, blendDelta);
                ClipWeights.Set(i, newWeight);
            }
            
            if (newWeight > 0.001f)
            {
                float newTime = ClipTimes[i] + Runner.DeltaTime;
                if (_clips[i].isLooping)
                {
                    newTime %= _clips[i].length;
                }
                else
                {
                    newTime = Mathf.Clamp(newTime, 0, _clips[i].length);
                }

                ClipTimes.Set(i, newTime);
            }
        }
    }

    public override void Render()
    {
        _detAnimator?.ApplyStateToGraph(ClipWeights, ClipTimes);
    }
}