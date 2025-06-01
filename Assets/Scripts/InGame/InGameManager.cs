using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using InGame.Health;
using InGame.Player;
using September.Common;
using September.InGame.UI;
using UnityEngine;


namespace September.InGame.Common
{
    public class InGameManager : NetworkBehaviour, IRegisterableService
    {
        [Networked] private TickTimer _tickTimer { get; set; }

        [Header("ゲーム時間（秒）")] [SerializeField] private int _gameTime = 1200;

        public Action OnOgreChanged;

        private NetworkRunner _networkRunner;
        
        public NetworkDictionary<PlayerRef,NetworkObject> PlayerDataDic => default;

        public override void Spawned()
        {
            _networkRunner = FindFirstObjectByType<NetworkRunner>();
            if (_networkRunner == null)
            {
                Debug.LogError("NetworkRunnerがありません");
            }
            if (!_networkRunner.IsServer) return;
            UIController.I.SetUpStartUI();
            Initialize();
        }

        public async void Initialize()
        {
            if(!HasInputAuthority) return;
            PlayerDatabase.Instance.ChooseOgre();
            var container = CharacterDataContainer.Instance;
            foreach (var pair in PlayerDatabase.Instance.PlayerDataDic)
            {
                 var player = await _networkRunner.SpawnAsync(
                     container.GetCharacterData(pair.Value.CharacterType).Prefab,
                     inputAuthority: pair.Key);
                 if (!PlayerDataDic.ContainsKey(pair.Key))
                 {
                     PlayerDataDic.Add(pair.Key, player);
                 }
                 var playerHealth = player.GetComponent<PlayerHealth>();
                playerHealth.OnDeath += RPC_OnPlayerKilled;
                //PlayerHealthのOnDeathに登録
            }
            StartTimer();
            HideCursor();
        }


        /// <summary>
        /// 各Playerの気絶時に呼ばれるメソッド
        /// </summary>
        public void RPC_OnPlayerKilled(HitData data)
        {
            if (!Runner.IsServer) return; // サーバー側でのみ実行可能
            
            var killerData = PlayerDatabase.Instance.PlayerDataDic.Get(data.ExecutorRef); //DataBaseから該当Playerの情報取得
            killerData.IsOgre = false;
            PlayerDatabase.Instance.PlayerDataDic.Set(data.ExecutorRef, killerData); //DataBase更新 

            var killedData = PlayerDatabase.Instance.PlayerDataDic.Get(data.TargetRef);
            killedData.IsOgre = false;
            PlayerDatabase.Instance.PlayerDataDic.Set(data.TargetRef, killedData);
            
            OnOgreChanged?.Invoke();
        }

        void HideCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void StartTimer()
        {
            _tickTimer = TickTimer.CreateFromSeconds(Runner, _gameTime);
            UIController.I.StartTimer();
        }

        private void GameEnded()
        {
            GetScore();
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