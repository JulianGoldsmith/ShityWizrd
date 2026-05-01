using Fusion;
using Fusion.Addons.Physics;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    public static BasicSpawner Instance { get; private set; }

    public NetworkRunner _runner;
    private static NetworkRunner static_runner;

    [SerializeField] private NetworkPrefabRef _playerPrefab;
    [SerializeField] private NetworkPrefabRef _handsPrefab;
    [SerializeField] private NetworkPrefabRef _itemPrefab;
    [SerializeField] private NetworkPrefabRef _testCounterPrefab;

    public Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();

    private List<double> pings = new List<double>();
    private float lastPingUpdateTime = 0f;
    private int displayedPing = 0;

    [Header("Extrapolation Decay Settings")]
    [Tooltip("How many ticks into the unconfirmed future before we start braking?")]
    public int graceTicks = 3;

    [Tooltip("The tick where the multiplier hits absolute zero.")]
    public int maxDecayTicks = 15;

    [Tooltip("X-axis: 0 to 1 (Progress). Y-axis: 1 to 0 (Multiplier).")]
    public AnimationCurve decayCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    public static NetworkObject Spawn(NetworkPrefabRef prefab, Vector3 pos, Quaternion rot, NetworkRunner.OnBeforeSpawned onBeforeSpawned = null)
    {
        // Deal with spawning objects.
        // If host, spawn the object.
        // If client, send a request to spawn the object.
        // How do we cast spells?
        // Do we just send a request to the host to start casting?
        // Or do we cast, then have the host correct it?
        if (static_runner == null || !static_runner.IsServer)
            return null;

        // Do we need to assign input authority here?
        // Would be relevant for nodes that change based on input, such 
        // as 'homing toward mouse cursor' or 'click a button to detonate'.

        // We allow a delegate (OnBeforeSpawned) to be passed, such that
        // a method can be called on the object, before replicating
        // it across all instances.
        return static_runner.Spawn(prefab, pos, rot, null, onBeforeSpawned);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            // Create a unique position for the player
            Vector3 spawnPosition;

            if (GameController.Instance != null)
            {
                LevelGenerator levelGen = GameController.Instance.levelGenerator;

                if (levelGen != null && levelGen.IsLevelGenerated)
                {
                    spawnPosition = levelGen.StartRoomSpawnPoint.transform.position;
                }
                else
                {
                    spawnPosition = new Vector3((player.RawEncoded % runner.Config.Simulation.PlayerCount) * 1, 1, 0);
                }
            }
            else
            {
                spawnPosition = new Vector3((player.RawEncoded % runner.Config.Simulation.PlayerCount) * 1, 1, 0);

            }
            //Vector3 handsoffset = new Vector3(0,0,-1);
            //NetworkObject networkHandsObject = runner.Spawn(_handsPrefab, spawnPosition + handsoffset, Quaternion.identity, player);

            // We spawn hands first so that the player OnSpawned can grab all the required references.
            // Unfortunately, everyone needs the references so we can't just pass a OnBeforeSpawned delegate
            // or do it here, we need everyone to find those references themselves for each player OnSpawn.
            // That's done, at the moment, in PhysicsHandController.
            NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);

            Vector3 itemoffset = new Vector3(0, 2, -2);
            NetworkObject networkItem = runner.Spawn(_itemPrefab, spawnPosition + itemoffset, Quaternion.identity, player);
            NetworkObject testCounter = runner.Spawn(_testCounterPrefab, spawnPosition + itemoffset, Quaternion.identity, player);

            // Keep track of the player avatars for easy access
            _spawnedCharacters.Add(player, networkPlayerObject);

            runner.SetPlayerObject(player, networkPlayerObject);
            // Share equipped spells? (i.e. full spell-graph?)
            // GameController.Instance.OnPlayerSpawned(networkPlayerObject);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
    }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }



    async void StartGame(GameMode mode)
    {
        Instance = this;
        // Create the Fusion runner and let it know that we will be providing user input
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;
        static_runner = _runner;

        var runnerSimulatePhysics3D = gameObject.AddComponent<RunnerSimulatePhysics3D>();
        runnerSimulatePhysics3D.ClientPhysicsSimulation = ClientPhysicsSimulation.SimulateAlways;

        // Create the NetworkSceneInfo from the current scene
        var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
        var sceneInfo = new NetworkSceneInfo();
        if (scene.IsValid)
        {
            sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);
        }

        // Start or join (depends on gamemode) a session with a specific name
        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = "TestRoom45",
            Scene = scene,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        //float prevDelta = Time.fixedDeltaTime;
        //Time.fixedDeltaTime = _runner.DeltaTime; ; ////////////////////////////////////////////////////////////////////////////Feels like i shouldnt chaneg this///
        //Debug.Log($"fixedDelta = {Time.fixedDeltaTime} changed from {prevDelta} and runnerDelta = {_runner.DeltaTime}");
    }

    private void OnGUI()
    {
        if (_runner == null)
        {
            if (GUI.Button(new Rect(0, 0, 200, 40), "Host"))
            {
                StartGame(GameMode.Host);
            }
            if (GUI.Button(new Rect(0, 40, 200, 40), "Join"))
            {
                StartGame(GameMode.Client);
            }
        }
        else
        {

            if (_runner.LocalPlayer.IsRealPlayer)
            {
                pings.Add(_runner.GetPlayerRtt(_runner.LocalPlayer));

                float currentTime = Time.time;

                // Check if 1 second has passed
                if (currentTime - lastPingUpdateTime >= 1.0f)
                {
                    // Calculate the average of the batch
                    double averageRTT = 0;
                    if (pings.Count > 0)
                    {
                        double sum = 0;
                        foreach (double rtt in pings)
                        {
                            sum += rtt;
                        }
                        averageRTT = sum / pings.Count;
                    }

                    displayedPing = (int)(averageRTT * 1000);
                    pings.Clear();
                    lastPingUpdateTime = currentTime;
                }

                GUI.Label(new Rect(0, 0, 200, 40), $"Ping: {displayedPing} ms");
            }

            if (_runner.IsServer && GameController.Instance != null)
            {
                if (GameController.Instance.levelGenerator != null)
                {
                    if (GUI.Button(new Rect(0, 0, 200, 40), $"Gen Level Seed: {GameController.Instance.levelGenerator.seed} "))
                    {
                        GameController.Instance.levelNetworkController.HostGenerateNewLevel(GameController.Instance.levelGenerator.seed);
                    }
                    if (GUI.Button(new Rect(0, 40, 200, 40), $"Gen Level Random Seed"))
                    {
                        GameController.Instance.levelNetworkController.HostGenerateNewLevel(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Decays a float based on how far the NetworkObject is predicting into the unconfirmed future.
    /// </summary>
    /// <summary>
    /// Decays a float based on how far the NetworkObject is predicting into the unconfirmed future.
    /// </summary>
    public float ApplyProxyDecay(NetworkObject netObj, float rawValue)
    {
        // 1. Only proxies decay!
        if (!netObj.IsProxy) return rawValue;

        var runner = netObj.Runner;

        // 2. Calculate the Delta (THE FIX: Check if Tick > 0 instead of .IsValid)
        int lastConfirmedTick = runner.LatestServerTick > 0 ? runner.LatestServerTick : runner.Tick;
        int deltaTicks = runner.Tick - lastConfirmedTick;

        // 3. Grace Period
        if (deltaTicks <= graceTicks) return rawValue;

        // 4. Dead Zone
        if (deltaTicks >= maxDecayTicks) return 0f;

        // 5. Braking Zone
        float decayProgress = (float)(deltaTicks - graceTicks) / (maxDecayTicks - graceTicks);
        float multiplier = decayCurve.Evaluate(decayProgress);

        return rawValue * multiplier;
    }

    /// <summary>
    /// Decays a Vector2 based on how far the NetworkObject is predicting into the unconfirmed future.
    /// </summary>
    public Vector2 ApplyProxyDecay(NetworkObject netObj, Vector2 rawVector)
    {
        if (!netObj.IsProxy) return rawVector;

        var runner = netObj.Runner;

        // THE FIX: Check if Tick > 0 instead of .IsValid
        int lastConfirmedTick = runner.LatestServerTick > 0 ? runner.LatestServerTick : runner.Tick;
        int deltaTicks = runner.Tick - lastConfirmedTick;

        if (deltaTicks <= graceTicks) return rawVector;
        if (deltaTicks >= maxDecayTicks) return Vector2.zero;

        float decayProgress = (float)(deltaTicks - graceTicks) / (maxDecayTicks - graceTicks);
        float multiplier = decayCurve.Evaluate(decayProgress);

        return rawVector * multiplier;
    }
}