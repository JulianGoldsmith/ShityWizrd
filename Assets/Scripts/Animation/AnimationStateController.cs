using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Linq;

public class AnimationStateController : NetworkBehaviour
{
    [HideInInspector] public DeterministicNetworkAnimator _detAnimator;

    [Header("Animator")]
    [SerializeField] private Animator _animator;

    [Header("Animation Clips")]
    [Tooltip("The list of all animations. ORDER MATTERS and must match indices.")]
    [SerializeField] private List<AnimClipConfig> _clipsConfig;
    [SerializeField] private AnimationClip _tPoseClip;

    public enum AnimState
    {
        Locomotion,
        // Future states can be added here
        // Attack,
        // Flinch
    }

    [Networked]
    public AnimState CurrentState { get; set; }

    [SerializeField] private const int IDLE_INDEX = 0;
    [SerializeField] private const int FORWARD_INDEX = 1;
    [SerializeField] private const int BACKWARD_INDEX = 2;
    [SerializeField] private const int LEFT_INDEX = 3;
    [SerializeField] private const int RIGHT_INDEX = 4;
    [SerializeField] private const int CLIP_CAPACITY = 5; // We now have 5 clips

    [Header("Blending Speeds")]
    [SerializeField] private float _blendSpeed = 5.0f;

    [Header("Blending Speeds")]
    [SerializeField] private float _locomotionBlendSpeed = 10.0f;

    [Networked, Capacity(CLIP_CAPACITY)]
    public NetworkArray<float> ClipWeights { get; }

    [Networked, Capacity(CLIP_CAPACITY)]
    public NetworkArray<float> ClipTimes { get; }

    private Vector2 _targetMoveVector;

    public Transform armatureRoot;

    [Header("Needed for blender imports Z = UP")]
    public bool zIsUp = true;
    public float RootMotionOffsetFromOrigin => _detAnimator.RootMotionVerticalDelta;
    public Vector2 RootMotionDeltaThisUpdate => _detAnimator.RootMotionHorizontalDelta;
    public Quaternion RootMotionRotation => _detAnimator?.RootMotionRotation ?? Quaternion.identity;

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();
    }

    public override void Spawned()
    {
        List<AnimationClip> clips = _clipsConfig.Select(config => config.Clip).ToList();

        _detAnimator = new DeterministicNetworkAnimator(_animator, clips, _tPoseClip, armatureRoot, zIsUp);

        if (Object.HasStateAuthority)
        {
            ClipWeights.Set(IDLE_INDEX, 1.0f);
            CurrentState = AnimState.Locomotion; // EntryState
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
            switch (CurrentState)
            {
                case AnimState.Locomotion:
                    RunLocomotionLogic(targetWeights);
                    break;
                    // [Future]
                    // case AnimState.Attack:
                    //     RunAttackLogic(targetWeights);
                    //     break;
            }
        }
        else
        {
            for (int i = 0; i < CLIP_CAPACITY; i++)
            {
                targetWeights[i] = ClipWeights[i];
            }
        }

        ApplyBlendingAndAdvanceTime(targetWeights);

        _detAnimator?.ApplyStateToGraph(ClipWeights, ClipTimes);
    }

    private void RunLocomotionLogic(float[] targetWeights)
    {
        float moveMagnitude = _targetMoveVector.magnitude;

        // --- This is a "Star Blend" ---
        // 1. Idle weight is 1.0 minus the total movement amount
        targetWeights[IDLE_INDEX] = 1.0f - moveMagnitude;

        // 2. Directional weights are the normalized direction * total movement
        if (moveMagnitude > 0.001f)
        {
            float normX = _targetMoveVector.x / moveMagnitude;
            float normY = _targetMoveVector.y / moveMagnitude;

            targetWeights[FORWARD_INDEX] = Mathf.Max(0, normY) * moveMagnitude;
            targetWeights[BACKWARD_INDEX] = Mathf.Max(0, -normY) * moveMagnitude;
            targetWeights[LEFT_INDEX] = Mathf.Max(0, -normX) * moveMagnitude;
            targetWeights[RIGHT_INDEX] = Mathf.Max(0, normX) * moveMagnitude;
        }
        else
        {
            // Not moving, all weights 0 except idle
            targetWeights[FORWARD_INDEX] = 0;
            targetWeights[BACKWARD_INDEX] = 0;
            targetWeights[LEFT_INDEX] = 0;
            targetWeights[RIGHT_INDEX] = 0;
        }
    }


    private void ApplyBlendingAndAdvanceTime(float[] targetWeights)
    {
        float blendDelta = Runner.DeltaTime * _locomotionBlendSpeed;

        for (int i = 0; i < _clipsConfig.Count; i++)
        {
            // Get the config for this clip
            AnimClipConfig config = _clipsConfig[i];
            float newWeight = ClipWeights[i];

            // A. Update Weights (Host or Input Authority)
            if (Object.HasStateAuthority || Object.HasInputAuthority)
            {
                float currentWeight = ClipWeights[i];
                float targetWeight = targetWeights[i];
                newWeight = Mathf.MoveTowards(currentWeight, targetWeight, blendDelta);
                ClipWeights.Set(i, newWeight);
            }

            // B. Advance Time (Everyone)
            if (newWeight > 0.001f) // Only advance if active
            {
                // --- [MODIFIED] ---
                // We now multiply DeltaTime by the clip's configured speed.
                float timeDelta = Runner.DeltaTime * config.Speed;
                float newTime = ClipTimes[i] + timeDelta;

                if (config.Clip.isLooping)
                {
                    newTime %= config.Clip.length;
                }
                else
                {
                    newTime = Mathf.Clamp(newTime, 0, config.Clip.length);
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