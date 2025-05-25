using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using InGame.Player.Ability;
using September.Common;
using UnityEngine;

public class GameLuncher : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] NetworkRunner networkRunnerPrefab;
    [SerializeField] NetworkPrefabRef playerAvatarPrefab;
    [SerializeField] NetworkObject abilityExecutor;
    [SerializeField] GameMode _gameMode;
    [SerializeField] AbilityTestUi _abilityTestUi;
    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
    
    GameInput _playerInput;
        
    private void Awake()
    {
        // InputSystemを有効にする
        _playerInput = new GameInput();
        _playerInput.Enable();
        _playerInput.Player.Enable();
    }
    
    // Start is called before the first frame update
    async void Start()
    {
        var networkRunner = Instantiate(networkRunnerPrefab);
        networkRunner.AddCallbacks(this);
        var result = await networkRunner.StartGame(new StartGameArgs
        {
            GameMode = _gameMode,
            SessionName = "GameSession",
            SceneManager = gameObject.GetComponent<NetworkSceneManagerDefault>(),
        });
        
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            Vector3 spawnPosition = new Vector3((player.RawEncoded % runner.Config.Simulation.PlayerCount) * 3, 1, 0);
            NetworkObject networkPlayerObject = runner.Spawn(playerAvatarPrefab, spawnPosition, Quaternion.identity, player);
            networkPlayerObject.GetComponent<PlayerAvatar>().NickName = $"Player{UnityEngine.Random.Range(0, 10000)}";
            _spawnedCharacters.Add(player, networkPlayerObject);
            
            if (player == runner.LocalPlayer)
            {
                Debug.Log($"Local player {player.PlayerId} has joined the game");
                var networkObject = runner.Spawn(abilityExecutor, Vector3.zero, Quaternion.identity, player);
                if (networkObject == null) Debug.LogError("abilityExecutorのスポーンに失敗しました");
            }
        }
        _abilityTestUi?.gameObject.SetActive(true);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var playerInput = new PlayerInput();
        var playerActions = _playerInput.Player;
        //  Input Actionからデータを取り出してネットワークに登録する
        playerInput.Buttons.Set(PlayerButtons.Jump, playerActions.Jump.IsPressed());
        playerInput.Buttons.Set(PlayerButtons.Attack, playerActions.Attack.IsPressed());
        playerInput.Buttons.Set(PlayerButtons.Ability1, playerActions.Ability1.IsPressed());
        playerInput.Buttons.Set(PlayerButtons.Ability2, playerActions.Ability2.IsPressed());
        playerInput.MoveDirection = playerActions.Move.ReadValue<Vector2>();
        playerInput.CameraYaw = Camera.main.transform.rotation.eulerAngles.y;
        input.Set(playerInput);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log("シーンのロードが完了しました");
        
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
    }
}
