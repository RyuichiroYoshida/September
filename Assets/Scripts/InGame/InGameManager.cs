using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Health;
using InGame.Player;
using NaughtyAttributes;
using September.Common;
using September.InGame.UI;
using UnityEngine;


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

        public override void Spawned()
        {
            if(HasStateAuthority) 
                RPC_SetUpUI();
        }

        private void Start()
        {
            _uiController = UIController.I;
            _networkRunner = FindFirstObjectByType<NetworkRunner>();
            if (_networkRunner == null)
            {
                Debug.LogError("NetworkRunnerがありません");
            }
            if (!_networkRunner.IsServer) return;
            Initialize().Forget();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SetUpUI()
        {
            _uiController.SetUpStartUI();
            _uiController.StartTimer();
        }

        private async UniTask Initialize()
        {
            PlayerDatabase.Instance.ChooseOgre();
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
                playerHealth.OnDeath += RPC_OnPlayerKilled;
                //PlayerHealthのOnDeathに登録
            }
            Register(StaticServiceLocator.Instance);
            StartTimer();
            HideCursor();
        }


        /// <summary>
        /// 各Playerの気絶時に呼ばれるメソッド
        /// </summary>
        private void RPC_OnPlayerKilled(HitData data)
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
            SetOgreUI(data);
        }

        void HideCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void StartTimer()
        {
            _tickTimer = TickTimer.CreateFromSeconds(Runner, _timerData.GameTime);
        }

        private void GameEnded()
        {
            GetScore();
        }

        // 鬼変更時のUI更新通知
        private void SetOgreUI(HitData data)
        {
            _uiController.ShowNoticeKillLog($"鬼が{data.ExecutorRef}から{data.TargetRef}に変更された");
            
            if (data.ExecutorRef == Runner.LocalPlayer)
                _uiController.ShowOgreLamp(false);
            else if(data.TargetRef == Runner.LocalPlayer)
                _uiController.ShowOgreLamp(true);
        }

        public IEnumerable<(PlayerRef, int score)> GetScore()
        {
            List<(PlayerRef player, int score)> _scores = new List<(PlayerRef, int)>();
            foreach (var pair in PlayerDatabase.Instance.PlayerDataDic)
            {
                _scores.Add((pair.Key, pair.Value.Score));
            }

            var ordered = _scores.OrderByDescending(x => x.score).ToList();
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
            }
        }

        public void Register(ServiceLocator locator)
        {
            locator.Register<InGameManager>(this);
        }
    }
}