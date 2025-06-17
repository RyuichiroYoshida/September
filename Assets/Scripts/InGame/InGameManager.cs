using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Health;
using InGame.Player;
using NaughtyAttributes;
using September.Common;
using September.InGame.UI;
using UnityEngine;
using Random = UnityEngine.Random;


namespace September.InGame.Common
{
    public class InGameManager : NetworkBehaviour, IRegisterableService
    {
        [Networked] private TickTimer _tickTimer { get; set; }

        [Header("Timer Settings")]
        [SerializeField,Label("TimerData")]private GameTimerData _timerData;
        
        private NetworkRunner _networkRunner;
        
        private readonly Dictionary<PlayerRef, NetworkObject> _playerDataDic = new();
        public IReadOnlyDictionary<PlayerRef, NetworkObject> PlayerDataDic => _playerDataDic;
        
        [Header("他Playerを気絶させたときに得られるスコア")] [SerializeField]
        private int _addScore;
        
        private UIController _uiController;
        public GameState CurrentState { get; private set; }

        private CancellationTokenSource _cts;
        public override void Spawned()
        {
            _cts = new CancellationTokenSource();
            _networkRunner = FindFirstObjectByType<NetworkRunner>();
            if (_networkRunner == null)
            {
                Debug.LogError("NetworkRunnerがありません");
            }
            _uiController = UIController.I;
            if (_networkRunner.IsServer)
            {
                Initialize();
            }
            if (HasStateAuthority)
            {
                RPC_SetUpUI();
            }
            HideCursor();
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SetUpUI()
        {
            Debug.Log("SetUpUI");
            _uiController.SetUpStartUI();
            _uiController.StartTimer();
        }

        private async UniTask Initialize()
        {
            var container = CharacterDataContainer.Instance;
            foreach (var pair in PlayerDatabase.Instance.PlayerDataDic)
            {
                 var player = await _networkRunner.SpawnAsync(
                     container.GetCharacterData(pair.Value.CharacterType).Prefab,
                     inputAuthority: pair.Key);
                 _networkRunner.SetPlayerObject(pair.Key, player);
                 Debug.Log($"Player {pair.Key} spawned");
                 if (!PlayerDataDic.ContainsKey(pair.Key))
                 {
                     _playerDataDic.Add(pair.Key, player);
                 }
                 var playerHealth = player.GetComponent<PlayerHealth>();
                playerHealth.OnDeath += OnPlayerKilled;
                //PlayerHealthのOnDeathに登録
            }
            Register(StaticServiceLocator.Instance);
            StartTimer();
        }


        /// <summary>
        /// 各Playerの気絶時に呼ばれるメソッド
        /// </summary>
        private void OnPlayerKilled(HitData data)
        {
            if (!HasStateAuthority) return;
            var killerData = PlayerDatabase.Instance.PlayerDataDic.Get(data.ExecutorRef); //DataBaseから該当Playerの情報取得
            var killedData = PlayerDatabase.Instance.PlayerDataDic.Get(data.TargetRef);
            if (killerData.IsOgre)
            {
                killerData.IsOgre = false;
                killedData.IsOgre = true;
                RPC_SetOgreUI(data.ExecutorRef,data.TargetRef);
                Debug.Log($"鬼が{data.ExecutorRef}から{data.TargetRef}に変更された");
            }
            killerData.Score += _addScore;
            PlayerDatabase.Instance.PlayerDataDic.Set(data.ExecutorRef, killerData); //DataBase更新 
            PlayerDatabase.Instance.PlayerDataDic.Set(data.TargetRef, killedData);
            var pData = PlayerDatabase.Instance.PlayerDataDic;
            foreach (var pair in pData)
            {
                Debug.Log(pair.Value.DisplayNickName + "が"+pair.Value.Score+"点");
            }
        }

        void HideCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        void ShowCursor()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private async UniTask StartTimer()
        {
            CurrentState = GameState.Preparation;
            for (int i = _timerData.PreStartTime; i >= 1; i--)
            {
                //ReadyTime表示
                await UniTask.Delay(TimeSpan.FromSeconds(_timerData.Duration), cancellationToken: _cts.Token);
            }
            CurrentState = GameState.Waiting;
            await UniTask.Delay(TimeSpan.FromSeconds(_timerData.AfterReadyDelay), cancellationToken: _cts.Token);
            //Start!
            ChooseOgre();
            CurrentState = GameState.Playing;
            _tickTimer = TickTimer.CreateFromSeconds(Runner, _timerData.GameTime);
        }

        private async UniTaskVoid GameEnded()
        {
            CurrentState = GameState.GameEnded;
            GetScore();
            await UniTask.Delay(TimeSpan.FromSeconds(_timerData.EndGameDelay), cancellationToken: _cts.Token);
            _cts.Cancel();
            _cts.Dispose();
            RPC_ShowCursor();
            await NetworkManager.Instance.QuitInGame();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ShowCursor()
        {
            ShowCursor();
        }
        // 鬼変更時のUI更新通知
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SetOgreUI(PlayerRef executor, PlayerRef targetRef)
        {
            _uiController.ShowNoticeKillLog($"鬼が{executor}から{targetRef}に変更された");
            
            if (executor == Runner.LocalPlayer)
                _uiController.ShowOgreLamp(false);
            else if(targetRef == Runner.LocalPlayer)
                _uiController.ShowOgreLamp(true);
        }

        public void GetScore()
        {
            List<(string playerName,int score,bool isOgre)> data = new List<(string,int,bool)>();
            foreach (var pair in PlayerDatabase.Instance.PlayerDataDic)
            {
                data.Add((pair.Value.DisplayNickName, pair.Value.Score,pair.Value.IsOgre));
            }
            var ordered = data.OrderBy(x => x.isOgre ? 1 : 0)
                                                      .OrderByDescending(x => x.score)
                                                      .ToList();
            var names = ordered.Select(x => x.playerName).ToArray();
            var scores = ordered.Select(x => x.score).ToArray();
            RPC_SetRankingData(names,scores);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SetRankingData(string[] names, int[] scores)
        {
            Debug.Log("SetRankingData");
            RankingDataHolder.Instance.SetData(names, scores);
        }
        
        public override void FixedUpdateNetwork()
        {
            if (_tickTimer.Expired(Runner))
            {
                _tickTimer = TickTimer.None;
                GameEnded();
            }
        }
        
           
        /// <summary>
        /// 鬼を抽選するメソッド
        /// </summary>
        public void ChooseOgre()
        {
            var dic = PlayerDatabase.Instance.PlayerDataDic;
            if (dic.Count <= 0 || !Runner.IsServer) return;
            
            var index = Random.Range(0, dic.Count);
            var ogreKey = dic.ToArray()[index].Key;
            var data = dic.Get(ogreKey);
            data.IsOgre = true;
            dic.Set(ogreKey, data);
            RPC_SetOgreLamp(ogreKey);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        void RPC_SetOgreLamp(PlayerRef ogreRef)
        {
            if(ogreRef == Runner.LocalPlayer)
                _uiController.ShowOgreLamp(true);
        }

        public void Register(ServiceLocator locator)
        {
            locator.Register<InGameManager>(this);
        }

        public enum GameState
        {
            Preparation,Waiting,Playing,GameEnded
        }
    }
}