using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using InGame.Common;
using September.Common;
using UnityEngine;

namespace InGame.Player.Ability
{
    [Serializable]
    public abstract class AbilityBase : ISharedAbilityStateReceiver
    {
        public enum AbilityPhase
        {
            None,
            Started,
            Active,
            Ending,
            Ended
        }

        [SerializeField] private AbilityName _abilityName;
        [SerializeField] protected float _cooldown;

        private readonly CooldownManager _cooldownManager = new();
        protected ISpawner _spawner;

        public event Action OnEndAbilityEvent;

        public float Cooldown => _cooldown;
        public float CurrentCooldown => _cooldownManager.CurrentCooldown;
        public virtual string DisplayName => AbilityName.ToString();
        public AbilityName AbilityName => _abilityName;
        public AbilityContext Context { get; private set; }
        public abstract bool RunLocal { get; }
        public bool AfterCooldown { get; private set; }
        protected bool IsCooldown => _cooldownManager.IsCooldown;
        
        protected int OwnerPlayerId { get; private set; } = -1;

        public AbilityPhase Phase { get; private set; } = AbilityPhase.None;

        protected AbilityBase() { }

        protected AbilityBase(AbilityBase abilityReference)
        {
            _abilityName = abilityReference._abilityName;
            _cooldown = abilityReference._cooldown;
        }

        public abstract AbilityBase Clone(AbilityBase abilityReference);

        public virtual void InitAbility(AbilityContext context, ISpawner spawner)
        {
            Context = context;
            OwnerPlayerId = context.SourcePlayer;
            _spawner = spawner ?? StaticServiceLocator.Instance.Get<ISpawner>();
        }

        public void Tick(float deltaTime)
        {
            ProcessPhase(deltaTime);
        }

        public void CalculateSharedVariable(float deltaTime)
        {
            _cooldownManager.Tick(deltaTime);
            OnCalculateSharedVariable(deltaTime);
        }

        protected virtual void OnCalculateSharedVariable(float deltaTime) { }

        protected void StartCooldown(float duration)
        {
            _cooldownManager.StartCooldown(duration);
            _cooldownManager.OnCooldownEnd += () => { AfterCooldown = true; };
        }

        private void ProcessPhase(float deltaTime)
        {
            switch (Phase)
            {
                case AbilityPhase.None:
                    break;
                case AbilityPhase.Started:
                    Phase = AbilityPhase.Active;
                    OnStart();
                    break;
                case AbilityPhase.Active:
                    OnUpdate(deltaTime);
                    break;
                case AbilityPhase.Ending:
                    ExecuteEndAbility();
                    break;
                case AbilityPhase.Ended:
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
            Phase = AbilityPhase.Started;
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

        protected virtual void OnStart() { }
        protected virtual void OnUpdate(float deltaTime) { }

        public virtual void OnEndAbility() { }

        public virtual void ForceEnd()
        {
            Phase = AbilityPhase.Ending;
        }

        private void ExecuteEndAbility()
        {
            OnEndAbility();
            OnEndAbilityEvent?.Invoke();
            Phase = AbilityPhase.Ended;
        }

        public virtual void ApplySharedState(AbilitySharedState sharedState)
        {
            if (sharedState.IsFloorActive == 1) StartCooldown(_cooldown);
        }
    }

    /// <summary>
    /// 全アビリティで共有する構造体
    /// </summary>
    public struct AbilitySharedState : INetworkStruct
    {
        public AbilityName AbilityName { get; set; }
        public int OwnerPlayerId { get; set; }
        public int IsFloorActive { get; set; }
    }

    public interface ISharedAbilityStateReceiver
    {
        void ApplySharedState(AbilitySharedState sharedState);
    }

    [Serializable]
    internal class CooldownManager
    {
        public enum CooldownState
        {
            BeforeCooldown,
            CountDowning,
            AfterCooldown
        }
        
        private float _cooldownTimer = 0;

        public float CurrentCooldown => Mathf.Max(0f, _cooldownTimer);
        public bool IsCooldown => CurrentCooldownState == CooldownState.CountDowning && _cooldownTimer > 0f;
        public CooldownState CurrentCooldownState { get; private set; } = CooldownState.BeforeCooldown;

        public Action OnCooldownEnd;

        public void StartCooldown(float duration)
        {
            _cooldownTimer = duration;
            CurrentCooldownState = CooldownState.CountDowning;
        }

        public void Tick(float deltaTime)
        {
            if (!CurrentCooldownState.Equals(CooldownState.CountDowning))
                return;

            _cooldownTimer -= deltaTime;

            if (_cooldownTimer <= 0f)
            {
                _cooldownTimer = 0f; 
                CurrentCooldownState = CooldownState.AfterCooldown;
                OnCooldownEnd?.Invoke();
            }
        }

        public void ResetCooldown()
        {
            _cooldownTimer = 0;
            CurrentCooldownState = CooldownState.BeforeCooldown;
        }
    }
}
