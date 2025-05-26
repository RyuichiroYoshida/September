using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace InGame.Player.Ability
{
    /// <summary>
    /// アビリティの基底クラス
    /// CalculateSharedVariable以外はサーバー側でのみ実行されることを想定しています。
    /// </summary>
    [Serializable]
    public abstract class AbilityBase
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
        protected AbilityPhase _phase = AbilityPhase.None;
        protected ISpawner _spawner;
        public event Action OnEndAbilityEvent;
        public float Cooldown => _cooldown;
        public float CurrentCooldown { get; protected set; }
        public virtual string DisplayName => AbilityName.ToString();
        public AbilityName AbilityName => _abilityName;
        public AbilityContext Context { get; private set; }
        public abstract bool RunLocal { get; }  // クライアント側で実行するかどうか

        protected AbilityBase() {}
        protected AbilityBase(AbilityBase abilityReference)
        {
            _abilityName = abilityReference._abilityName;
            _cooldown = abilityReference._cooldown;
        }

        protected bool IsCooldown => CurrentCooldown > 0f;

        /// <summary>
        /// ホストとクライアントで共有する変数の計算を行う
        /// 例えばクールタイムはクライアント側でも現在の値を参照したいのでここに書く
        /// </summary>
        /// <param name="deltaTime"></param>
        public virtual void CalculateSharedVariable(float deltaTime)
        {
            if (CurrentCooldown > 0f) CurrentCooldown -= deltaTime;
            if (CurrentCooldown < 0f) CurrentCooldown = 0f;
        }
        
        public virtual void ResetSharedVariable()
        {
            CurrentCooldown = _cooldown;
        }

        public abstract AbilityBase Clone(AbilityBase abilityReference);
        
        /// <summary>
        /// ここには初期化処理だけ書いてください
        /// 実際のアビリティの挙動はStartかUpdateにお願いします
        /// </summary>
        /// <param name="context"></param>
        /// <param name="spawner"></param>
        public virtual void InitAbility(AbilityContext context, ISpawner spawner)
        {
            Context = context;
            _spawner = spawner;
        }

        public void Tick(float deltaTime)
        {
            switch (_phase)
            {
                case AbilityPhase.None: break;
                case AbilityPhase.Started:
                    OnStart();
                    _phase = AbilityPhase.Active;
                    break;
                case AbilityPhase.Active:
                    OnUpdate(deltaTime);
                    break;
                case AbilityPhase.Ended:
                    EndAbility();
                    break;
            }
        }

        public virtual bool TryInitializeWithTrigger(AbilityContext context,
            List<AbilityRuntimeInfo> currentPlayerActiveAbilityInfo, ISpawner spawner)
        {
            switch (context.ActionType)
            {
                case AbilityActionType.発動:
                    if (currentPlayerActiveAbilityInfo == null || currentPlayerActiveAbilityInfo.All(x => x.Instance.AbilityName != AbilityName))
                    {
                        InitAbility(context, spawner);
                        _phase = AbilityPhase.Started;
                        return true;
                    }
                    break;
                case AbilityActionType.停止:
                    var currentRunningAbility = currentPlayerActiveAbilityInfo.FirstOrDefault(x => x.Instance.AbilityName == AbilityName);
                    if (currentRunningAbility != null)
                    {
                        currentRunningAbility.Instance.ForceEnd();
                        _phase = AbilityPhase.Ending;
                    }
                    break;
            }
            return false;
        }
        protected virtual void OnStart() {}
        protected virtual void OnUpdate(float deltaTime) {}
        public virtual void ForceEnd() => _phase = AbilityPhase.Ended;
        public virtual void OnEndAbility() {}
        protected void EndAbility()
        {
            OnEndAbility();
            OnEndAbilityEvent?.Invoke();
        }
    }

    
}
