using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace September.Common
{
    public class GameLauncher : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static GameLauncher Instance;
        [SerializeField] NetworkRunner _runnerPrefab;
        [SerializeField] PlayerDatabase _playerDatabasePrefab;
        [SerializeField, Scene] string _titleSceneName;
        [SerializeField, Scene] string _lobbySceneName;
        [SerializeField, Scene] string _gameSceneName;
        NetworkRunner _networkRunner;
        UniTask _currentTask;
        private void Start()
        {
            if (Instance == null)
            {
                _networkRunner = Instantiate(_runnerPrefab);
                _networkRunner.AddCallbacks(this);
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        public void CreateLobby(string gameName, int playerCount)
        {
            if (!_currentTask.Status.IsCompleted()) return;
            _currentTask = CreateLobbyAsync(gameName, playerCount).Preserve();
        }
        async UniTask CreateLobbyAsync(string gameName, int playerCount)
        {
            var result = await _networkRunner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Host,
                SessionName = gameName,
                PlayerCount = playerCount
            });
            if (!result.Ok)
            {
                Debug.Log(result.ShutdownReason);
                await InitializeRunner();
                return;
            }
            await _networkRunner.SpawnAsync(_playerDatabasePrefab);
            await _networkRunner.LoadScene(_lobbySceneName);
        }
        public void JoinLobby(string gameName)
        {
            if (!_currentTask.Status.IsCompleted()) return;
            _currentTask = JoinLobbyAsync(gameName).Preserve();
        }
        async UniTask JoinLobbyAsync(string gameName)
        {
            var result = await _networkRunner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = gameName
            });
            if (!result.Ok)
            {
                Debug.Log(result.ShutdownReason);
                await InitializeRunner();
            }
        }
        async UniTask InitializeRunner()
        {
            await _networkRunner.Shutdown();
            _networkRunner = Instantiate(_runnerPrefab);
            _networkRunner.AddCallbacks(this);
        }
        public async UniTaskVoid QuitLobby()
        {
            await _networkRunner.Shutdown();
            await SceneManager.LoadSceneAsync(_titleSceneName);
            _networkRunner = Instantiate(_runnerPrefab);
            _networkRunner.AddCallbacks(this);
        }

        public async UniTaskVoid StartGame()
        {
            if (!_networkRunner.IsServer) return;
            _networkRunner.SessionInfo.IsOpen = false;
            await _networkRunner.LoadScene(_gameSceneName);
            PlayerDatabase.Instance.ChooseOgre();
            var container = CharacterDataContainer.Instance;
            foreach (var pair in PlayerDatabase.Instance.PlayerDataDic)
            {
                await _networkRunner.SpawnAsync(container.GetCharacterData(pair.Value.CharacterType).Prefab,
                    inputAuthority: pair.Key);
            }
        }
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner,
            NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress,
            NetConnectFailedReason reason) { }
        void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner,
            Dictionary<string, object> data) { }
        void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key,
            ArraySegment<byte> data) { }
        void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key,
            float progress) { }
        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
    }
}