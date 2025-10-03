using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Fusion.Addons.Physics;

public class BasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    public NetworkRunner _runner;
    private static NetworkRunner static_runner;

    [SerializeField] private NetworkPrefabRef _playerPrefab;
    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();

    public static NetworkObject Spawn(NetworkPrefabRef prefab, Vector3 pos, Quaternion rot, NetworkRunner.OnBeforeSpawned onBeforeSpawned = null)
    {
        // Deal with spawning objects.
        // If host, spawn the object.
        // If client, send a request to spawn the object.
        // How do we cast spells?
        // Do we just send a request to the host to start casting?
        // Or do we cast, then have the host correct it?
        if(static_runner == null || !static_runner.IsServer)
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
            Vector3 spawnPosition = new Vector3((player.RawEncoded % runner.Config.Simulation.PlayerCount) * 1, 1, 0);
            //NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);

            // Keep track of the player avatars for easy access
            //_spawnedCharacters.Add(player, networkPlayerObject);

            // Share equipped spells? (i.e. full spell-graph?)

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
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();

        if (Keyboard.current.wKey.isPressed)
            data.direction += Vector3.forward;

        if (Keyboard.current.sKey.isPressed)
            data.direction += Vector3.back;

        if (Keyboard.current.aKey.isPressed)
            data.direction += Vector3.left;

        if (Keyboard.current.dKey.isPressed)
            data.direction += Vector3.right;

        data.lookRotation = Camera.main.transform.rotation;

        input.Set(data);
    }
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
        // Create the Fusion runner and let it know that we will be providing user input
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;
        static_runner = _runner;

        var runnerSimulatePhysics3D = gameObject.AddComponent<RunnerSimulatePhysics3D>();
        runnerSimulatePhysics3D.ClientPhysicsSimulation = ClientPhysicsSimulation.SimulateForward;

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
            SessionName = "TestRoom",
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
    }
}