using System;
using InGame.Player.Sarutobi;
using September.Common;
using September.InGame.Common;
using UnityEngine;

namespace InGame.Player.Ability.Condition
{
    [Serializable]
    public class SarutobiAttackCondition : IAbilityExecuteCondition
    {
        public string TargetAbilityName => nameof(AbilityMultiHitAttack);
        private PlayerMovement _playerMovement;
        private PlayerManager _playerManager;
        private ThrowKunai _throwKunai;

        public bool IsConditionMatch(in TriggerEventContext context)
        {
            if (!context.Owner) return false;
            if (!_playerMovement) _playerMovement = context.Owner.GetComponent<PlayerMovement>();
            if (!_playerManager) _playerManager = context.Owner.GetComponent<PlayerManager>();
            if (!_throwKunai) _throwKunai = context.Owner.GetComponent<ThrowKunai>();

            //Available状態でAttackボタンが押されたら条件を満たす
            return _playerMovement.IsGround && !_playerManager.IsStun && !IsGameEnded() &&
                   _playerManager.CurrentPlayerControlState == PlayerManager.PlayerControlState.Normal &&
                   _throwKunai.State == ThrowKunai.KunaiStateType.Idol &&
                   context.AbilityRef.Phase == AbilityBase.AbilityPhase.Available &&
                   context.AbilityRef.CanStartAbilityOverride() &&
                   context.CurrentButtons.GetPressed(context.PreviousButtons).IsSet(PlayerButtons.Attack);
        }

        private bool IsGameEnded()
        {
            try
            {
                var inGameManager = StaticServiceLocator.Instance.Get<InGameManager>();
                if (inGameManager == null)
                {
                    Debug.Log("[IsGameEnded] inGameManager is NULL");
                    return false;
                }


                return inGameManager.CurrentStateName != "PlayingState" && inGameManager.CurrentStateName != "TutorialState";
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[IsGameEnded] Exception: {e}");
                return true;
            }
        }
    }
}
