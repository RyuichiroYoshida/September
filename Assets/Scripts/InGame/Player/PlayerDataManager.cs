using UnityEngine;
using System.Collections.Generic;
using September.InGame.UI;
using UniRx;

namespace InGame.Player
{
    public class PlayerDataManager : MonoBehaviour
    {
        private readonly Dictionary<int, PlayerData> _playerData = new();

        // GameLauncherでDataを登録する必要がある
        public void RegisterPlayer(int playerId, PlayerData data)
        {
            if (!_playerData.TryAdd(playerId, data))
                return;

            if (!data.ISLocalPlayer)
                return;

            // Health監視
            data.CurrentHealth.DistinctUntilChanged().Subscribe(UIController.I.ChangeSliderValue).AddTo(this);
            // Stamina 監視
            data.CurrentStamina.Subscribe(UIController.I.ChangeStaminaValue).AddTo(this);
        }

        public PlayerData GetPlayerData(int playerId)
        {
            return _playerData.GetValueOrDefault(playerId);
        }
    }
}