using September.InGame.Common.Stats;
using UnityEngine;
using September.InGame.UI;

namespace InGame.Player
{
	public class PlayerDataManager : MonoBehaviour
	{
		private PlayerManager _playerManager;
		private PlayerStatus _playerStatus;
        private PlayerHealth _health;
        private PlayerMovement _movement;
        private UIController _evasionUI;
        private int _previousEvasionStamina;
        private float _previousEvasionProgress;
        private bool _evasionBound;

		private void Start()
		{
			Initialize();
			RegisterPlayer(_playerStatus);
		}

		private void Initialize()
		{
			_playerManager = GetComponent<PlayerManager>();
			_playerStatus = GetComponent<PlayerStatus>();
            _movement = GetComponent<PlayerMovement>();
		}

		// GameLauncherでDataを登録する必要がある
		private void RegisterPlayer(PlayerStatus status)
		{
			if (!_playerManager.IsLocalPlayer)
				return;

            if (UIController.I)
            {
                // Health監視。最大HPはPlayerStatus側で扱い、UIには割合だけ渡す。
                void PublishHealth(float currentHealth)
                {
                    float maxHealth = status.MaxHealth;
                    float healthRatio = maxHealth > 0f
                        ? currentHealth / maxHealth
                        : 0f;

                    UIController.I.ChangeHealthRatio(healthRatio);
                }
                status.SubscribeStatOnChanged(StatType.Health, PublishHealth);
                PublishHealth(status.CurrentHealth);
                _health = GetComponent<PlayerHealth>();
                if (_health != null) _health.OnDeathVisual += OnDeath;
                // Stamina 監視
                status.SubscribeStatOnChanged(StatType.Stamina, x => UIController.I.ChangeStaminaValue(x));
            }
        }

        // UI向けの通知は描画フレームで行い、回避処理から切り離す。
        private void LateUpdate()
        {
            if (!_playerManager || !_playerManager.IsLocalPlayer ||
                !_playerStatus || !_playerStatus.Object || !_playerStatus.Object.IsValid ||
                !_movement || !_movement.Object || !_movement.Object.IsValid || !UIController.I)
                return;

            int count = _playerStatus.CurrentEvasionStamina;
            float progress = _movement.EvasionStaminaProgress;

            if (!_evasionBound || _evasionUI != UIController.I)
            {
                _evasionUI = UIController.I;
                _previousEvasionStamina = count;
                _previousEvasionProgress = progress;
                _evasionBound = true;
                _evasionUI.ShowEvasionStaminaProgress(progress);
                return;
            }

            // 回復による増加と、消費による減少を通知する。
            for (int recovered = _previousEvasionStamina + 1; recovered <= count; recovered++)
                _evasionUI.ShowEvasionStamina(recovered);
            if (count < _previousEvasionStamina)
                _evasionUI.ShowEvasionStamina(count);
            _previousEvasionStamina = count;

            if (!Mathf.Approximately(progress, _previousEvasionProgress))
            {
                _previousEvasionProgress = progress;
                _evasionUI.ShowEvasionStaminaProgress(progress);
            }
        }

        private void OnDeath(global::InGame.Health.HitData hitData)
        {
            if (!UIController.I) return;
            UIController.I.ChangeHealthRatio(0f);
            // 実HPはDeathで即回復するため、UIだけ空になる演出を完走させる。
            UIController.I.ChangeHealthRatio(1f);
        }

        private void OnDestroy()
        {
            if (_health != null) _health.OnDeathVisual -= OnDeath;
        }
    }
}
