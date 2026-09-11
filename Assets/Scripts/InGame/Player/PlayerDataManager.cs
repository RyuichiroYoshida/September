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

		private void Start()
		{
			Initialize();
			RegisterPlayer(_playerStatus);
		}

		private void Initialize()
		{
			_playerManager = GetComponent<PlayerManager>();
			_playerStatus = GetComponent<PlayerStatus>();
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
