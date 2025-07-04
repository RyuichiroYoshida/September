using System;
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
        
        public int MaxHealth { get; private set; }
        public float MaxStamina { get; private set; }
        public int AttackDamage { get; private set; }
        public int AttackDamageOgre { get; private set; }
        
        public bool ISLocalPlayer => _playerManager && _playerManager.IsLocalPlayer;
        
        
        #region Events

        private readonly ReactiveProperty<int> _currentHealth = new(0);
        private readonly ReactiveProperty<float> _currentStamina = new(0);
        public ReadOnlyReactiveProperty<int> CurrentHealth => _currentHealth.ToReadOnlyReactiveProperty();
        public ReadOnlyReactiveProperty<float> CurrentStamina => _currentStamina.ToReadOnlyReactiveProperty();
        
        #endregion

        private void Start()
        {
            _playerManager = GetComponent<PlayerManager>();
            AttackDamage = _playerManager.PlayerParameter.AttackDamage;
            
            var health = GetComponent<PlayerHealth>();
            MaxHealth = _playerManager.PlayerParameter.Health;
            health.OnHealthChanged.Subscribe(hp => _currentHealth.Value = hp).AddTo(this);
            
            var movement = GetComponent<PlayerMovement>();
            MaxStamina = _playerManager.PlayerParameter.Stamina;
            movement.OnStaminaChanged.Subscribe(stamina => _currentStamina.Value = stamina).AddTo(this);
        }
    }
}