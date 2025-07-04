using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using September.Common;
using UnityEngine;
using UnityEngine.UI;

namespace September.Lobby
{
    public class LobbyController : NetworkBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] LobbyPlayerUI _selfUIPrefab;
        [SerializeField] LobbyPlayerUI _otherUIPrefab;
        [SerializeField] Button _startButton;
        [SerializeField] Button _quitButton;
        [SerializeField] Text _roomNameText;
        [SerializeField] Transform _contentTransform;
        readonly Dictionary<PlayerRef, LobbyPlayerUI> _lobbyPlayerUIDic = new();
        
        public override void Spawned()
        {
            _roomNameText.text = Runner.SessionInfo.Name;
            Runner.AddCallbacks(this);
            PlayerDatabase.Instance.AddPlayerData(Runner.LocalPlayer);
            foreach (var pr in Runner.ActivePlayers.Reverse())
            {
                AddContents(pr);
            }
            if (Runner.IsServer)
            {
                _startButton.onClick.AddListener(() => NetworkManager.Instance.StartGame().Forget());
            }
            else
            {
                _startButton.gameObject.SetActive(false);
            }
            _quitButton.onClick.AddListener(() => NetworkManager.Instance.QuitLobby().Forget());
            PlayerDatabase.Instance.ChangedDataAction += ChangeLobbyPlayerUI;
        }
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            Runner.RemoveCallbacks(this);
            PlayerDatabase.Instance.ChangedDataAction -= ChangeLobbyPlayerUI;
        }
        
        void AddContents(PlayerRef playerRef)
        {
            if (_lobbyPlayerUIDic.ContainsKey(playerRef)) return;
            if (Runner.LocalPlayer == playerRef)
            {
                _lobbyPlayerUIDic.Add(playerRef, Instantiate(_selfUIPrefab, _contentTransform));
                _lobbyPlayerUIDic[playerRef].Dropdown.onValueChanged
                    .AddListener(num =>
                    {
                        var data = CharacterDataContainer.Instance.GetCharacterData(num);
                        PlayerDatabase.Instance.Rpc_SetCharacter(playerRef, data.Type);
                    });
            }
            else
            {
                _lobbyPlayerUIDic.Add(playerRef, Instantiate(_otherUIPrefab, _contentTransform));
            }
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (Runner.LocalPlayer == player) return;
            AddContents(player); 
        }
        
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Destroy(_lobbyPlayerUIDic[player].gameObject);
            _lobbyPlayerUIDic.Remove(player);
            if (HasStateAuthority) PlayerDatabase.Instance.PlayerDataDic.Remove(player);
        }
        
        void ChangeLobbyPlayerUI(NetworkDictionary<PlayerRef, SessionPlayerData> dictionary)
        {
            foreach (var kv in dictionary)
            {
                if (!_lobbyPlayerUIDic.TryGetValue(kv.Key, out var value)) return;
                value.NameText.text = kv.Value.DisplayNickName;
                if (value.JobText) value.JobText.text = CharacterDataContainer.Instance.GetCharacterData(kv.Value.CharacterType).DisplayName;
            }
        }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
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