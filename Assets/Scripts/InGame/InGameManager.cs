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
            if (_networkRunner == Runner.IsServer)
            {
                Initialize();
            }
            if (HasStateAuthority)
            {
                RPC_SetUpUI();
            }
            ChooseOgre();
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
            HideCursor();
        }


        /// <summary>
        /// 各Playerの気絶時に呼ばれるメソッド
        /// </summary>
        private void OnPlayerKilled(HitData data)
        {
            if (!Runner.IsServer) return; // サーバー側でのみ実行可能
            
            var killerData = PlayerDatabase.Instance.PlayerDataDic.Get(data.ExecutorRef); //DataBaseから該当Playerの情報取得
            killerData.IsOgre = false;
            PlayerDatabase.Instance.PlayerDataDic.Set(data.ExecutorRef, killerData); //DataBase更新 

            var killedData = PlayerDatabase.Instance.PlayerDataDic.Get(data.TargetRef);
            killedData.IsOgre = false;
            PlayerDatabase.Instance.PlayerDataDic.Set(data.TargetRef, killedData);
            killerData.Score += _addScore;
            Debug.Log($"鬼が{data.ExecutorRef}から{data.TargetRef}に変更された");
            RPC_SetOgreUI(data.ExecutorRef,data.TargetRef);
        }

        void HideCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
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
            CurrentState = GameState.Playing;
            _tickTimer = TickTimer.CreateFromSeconds(Runner, _timerData.GameTime);
        }

        private void GameEnded()
        {
            CurrentState = GameState.GameEnded;
            GetScore();
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

        public IEnumerable<(PlayerRef, int score)> GetScore()
        {
            List<(PlayerRef player, int score)> scores = new();
            foreach (var pair in PlayerDatabase.Instance.PlayerDataDic)
            {
                scores.Add((pair.Key, pair.Value.Score));
            }

            var ordered = scores.OrderByDescending(x => x.score).ToList();
            for (int i = 0; i < ordered.Count(); i++)
            {
                Debug.Log($"{i + 1} 位は{ordered[i].player}でスコアは{ordered[i].score}点");
            }

            return ordered;
        }

        public override void FixedUpdateNetwork()
        {
            if (_tickTimer.Expired(Runner))
            {
                GameEnded();
                _tickTimer = TickTimer.None;
                _cts.Cancel();
                _cts.Dispose();
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