using System;
using Fusion;
using September.Common;
using UnityEngine;

namespace InGame.Player.Ult
{
    public class UltCondition : NetworkBehaviour, IUltCondition
    {
        [SerializeField] private int _requiredScore = 1000;

        /// <summary> 直近の必殺技発動時のスコア </summary>
        [Networked, OnChangedRender(nameof(OnPrevScoreChangedRender))] private int PrevScore { get; set; }

        private int _currentScore;
        /// <summary>操作主の参照を保存する変数</summary>
        private PlayerRef _ownerRef;
        /// <summary>PlayerDataBaseの参照を保持する変数</summary>
        private PlayerDatabase _subscribedDatabase;

        public int RemainingScore => Mathf.Clamp(_requiredScore - (_currentScore - PrevScore), 0, _requiredScore);
        public float Progress => Mathf.Clamp01((float)(_currentScore - PrevScore) / _requiredScore);

        public event Action OnProgressChanged;

        public bool IsAvailable()
        {
            return _currentScore - PrevScore >= _requiredScore;
        }

        public void OnUltActivated()
        {
            PrevScore = _currentScore;
        }

        /// <summary>
        /// 必殺技発動時にUIを更新する用
        /// </summary>
        private void OnPrevScoreChangedRender()
        {
            OnProgressChanged?.Invoke();
        }

        private void Start()
        {
            _ownerRef = Object.InputAuthority;
            _subscribedDatabase = PlayerDatabase.Instance;

            if (_subscribedDatabase == null)
            {
                Debug.LogError("[UltCondition] PlayerDatabase is not found");
                return;
            }

            // スコアの変動を監視
            _subscribedDatabase.ChangedDataAction += OnPlayerDataChanged;
            OnPlayerDataChanged(_subscribedDatabase.PlayerDataDic);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            UnsubscribePlayerData();
        }

        private void OnDestroy()
        {
            // NetworkObject以外の理由で破棄された場合の保険。
            UnsubscribePlayerData();
        }

        /// <summary>
        /// プレイヤー情報が変更されたときに呼ばれるメソッド
        /// </summary>
        /// <param name="dict">プレイヤーの情報を持つ辞書</param>
        private void OnPlayerDataChanged(NetworkDictionary<PlayerRef, SessionPlayerData> dict)
        {
            if (!dict.TryGet(_ownerRef, out var playerData))
            {
                Debug.LogError($"[UltCondition] PlayerData is not found: {_ownerRef}");
                return;
            }

            _currentScore = playerData.Score;
            OnProgressChanged?.Invoke();
        }

        /// <summary>
        /// イベントを解除するメソッド
        /// </summary>
        private void UnsubscribePlayerData()
        {
            if (_subscribedDatabase == null)
                return;

            _subscribedDatabase.ChangedDataAction -= OnPlayerDataChanged;
            _subscribedDatabase = null;
        }
    }
}
