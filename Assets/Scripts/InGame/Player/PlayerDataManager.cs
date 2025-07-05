using UnityEngine;
using September.InGame.UI;
using UniRx;

namespace InGame.Player
{
    public class PlayerDataManager : MonoBehaviour
    {
        private PlayerManager _playerManager;
        private PlayerStatus _playerStatus;

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
            
            // Health監視
            status.ReactiveCurrentHealth.DistinctUntilChanged().Subscribe(UIController.I.ChangeSliderValue).AddTo(this);
            // Stamina 監視
            status.ReactiveCurrentStamina.DistinctUntilChanged().Subscribe(UIController.I.ChangeStaminaValue).AddTo(this);
        }
    }
}