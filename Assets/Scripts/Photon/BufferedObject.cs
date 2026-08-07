using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class BufferedObject : NetworkBehaviour
{
    [Header("Buffered Roots")]
    [SerializeField] private Transform[] physicsRoots;
    [SerializeField] private Transform[] visualRoots;

    [Header("Awake Physics State")]
    [SerializeField] private bool awakeIsKinematic;

    [Networked] public int WakeTick { get; set; }
    [Networked] public int SleepTick { get; set; }

    public bool IsAwake => WakeTick > SleepTick;
    public bool CanRunSimulationCode => IsAwake || _isPreparingWake;

    private readonly List<IBufferableComponent> _bufferableComponents = new List<IBufferableComponent>();
    private Rigidbody _rigidbody;
    private NetworkRigidbody3D _networkRigidbody;
    private bool _hasAppliedState;
    private bool _locallyAwake;
    private bool _isPreparingWake;
    private int _appliedWakeTick = int.MinValue;
    private int _appliedSleepTick = int.MinValue;

    public override void Spawned()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _networkRigidbody = GetComponent<NetworkRigidbody3D>();
        CacheBufferableComponents();
        Runner.SetIsSimulated(Object, true);

        if (HasStateAuthority && WakeTick == 0 && SleepTick == 0)
        {
            WakeTick = -1;
            SleepTick = 0;
        }

        ApplyDormantPhysicsInvariant();
        SetRootsActive(visualRoots, false);
    }

    public override void FixedUpdateNetwork()
    {
        ReconcileSimulationState();

        if (!CanRunSimulationCode)
            ApplyDormantPhysicsInvariant();
    }

    public override void Render()
    {
        SetRootsActive(visualRoots, IsAwake);
    }

    public void Wake()
    {
        WakeTick = Runner.Tick;
        _isPreparingWake = false;
        ApplyStateImmediately();
    }

    public void BeginWakeInitialization()
    {
        if (IsAwake)
            return;

        _isPreparingWake = true;
        DetachForWake();

        SetRootsActive(physicsRoots, true);
        ApplyAwakePhysicsState();
    }

    public void CompleteWakeInitialization()
    {
        Wake();
    }

    public void Sleep()
    {
        _isPreparingWake = false;
        SleepTick = Runner.Tick;
        ApplyStateImmediately();
    }

    public void ApplyStateImmediately()
    {
        ReconcileSimulationState();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_hasAppliedState && _locallyAwake)
            NotifySleep(SleepTick);

        SetRootsActive(physicsRoots, false);
        SetRootsActive(visualRoots, false);
        _hasAppliedState = false;
        _locallyAwake = false;
        _isPreparingWake = false;
        base.Despawned(runner, hasState);
    }

    private void CacheBufferableComponents()
    {
        _bufferableComponents.Clear();
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is not IBufferableComponent bufferableComponent)
                continue;

            _bufferableComponents.Add(bufferableComponent);
            bufferableComponent.BindBufferedObject(this);
        }
    }

    private void ReconcileSimulationState()
    {
        bool shouldBeAwake = IsAwake;
        bool wakeChanged = shouldBeAwake && (!_hasAppliedState || !_locallyAwake || _appliedWakeTick != WakeTick);
        bool sleepChanged = !shouldBeAwake && (!_hasAppliedState || _locallyAwake || _appliedSleepTick != SleepTick);

        if (wakeChanged)
        {
            if (_hasAppliedState && _locallyAwake)
                NotifySleep(SleepTick);

            ApplyAwakeState();
        }
        else if (sleepChanged)
        {
            ApplySleepState();
        }
    }

    private void ApplyAwakeState()
    {
        bool isActivationTick = Runner.Tick == WakeTick;

        DetachForWake();
        SetRootsActive(physicsRoots, true);

        foreach (IBufferableComponent bufferableComponent in _bufferableComponents)
            bufferableComponent.OnBufferedWake(WakeTick, isActivationTick);
        
        ApplyAwakePhysicsState();

        _hasAppliedState = true;
        _locallyAwake = true;
        _appliedWakeTick = WakeTick;
        _appliedSleepTick = SleepTick;
    }

    private void DetachForWake()
    {
        if (transform.parent != null) transform.SetParent(null, true);
    }

    private void ApplyAwakePhysicsState()
    {
        if (_networkRigidbody != null)
            _networkRigidbody.RBIsKinematic = awakeIsKinematic;

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = awakeIsKinematic;
            _rigidbody.detectCollisions = true;

            if (!awakeIsKinematic)
                _rigidbody.WakeUp();
        }
    }

    private void ApplySleepState()
    {
        SetRootsActive(physicsRoots, false);
        ApplyDormantPhysicsInvariant();
        NotifySleep(SleepTick);
        _hasAppliedState = true;
        _locallyAwake = false;
        _appliedWakeTick = WakeTick;
        _appliedSleepTick = SleepTick;
    }

    private void ApplyDormantPhysicsInvariant()
    {
        SetRootsActive(physicsRoots, false);

        if (_rigidbody != null)
        {
            if (!_rigidbody.isKinematic)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.Sleep();
            }

            _rigidbody.detectCollisions = false;
            _rigidbody.isKinematic = true;
        }

        if (_networkRigidbody != null && !_networkRigidbody.RBIsKinematic)
            _networkRigidbody.RBIsKinematic = true;
    }

    private void NotifySleep(int sleepTick)
    {
        foreach (IBufferableComponent bufferableComponent in _bufferableComponents)
            bufferableComponent.OnBufferedSleep(sleepTick);
    }

    private static void SetRootsActive(Transform[] roots, bool isActive)
    {
        if (roots == null)
            return;

        foreach (Transform root in roots)
        {
            if (root != null && root.gameObject.activeSelf != isActive)
                root.gameObject.SetActive(isActive);
        }
    }
}
