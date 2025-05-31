using System;
using Fusion;
using September.Common;
using UnityEngine;

namespace InGame.Player.Ability.Mock
{
    public class AbilityManager : NetworkBehaviour
    {
        [SerializeReference, SubclassSelector] AbilityBase[] _abilities;
        
        // 入力でWasPressedとか使うのに必要
        [Networked] private NetworkButtons PreviousButtons { get; set; }

        private void Awake()
        {
            PlayerStatus playerStatus = GetComponentInParent<PlayerStatus>();
            
            foreach (var ability in _abilities)
            {
                ability.InitAbility(playerStatus);
            }
        }

        public override void FixedUpdateNetwork()
        {
            // 入力
            if (GetInput<PlayerInput>(out var input))
            {
                

                PreviousButtons = input.Buttons;
            }
            
            // updateの更新
            foreach (var ability in _abilities)
            {
                ability.UpdateAbility(Runner.DeltaTime);
            }
        }
    }

    [Serializable]
    public abstract class AbilityBase
    {
        [SerializeField] AbilityInputType _abilityInputType;
        [SerializeField] private float _cooldown;
        
        PlayerStatus _ownerPlayerStatus;
        float _cooldownTimer;

        // Ability初期化
        public void InitAbility(PlayerStatus ownerPlayerStatus)
        {
            _ownerPlayerStatus = ownerPlayerStatus;
        }
        // Abilityの発動
        public abstract void ActivateAbility();
        public abstract void UpdateAbility(float deltaTime);
        // Abilityの終了
        public abstract void EndAbility();
        // Abilityが発動かどうか
        public abstract bool CanActivateAbility();
    }

    [Serializable]
    public class AbilityA : AbilityBase
    {
        public override void UpdateAbility(float deltaTime)
        {
        }

        public override void ActivateAbility()
        {
        }

        public override void EndAbility()
        {
        }

        public override bool CanActivateAbility()
        {
            return true;
        }
    }

    [Serializable]
    public class AbilityB : AbilityBase
    {
        public override void UpdateAbility(float deltaTime)
        {
        }

        public override void ActivateAbility()
        {
        }

        public override void EndAbility()
        {
        }

        public override bool CanActivateAbility()
        {
            return true;
        }
    }

    enum AbilityInputType
    {
        RightClick,
        LeftClick,
        Shift,
        E
    }
}
