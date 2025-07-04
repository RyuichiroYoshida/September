using UnityEngine;
using September.InGame.UI;
using UniRx;

namespace InGame.Player
{
    public class PlayerDataManager : MonoBehaviour
    {
        private PlayerStatus _playerStatus;

        private void Start()
        {
            Initialize();
            RegisterPlayer(_playerStatus);
        }

        private void Initialize()
        {
            _playerStatus = GetComponent<PlayerStatus>();
        }

        // GameLauncherでDataを登録する必要がある
        private void RegisterPlayer(PlayerStatus status)
        {
            if (!status.ISLocalPlayer)
                return;
            
            // Health監視
            status.CurrentHealth.DistinctUntilChanged().Subscribe(UIController.I.ChangeSliderValue).AddTo(this);
            // Stamina 監視
            status.CurrentStamina.DistinctUntilChanged().Subscribe(UIController.I.ChangeStaminaValue).AddTo(this);
        }
    }
}