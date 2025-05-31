using UnityEngine;
using System.Collections.Generic;
using September.InGame.UI;
using UniRx;

namespace InGame.Player
{
    public class PlayerDataManager : MonoBehaviour
    {
        private readonly Dictionary<int, PlayerStatus> _playerData = new();

        // GameLauncherでDataを登録する必要がある
        public void RegisterPlayer(int playerId, PlayerStatus status)
        {
            if (!_playerData.TryAdd(playerId, status))
                return;

            if (!status.ISLocalPlayer)
                return;

            // Health監視
            status.CurrentHealth.DistinctUntilChanged().Subscribe(UIController.I.ChangeSliderValue).AddTo(this);
            // Stamina 監視
            status.CurrentStamina.Subscribe(UIController.I.ChangeStaminaValue).AddTo(this);
        }

        public PlayerStatus GetPlayerData(int playerId)
        {
            return _playerData.GetValueOrDefault(playerId);
        }
    }
}