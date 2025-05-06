using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using Random = UnityEngine.Random;

namespace September.Common
{
    public class GameLauncher : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static GameLauncher Instance;
        [SerializeField] NetworkPrefabRef[] _playerPrefab;
        [SerializeField] NetworkRunner _runnerPrefab;
        [SerializeField] string _inGameName;
        [SerializeField] private GameObject _airPlane;
        NetworkRunner _networkRunner;
        public Action<NetworkObject, PlayerRef> OnPlayerSpawned;
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

        public async UniTaskVoid CreateGame(string gameName, int playerCount)
        {
            await _networkRunner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Host,
                SessionName = gameName,
                PlayerCount = playerCount,
            });
            _networkRunner.LoadScene(_inGameName);
        }

        public void JoinGame(string gameName)
        {
            _networkRunner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = gameName
            });
        }

        /// <summary>
        /// プレイヤーが参加した際に呼ばれる
        /// </summary>
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            //  通知が来たのがホストのランナーなら
            if (runner.IsServer)
            {
                var rand = Random.insideUnitCircle * 5f;
                var spawnPosition = new Vector3(rand.x, 2f, rand.y);

                #region 仮
                NetworkPrefabRef selectedPrefab;

                switch (player.PlayerId)
                {
                    case 1:
                        selectedPrefab = _playerPrefab[1];
                        break;
                    case 2:
                        selectedPrefab = _playerPrefab[2];
                        break;
                    default:
                        selectedPrefab = _playerPrefab[0];
                        break;
                }

                #endregion
                
                var avatar = runner.Spawn(selectedPrefab, spawnPosition, Quaternion.identity, inputAuthority: player, onBeforeSpawned:
                    (targetRunner, targetObj) => OnPlayerSpawned?.Invoke(targetObj, player));
                runner.SetPlayerObject(player, avatar);

                if (player.PlayerId == 2)
                {
                    runner.Spawn(_airPlane,_airPlane.transform.position,Quaternion.identity,inputAuthority: player, onBeforeSpawned:
                        (targetRunner, targetObj) =>
                        {
                            Debug.Log($" {player} に飛行機を割り当て");
                        });
                }
            }
        }
        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            // 退出したプレイヤーのアバターを破棄する
            if (runner.TryGetPlayerObject(player, out var avatar)) 
            {
                runner.Despawn(avatar);
            }
        }
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