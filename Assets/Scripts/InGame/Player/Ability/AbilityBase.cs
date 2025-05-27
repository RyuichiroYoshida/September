using System;
using System.Collections.Generic;
using System.Linq;
using InGame.Common;
using September.Common;
using UnityEngine;

namespace InGame.Player.Ability
{
    /// <summary>
    /// アビリティの基底クラス
    /// </summary>
    [Serializable]
    public abstract class AbilityBase
    {
        public enum AbilityPhase
        {
            None,
            Started,
            Active,
            Ended
        }

        [SerializeField] private AbilityName _abilityName;
        [SerializeField] protected float _cooldown;
        
        private readonly CooldownManager _cooldownManager = new();
        private AbilityPhase _phase = AbilityPhase.None;
        protected ISpawner _spawner;
        
        public event Action OnEndAbilityEvent;
        
        public float Cooldown => _cooldown;
        public float CurrentCooldown => _cooldownManager.CurrentCooldown;
        public virtual string DisplayName => AbilityName.ToString();
        public AbilityName AbilityName => _abilityName;
        public AbilityContext Context { get; private set; }
        public abstract bool RunLocal { get; }
        public bool IsActive => _phase == AbilityPhase.Active;
        protected bool IsCooldown => _cooldownManager.IsCooldown;

        protected AbilityBase() {}
        
        protected AbilityBase(AbilityBase abilityReference)
        {
            _abilityName = abilityReference._abilityName;
            _cooldown = abilityReference._cooldown;
        }

        public abstract AbilityBase Clone(AbilityBase abilityReference);

        /// <summary>
        /// アビリティの初期化処理
        /// 実際のアビリティの挙動はStartかUpdateで実装してください
        /// </summary>
        public virtual void InitAbility(AbilityContext context, ISpawner spawner)
        {
            Context = context;
            _spawner = spawner ?? StaticServiceLocator.Instance.Get<ISpawner>();
        }

        public void Tick(float deltaTime)
        {
            ProcessPhase();
        }

        public void TickTime(float deltaTime)
        {
            _cooldownManager.Tick(deltaTime);
        }

        private void ProcessPhase()
        {
            switch (_phase)
            {
                case AbilityPhase.None:
                    break;
                case AbilityPhase.Started:
                    OnStart();
                    _phase = AbilityPhase.Active;
                    break;
                case AbilityPhase.Active:
                    OnUpdate(Time.deltaTime);
                    break;
                case AbilityPhase.Ended:
                    ExecuteEndAbility();
                    break;
            }
        }

        public virtual bool TryInitializeWithTrigger(AbilityContext context,
            List<AbilityRuntimeInfo> currentPlayerActiveAbilityInfo, ISpawner spawner)
        {
            switch (context.ActionType)
            {
                case AbilityActionType.発動:
                    return TryStartAbility(context, currentPlayerActiveAbilityInfo, spawner);
                case AbilityActionType.停止:
                    return TryStopAbility(currentPlayerActiveAbilityInfo);
                default:
                    return false;
            }
        }

        private bool TryStartAbility(AbilityContext context, 
            List<AbilityRuntimeInfo> currentActiveAbilities, ISpawner spawner)
        {
            if (IsAbilityAlreadyActive(currentActiveAbilities))
                return false;

            InitAbility(context, spawner);
            _phase = AbilityPhase.Started;
            _cooldownManager.StartCooldown(_cooldown);
            return true;
        }

        private bool TryStopAbility(List<AbilityRuntimeInfo> currentActiveAbilities)
        {
            var runningAbility = FindRunningAbility(currentActiveAbilities);
            if (runningAbility == null)
                return false;

            runningAbility.Instance.ForceEnd();
            return true;
        }

        private bool IsAbilityAlreadyActive(List<AbilityRuntimeInfo> currentActiveAbilities)
        {
            return currentActiveAbilities?.Any(x => x.Instance.AbilityName == AbilityName) == true;
        }

        private AbilityRuntimeInfo FindRunningAbility(List<AbilityRuntimeInfo> currentActiveAbilities)
        {
            return currentActiveAbilities?.FirstOrDefault(x => x.Instance.AbilityName == AbilityName);
        }

        protected virtual void OnStart() {}
        protected virtual void OnUpdate(float deltaTime) {}
        public virtual void OnEndAbility() {}

        public virtual void ForceEnd()
        {
            _phase = AbilityPhase.Ended;
        }

        private void ExecuteEndAbility()
        {
            OnEndAbility();
            OnEndAbilityEvent?.Invoke();
            _phase = AbilityPhase.None;
        }
    }

    /// <summary>
    /// クールダウン管理を責務分離したクラス
    /// </summary>
    [Serializable]
    internal class CooldownManager
    {
        private float _cooldownTimer = 0f;
        private bool _shouldTickCooldown = false;

        public float CurrentCooldown => Mathf.Max(0f, _cooldownTimer);
        public bool IsCooldown => _cooldownTimer > 0f;

        public void StartCooldown(float cooldownTime)
        {
            _cooldownTimer = cooldownTime;
            _shouldTickCooldown = true;
        }

        public void Tick(float deltaTime)
        {
            if (!_shouldTickCooldown || _cooldownTimer <= 0f)
                return;

            _cooldownTimer -= deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _cooldownTimer = 0f;
                _shouldTickCooldown = false;
            }
        }

        public void ResetCooldown()
        {
            _cooldownTimer = 0f;
            _shouldTickCooldown = false;
        }
    }
}