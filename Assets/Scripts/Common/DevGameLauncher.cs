using System;
using System.Collections.Generic;
using Fusion;
using September.Common;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Fusion.Sockets;
using NaughtyAttributes;
using UnityEngine.SceneManagement;

namespace Common
{
    /// <summary> InGameシーンから始めてもいい感じになるやつ </summary>
    public class DevGameLauncher : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] NetworkRunner _runnerPrefab;
        [SerializeField] PlayerDatabase _playerDatabasePrefab;
        [SerializeField, Scene] string _inGameSceneName;
        [SerializeField] string _roomName = "TestRoom";
        [SerializeField] int _playerCount = 1;
        [SerializeField] CharacterType _characterType;
        [SerializeField] bool _useClickToStart;
        
        NetworkRunner _runner;

        private void Awake()
        {
            if (PlayerDatabase.Instance)
            {
                enabled = false;
                return;
            }

            if (!_useClickToStart) StartGame().Forget();
        }

        private void Update()
        {
            if (_useClickToStart && !_runner && Input.GetMouseButtonDown(0))
            {
                StartGame().Forget();
            }
        }

        async UniTask StartGame()
        {
            // NetworkRunnerを作成して部屋を作成
            _runner = Instantiate(_runnerPrefab);
            _runner.AddCallbacks(this);
            _runner.ProvideInput = true;
            
            var result = await _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.AutoHostOrClient,
                SessionName = _roomName,
                PlayerCount = _playerCount,
                Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex)
            });

            if (!result.Ok)
            {
                Debug.LogError(result);
                return;
            }

            if (_runner.IsServer)
            {
                NetworkObject playerDatabaseObject = await _runner.SpawnAsync(_playerDatabasePrefab);
                PlayerDatabase playerDatabase = playerDatabaseObject.GetComponent<PlayerDatabase>();
                playerDatabase.ChangedDataAction += CheckStartInGame;
            }
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            AddPlayerData(player).Forget();
        }
        
        async UniTask AddPlayerData(PlayerRef playerRef)
        {
            if (_runner.LocalPlayer != playerRef) return;

            await UniTask.WaitUntil(() => (bool)PlayerDatabase.Instance);
            
            var playerData = new SessionPlayerData($"dev:{playerRef}", 0)
            {
                CharacterType = _characterType
            };
            
            PlayerDatabase.Instance.Rpc_SetPlayerData(playerRef, playerData);
        }

        void CheckStartInGame(NetworkDictionary<PlayerRef, SessionPlayerData> dictionary)
        {
            if (PlayerDatabase.Instance.PlayerDataDic.Count == _playerCount)
            {
                PlayerDatabase.Instance.ChangedDataAction -= CheckStartInGame;
                _runner.LoadScene(_inGameSceneName);
            }
        }

        #region INetworkRunnerCallbacks
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){}
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){}
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player){}
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason){}
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason){}
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token){}
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason){}
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message){}
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data){}
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress){}
        public void OnInput(NetworkRunner runner, NetworkInput input){}
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input){}
        public void OnConnectedToServer(NetworkRunner runner){}
        public void OnConnectedToServer(){}
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList){}
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data){}
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken){}
        public void OnSceneLoadDone(NetworkRunner runner){}
        public void OnSceneLoadStart(NetworkRunner runner){}
        #endregion
    }
}
