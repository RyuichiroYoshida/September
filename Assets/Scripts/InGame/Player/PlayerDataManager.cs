using UnityEngine;
using System.Collections.Generic;
using September.InGame.UI;
using UniRx;

namespace InGame.Player
{
    public class PlayerDataManager : MonoBehaviour
    {
        private readonly Dictionary<int, PlayerData> _playerData = new();
        
        public Subject<(int PlayerId,int health)> OnHealthChanged = new();
        public Subject<(int playerId, float stamina)> OnStaminaChanged = new();

        // GameLauncherでDataを登録する必要がある
        public void RegisterPlayer(int playerId, PlayerData data)
        {
            if(!_playerData.TryAdd(playerId, data))
                return;

            // Health監視
            data.Health.Subscribe(hp =>
            {
                if (data.IsLocalPlayer)
                    UIController.I.OnOnChangeSliderValue.Value = hp;
            }).AddTo(data);
            // Stamina 監視
            data.StaminaSubject
                .Subscribe(stamina =>
                {
                    if(data.IsLocalPlayer)
                        UIController.I.OnChangeStaminaValue.Value = stamina;
                }).AddTo(data);
        }

        public PlayerData GetPlayerData(int playerId)
        {
            return _playerData.GetValueOrDefault(playerId);
        }
    }
}