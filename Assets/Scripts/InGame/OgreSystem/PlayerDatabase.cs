using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Sockets;

namespace September.OgreSystem
{
    public class PlayerDatabase : NetworkBehaviour, INetworkRunnerCallbacks
    {
        private static PlayerDatabase _instance;

        public static PlayerDatabase Instatnce
        {
            get
            {
                if (_instance == null)
                    _instance = new PlayerDatabase();
                return _instance;
            }
        }
        
        [Networked, Capacity(8)]
        public NetworkDictionary<int, PlayerData> PlayerDictionary { get;}

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
        public void Update(PlayerData playerData)
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

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
            throw new NotImplementedException();
        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
            throw new NotImplementedException();
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            PlayerData playerData = new PlayerData(player.PlayerId, "shiomi", 20, 20, false, false, player);
            Register(playerData);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            throw new NotImplementedException();
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            throw new NotImplementedException();
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            throw new NotImplementedException();
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            throw new NotImplementedException();
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            throw new NotImplementedException();
        }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
        {
            throw new NotImplementedException();
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {
            throw new NotImplementedException();
        }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
            throw new NotImplementedException();
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            throw new NotImplementedException();
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
            throw new NotImplementedException();
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            throw new NotImplementedException();
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            throw new NotImplementedException();
        }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        {
            throw new NotImplementedException();
        }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
            throw new NotImplementedException();
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            throw new NotImplementedException();
        }

        public void OnSceneLoadStart(NetworkRunner runner)
        {
            throw new NotImplementedException();
        }
    }
}

