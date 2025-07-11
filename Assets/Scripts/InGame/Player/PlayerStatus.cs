using System.Collections.Generic;
using System.Linq;
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
        private void OnChangeHealth() => _currentHealth.Value = (int)CurrentHealth;
        public float MaxStamina { get; private set; }
        public float MaxSpeedRate { get; set; } = 1;
        [Networked, HideInInspector, OnChangedRender(nameof(OnChangeStamina))] public float CurrentStamina { get; set; }
        private void OnChangeStamina() => _currentStamina.Value = CurrentStamina;
        public float StaminaRegen { get; private set; }
        public int AttackDamage { get; set; }

        private List<ActiveEffect> _activeEffects = new();

        private Stat _healthStat;
        private Stat _speedStat;
        private Stat _staminaStat;
        private Stat _staminaRegenStat;
        private Stat _attackDamageStat;
        
        public Stat HealthStat => _healthStat;
        public Stat SpeedStat => _speedStat;
        public Stat StaminaStat => _staminaStat;
        public Stat StaminaRegenStat => _staminaRegenStat;
        public Stat AttackDamageStat => _attackDamageStat;
        
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

            _healthStat = new Stat(_param.Health, _activeEffects);
            _speedStat = new Stat(_param.Speed, _activeEffects);
            _staminaStat = new Stat(_param.Stamina, _activeEffects);
            _staminaRegenStat = new Stat(_param.StaminaRegen, _activeEffects);
            _attackDamageStat = new Stat(_param.AttackDamage, _activeEffects);
        }

        public override void FixedUpdateNetwork()
        {
            UpdateEffects(Runner.DeltaTime);
        }

        void UpdateEffects(float deltaTime)
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                // 期限切れのEffectは削除
                if (_activeEffects[i].IsExpired)
                {
                    _activeEffects.RemoveAt(i);
                    continue;
                }
                
                _activeEffects[i].Tick(deltaTime);
            }
        }

        public void AddEffect(StatusEffect effect)
        {
            var targetEffect = _activeEffects.Find(activeEffect => activeEffect.EffectData == effect);

            if (targetEffect == null)
            {
                Debug.Log("apply new effect");
                _ = new ActiveEffect(effect, this);
            }
            else
            {
                targetEffect.AddStack();
                Debug.Log($"apply add stack : {targetEffect.StackCount}");
            }
        }

        public void RemoveEffect(StatusEffect effect)
        {
            var targetIndex = _activeEffects.FindIndex(activeEffect => activeEffect.EffectData == effect);

            if (targetIndex != -1)
            {
                _activeEffects[targetIndex].RemoveStack();
                Debug.Log($"remove stack : {_activeEffects[targetIndex].StackCount}");
            }
        }

        void PostApplyEffect(StatusEffect effect)
        {
            // Clampとかする
        }

        public ref Stat GetStatFromType(StatType type)
        {
            if (type == StatType.Health) return ref _healthStat;
            if (type == StatType.Speed) return ref _speedStat;
            if (type == StatType.Stamina) return ref _staminaStat;
            if (type == StatType.StaminaRegen) return ref _staminaRegenStat;
            return ref _attackDamageStat;
        }

        public struct Stat
        {
            private readonly List<ActiveEffect> _activeEffects;
            
            public float BaseValue;
            public float Value { get; private set; }

            /// <summary> エフェクトの更新通知 </summary>
            public void EffectUpdateNotice()
            {
                Value = BaseValue;
                float multiplyValue = 1;

                foreach (var effect in _activeEffects)
                {
                    if (effect.EffectData.DurationType == DurationType.Instant || effect.EffectData.IsPeriodic) continue;

                    for (int i = 0; i < (effect.EffectData.StackOp.ModifierAppliesPerStack ? effect.StackCount : 1); i++)
                    {
                        foreach (var modifier in effect.EffectData.ParamModifiers)
                        {
                            if (modifier.ModifierOp == ModifierOperation.Add) Value += modifier.Magnitude;
                            else if (modifier.ModifierOp == ModifierOperation.Multiply) multiplyValue *= modifier.Magnitude;
                            else
                            {
                                Value = modifier.Magnitude;
                                return;
                            }
                        }
                    }
                }

                Value *= multiplyValue;
            }

            public void SetValue(float value)
            {
                Value = value;
                BaseValue = value;
            }

            public Stat(float baseValue, List<ActiveEffect> activeEffects)
            {
                _activeEffects = activeEffects;
                Value = baseValue;
                BaseValue = baseValue;
            }
        }

        /// <summary> Effect 用 </summary>
        public enum StatType
        {
            Health,
            Speed,
            Stamina,
            StaminaRegen,
            AttackDamage
        }
        
        /// <summary> StatEffectを発動中管理する </summary>
        public class ActiveEffect
        {
            private readonly PlayerStatus _targetStatus;
            private readonly List<float> _timers = new();
            private readonly List<float> _periodTimers = new();

            public readonly StatusEffect EffectData;
            public int StackCount { get; private set; }
            public float LeadTimer => _timers.Any() ? _timers[0] : 0;
            public bool IsExpired => StackCount <= 0;

            public ActiveEffect(StatusEffect effectData, PlayerStatus targetStatus)
            {
                EffectData = effectData;
                _targetStatus = targetStatus;

                if (effectData.DurationType == DurationType.Instant)
                {
                    ApplyEffect();
                    return;
                }

                // 後にListを使った更新通知を出すのでこのタイミングでAddする
                _targetStatus._activeEffects.Add(this);
                
                AddStack();
            }

            public void AddStack()
            {
                // instant な effect や Stack 数が Max なら Stack は増えない
                if (EffectData.DurationType == DurationType.Instant || EffectData.StackOp.LimitCount <= StackCount) return;
                
                if (EffectData.DurationType == DurationType.HasDuration)
                {
                    _timers.Add(EffectData.Duration);
                }
                
                StackCount++;

                // ModifierAppliesPerStack == false なら一つのTimerでいい
                if (EffectData.IsPeriodic && (EffectData.StackOp.ModifierAppliesPerStack || !_periodTimers.Any()))
                {
                    _periodTimers.Add(EffectData.Period);
                }
                
                ApplyEffect();

                if (EffectData.StackOp.RefreshDuration)
                {
                    for (int i = 0; i < StackCount; i++)
                    {
                        _timers[i] = EffectData.Duration;
                    }
                }
            }

            public void Tick(float deltaTime)
            {
                if (EffectData.Duration == 0) return;
                
                // has duration Timer 管理
                if (EffectData.Duration > 0)
                {
                    for (int i = 0; i < _timers.Count; i++)
                    {
                        _timers[i] -= deltaTime;
                    }

                    if (_timers.Any() && _timers[0] <= 0)
                    {
                        RemoveStack();
                    }
                }
                
                // period timer
                for (int i = 0; i < (EffectData.StackOp.ModifierAppliesPerStack ? StackCount : 1); i++)
                {
                    _periodTimers[i] -= deltaTime;
                    
                    if (_periodTimers[i] <= 0)
                    {
                        _periodTimers[i] += EffectData.Period;
                        ApplyEffect();
                    }
                }
            }

            void ApplyEffect()
            {
                foreach (var modifier in EffectData.ParamModifiers)
                {
                    ApplyModifier(modifier, ref _targetStatus.GetStatFromType(modifier.StatType));
                }
                
                _targetStatus.PostApplyEffect(EffectData);
            }

            void ApplyModifier(StatModifier modifier, ref Stat statToEffect)
            {
                switch (modifier.ModifierOp)
                {
                    case ModifierOperation.Add:
                        if (EffectData.DurationType == DurationType.Instant || EffectData.IsPeriodic) statToEffect.BaseValue += modifier.Magnitude;
                        break;
                    case ModifierOperation.Multiply:
                        if (EffectData.DurationType == DurationType.Instant || EffectData.IsPeriodic) statToEffect.BaseValue *= modifier.Magnitude;
                        break;
                    case ModifierOperation.Override:
                        if (EffectData.DurationType == DurationType.Instant || EffectData.IsPeriodic) statToEffect.BaseValue = modifier.Magnitude;
                        break;
                }
                
                statToEffect.EffectUpdateNotice();
            }

            public void RemoveStack()
            {
                if (StackCount <= 0) return;

                if (EffectData.StackOp.ExpirationPolicy == ExpirationPolicyType.RemoveAllStack)
                {
                    _timers.Clear();
                    _periodTimers.Clear();
                    StackCount = 0;
                }
                else
                {
                    if (EffectData.DurationType == DurationType.HasDuration)
                    {
                        _timers.RemoveAt(0);
                        _periodTimers.RemoveAt(0);

                        if (EffectData.StackOp.ExpirationPolicy == ExpirationPolicyType.AndRefreshDuration)
                        {
                            for (int i = 0; i < StackCount; i++)
                            {
                                _timers[i] = EffectData.Duration;
                            }
                        }
                    }
                    
                    StackCount--;
                }

                // stat に変更の通知
                foreach (var modifier in EffectData.ParamModifiers)
                {
                    _targetStatus.GetStatFromType(modifier.StatType).EffectUpdateNotice();
                }
            }
        }
    }
}