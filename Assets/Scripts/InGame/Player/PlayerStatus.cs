using UniRx;
using UnityEngine;

namespace InGame.Player
{
    // いろんなPlayerのデータの中継
    [RequireComponent(typeof(PlayerManager))]
    [RequireComponent(typeof(PlayerHealth))]
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerStatus : MonoBehaviour
    {
        PlayerManager _playerManager;
        PlayerHealth _health;
        PlayerMovement _movement;
        public int MaxHealth { get; private set; }
        
        public float MaxStamina { get; private set; }
        public int AttackDamage { get; private set; }
        public int AttackDamageOgre { get; private set; }
        
        public bool ISLocalPlayer => _playerManager && _playerManager.IsLocalPlayer;
        
        
        #region Events 
        
        public ReactiveProperty<int> CurrentHealth { get; private set; } = new();
        public ReactiveProperty<int> CurrentStamina { get; private set; } = new();
        
        #endregion

        public void Initialize(PlayerManager playerManager, PlayerHealth health, PlayerMovement movement)
        {
            _playerManager = playerManager;
            MaxHealth = health.MaxHealth;
            MaxStamina = movement.MaxStamina;
        }
    }
}