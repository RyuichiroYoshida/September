using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using September.InGame;
using September.Common;
using UnityEngine;

namespace September.OgreSystem
{
    public class PlayerDatabase : NetworkBehaviour, INetworkRunnerCallbacks
    {
        public static PlayerDatabase Instance{ get; private set; }

        public override void Spawned()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Runner.AddCallbacks(this);
                GameLauncher.Instance.OnPlayerSpawned += OnPlayerSpawned;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        [Networked, Capacity(8)] 
        private NetworkDictionary<int, PlayerData> PlayerDictionary => default;

        /// <summary>
        /// プレイヤーデータを登録する
        /// </summary>
        /// <param name="data">プレイヤーデータ</param>
        public void Register(PlayerData data)
        {
            PlayerDictionary.Add(data.ID, data);
        }

        /// <summary>
        /// プレイヤーデータを取得
        /// </summary>
        /// <param name="id">ID（キー）</param>
        /// <param name="data">データ</param>
        /// <returns></returns>
        public bool TryGetPlayerData(int id, out PlayerData data)
        {
            return PlayerDictionary.TryGet(id, out data);
        }

        
        /// <summary>
        /// プレイヤーデータの更新
        /// </summary>
        /// <param name="playerData"></param>
        public void UpdateDatabase(PlayerData playerData)
        {
            PlayerDictionary.Set(playerData.ID, playerData);
        }

        /// <summary>
        /// すべてのデーターを取得
        /// </summary>
        /// <returns></returns>
        public List<PlayerData> GetAll()
        {
            List<PlayerData> playerData = new List<PlayerData>();
            foreach (var player in PlayerDictionary)
            {
                playerData.Add(player.Value);
            }
            return playerData;
        }

        public void OnPlayerSpawned(NetworkObject networkObject, PlayerRef player)
        {
            
        }

        [Rpc]
        public static void Rpc_OnPlayerSpawned(NetworkRunner networkRunner, NetworkObject networkObject, PlayerRef player)
        {
            if (Instance)
            {
                var playerAvatar = networkObject.GetComponent<PlayerAvatar>();
            
                PlayerData playerData = new PlayerData(player.PlayerId, playerAvatar.NickName, 20, 20, false, false, player);
                Instance.Register(playerData);
            }
        }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
            
        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
            
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            
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
            
        }

        public void OnSceneLoadStart(NetworkRunner runner)
        {
            
        }
    }
}

