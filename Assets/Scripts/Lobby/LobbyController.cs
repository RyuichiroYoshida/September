using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using September.Common;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace September.Lobby
{
    public class LobbyController : NetworkBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private PlayerConditionView _playerConditionViewPrefab;
        [SerializeField] private BotCharacterSelect _botCharacterSelect;
        [SerializeField] private Button _readyButton;
        [SerializeField] private Button _quitButton;
        [SerializeField] private Button _addBotButton;
        [SerializeField] private TextMeshProUGUI _playerNameText;
        [SerializeField] private TextMeshProUGUI _roomNameText;
        [SerializeField] private Image _fadePanel;
        [SerializeField] private Transform _contentTransform;

        readonly Dictionary<PlayerRef, PlayerConditionView> _lobbyPlayerUIDic = new();
        [Networked, OnChangedRender(nameof(OnChangedIsReady)), Capacity(8), HideInInspector]
        public NetworkDictionary<PlayerRef, NetworkBool> PlayerIsReadyDic => default;

        [Networked, HideInInspector]
        public MapType SelectedMapType { get; private set; } = MapType.Pirate;

        PlayerDatabase _playerDatabase;

        public override async void Spawned()
        {
            if (HasStateAuthority)
            {
                SelectedMapType = NetworkManager.Instance.SelectedMapType;
            }

            _roomNameText.text = Runner.SessionInfo.Name;
            Runner.AddCallbacks(this);

            _playerDatabase = await WaitForPlayerDatabaseAsync();
            if (_playerDatabase == null) return;

            foreach (var kv in _playerDatabase.PlayerDataDic)
            {
                AddContents(kv.Key);
            }
            AddContents(Runner.LocalPlayer);
            _playerDatabase.AddPlayerData(Runner.LocalPlayer);
            _readyButton.onClick.AddListener(() => Rpc_ToggleReady(Runner.LocalPlayer));
            _quitButton.onClick.AddListener(() => NetworkManager.Instance.QuitLobby().Forget());

            if (HasStateAuthority)
            {
                _addBotButton.onClick.AddListener(() => _playerDatabase.AddBotData());
            }
            _addBotButton.gameObject.SetActive(HasStateAuthority);

            _playerDatabase.ChangedDataAction += ChangeLobbyPlayerUI;
            OnChangedIsReady();

            _playerDatabase.OnBotJoin.Subscribe(x => OnBotJoined(x)).AddTo(this);
            _playerDatabase.OnBotLeft.Subscribe(x => OnBotLeft(x)).AddTo(this);
        }

        async UniTask<PlayerDatabase> WaitForPlayerDatabaseAsync()
        {
            await UniTask.WaitUntil(() =>
                PlayerDatabase.Instance != null
                && PlayerDatabase.Instance.Runner == Runner
                && PlayerDatabase.Instance.Object != null
                && PlayerDatabase.Instance.Object.IsValid,
                cancellationToken: this.GetCancellationTokenOnDestroy());

            return PlayerDatabase.Instance;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            Runner.RemoveCallbacks(this);
            if (_playerDatabase != null)
                _playerDatabase.ChangedDataAction -= ChangeLobbyPlayerUI;
        }

        private void OnChangedIsReady()
        {
            int isReadyCount = 0;
            foreach (var kv in PlayerIsReadyDic)
            {
                if (kv.Value) isReadyCount++;
                if (!_lobbyPlayerUIDic.TryGetValue(kv.Key, out var value)) return;
                value.IsReadyImage.enabled = kv.Value;
            }
            //  全員準備完了ならゲームを開始する
            if (_playerDatabase == null || !_playerDatabase.Object.IsValid) return;

            if (isReadyCount == _playerDatabase.PlayerDataDic.Count && HasStateAuthority)
            {
                DelayStartGame(0.5f).Forget();
                RPC_Fade();
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
        private void Rpc_ToggleReady(PlayerRef playerRef)
        {
            PlayerIsReadyDic.Set(playerRef, !PlayerIsReadyDic.Get(playerRef));
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Fade()
        {
            NetworkManager.Instance.Fade(_fadePanel).Forget();
        }

        bool _isStartingGame = false;
        private async UniTaskVoid DelayStartGame(float delay)
        {
            //Debug.Log("DelayStartGame");
            if (_isStartingGame) return;
            _isStartingGame = true;
            await UniTask.WaitForSeconds(delay);
            NetworkManager.Instance.StartGame(new GameStartContext(SelectedMapType)).Forget();
        }

        void AddContents(PlayerRef playerRef)
        {
            if (_lobbyPlayerUIDic.ContainsKey(playerRef)) return;

            _lobbyPlayerUIDic.Add(playerRef, Instantiate(_playerConditionViewPrefab, _contentTransform));

        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (HasStateAuthority)
            {
                PlayerIsReadyDic.Add(player, false);
            }
            if (Runner.LocalPlayer == player) return;
            AddContents(player);
        }
        public void OnBotJoined(PlayerRef bot)
        {
            Debug.Log(bot.AsIndex);
            if (bot.AsIndex < PlayerDatabase.BotStartIndex) return;

            if (HasStateAuthority)
            {
                PlayerIsReadyDic.Add(bot, true);
            }
            AddContents(bot);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (_lobbyPlayerUIDic.ContainsKey(player))
            {
                Destroy(_lobbyPlayerUIDic[player].gameObject);
            }
            _lobbyPlayerUIDic.Remove(player);
            if (HasStateAuthority)
            {
                if (_playerDatabase != null && _playerDatabase.Object.IsValid)
                    _playerDatabase.PlayerDataDic.Remove(player);
                PlayerIsReadyDic.Remove(player);
            }
        }

        public void OnBotLeft(PlayerRef bot)
        {
            OnPlayerLeft(null, bot);
        }

        void ChangeLobbyPlayerUI(NetworkDictionary<PlayerRef, SessionPlayerData> dictionary)
        {
            if (dictionary.ContainsKey(Runner.LocalPlayer)) _playerNameText.text = dictionary[Runner.LocalPlayer].DisplayNickName;
            foreach (var kv in dictionary)
            {
                PlayerRef playerRef = kv.Key;
                if (!_lobbyPlayerUIDic.TryGetValue(playerRef, out var value)) return;
                value.PlayerNameText.text = kv.Value.DisplayNickName;
                value.CharacterIconImage.sprite = CharacterDataContainer.Instance.GetCharacterData(kv.Value.CharacterType).CharacterIcon;

                bool isBot = playerRef.AsIndex >= PlayerDatabase.BotStartIndex;
                bool HasBotAuthority = isBot && HasStateAuthority;
                value.CharacterChangeButton.gameObject.SetActive(HasBotAuthority);
                value.BotRemoveButton.gameObject.SetActive(HasBotAuthority);

                if (HasBotAuthority)
                {
                    RectTransform rect = value.CharacterChangeButton.GetComponent<RectTransform>();
                    value.CharacterChangeButton.onClick.AddListener(() => { _botCharacterSelect.ShowPanel(playerRef, value.BotRemoveButton.transform.position); });
                    value.BotRemoveButton.onClick.AddListener(() => _playerDatabase.RemoveBotData(playerRef));
                }
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
