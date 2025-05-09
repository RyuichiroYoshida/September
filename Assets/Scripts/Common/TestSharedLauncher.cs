using System;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using Random = UnityEngine.Random;

namespace September.Common
{
    public class TestSharedLauncher : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] NetworkRunner _networkRunner;
        [SerializeField] NetworkPrefabRef _playerAvatarPrefab;

        private async void Start()
        {
            // NetworkRunnerを作成して部屋を作成
            var networkRunner = Instantiate(_networkRunner);
            networkRunner.AddCallbacks(this);
            
            var result = await networkRunner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Shared
            });
            
            Debug.Log(result);
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            // 参加したPlayerが自分ではなかったらreturn
            if (player != runner.LocalPlayer) return;
            
            var random = Random.insideUnitCircle * 5f;
            var spawnPosition = new Vector3(random.x, 2f, random.y);
            
            // 自身のAvatarのスポーン
            runner.Spawn(_playerAvatarPrefab, spawnPosition, Quaternion.identity);
        }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {}
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {}
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) {}
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {}
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {}
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) {}
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {}
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) {}
        public void OnInput(NetworkRunner runner, NetworkInput input) {}
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {}
        public void OnConnectedToServer(NetworkRunner runner) {}
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) {}
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) {}
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {}
        public void OnSceneLoadDone(NetworkRunner runner) {}
        public void OnSceneLoadStart(NetworkRunner runner) {}
    }
}
