using Fusion;
using UniRx;
using UnityEngine;

namespace InGame.Player
{
    public class PlayerStatus : NetworkBehaviour
    {
        [SerializeField] PlayerParameter _param;
        
        public int MaxHealth { get; private set; }
        [Networked, HideInInspector, OnChangedRender(nameof(OnChangeHealth))] public int CurrentHealth { get; set; }
        private void OnChangeHealth() => _currentHealth.Value = CurrentHealth;
        public float MaxStamina { get; private set; }
        public float MaxSpeedRate { get; set; } = 1;
        [Networked, HideInInspector, OnChangedRender(nameof(OnChangeStamina))] public float CurrentStamina { get; set; }
        private void OnChangeStamina() => _currentStamina.Value = CurrentStamina;
        public float StaminaRegen { get; private set; }
        public int AttackDamage { get; set; }
        
        #region Events

        private readonly ReactiveProperty<int> _currentHealth = new(0);
        private readonly ReactiveProperty<float> _currentStamina = new(0);
        public ReadOnlyReactiveProperty<int> ReactiveCurrentHealth => _currentHealth.ToReadOnlyReactiveProperty();
        public ReadOnlyReactiveProperty<float> ReactiveCurrentStamina => _currentStamina.ToReadOnlyReactiveProperty();
        
        #endregion

        public override void Spawned()
        {
            InitStatus();
        }

        void InitStatus()
        {
            MaxHealth = _param.Health;
            CurrentHealth = MaxHealth;
            MaxStamina = _param.Stamina;
            CurrentStamina = _param.Stamina;
            StaminaRegen = _param.StaminaRegen;
            AttackDamage = _param.AttackDamage;
        }
    }
}