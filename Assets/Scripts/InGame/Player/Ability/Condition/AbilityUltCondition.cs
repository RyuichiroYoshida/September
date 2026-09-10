using System;
using Fusion;
using InGame.Player.Ult;
using September.Common;
using UnityEngine;

namespace InGame.Player.Ability.Condition
{
    [Serializable]
    public class AbilityUltCondition : IAbilityExecuteCondition
    {
        [SerializeField] private string _targetAbilityName;
        [SerializeField] private PlayerButtons _button = PlayerButtons.Ultimate;
        
        private PlayerManager _playerManager;
        private IUltCondition _condition;
        
        public string TargetAbilityName => _targetAbilityName;
        
        public bool IsConditionMatch(in TriggerEventContext context)
        {
            if (!_playerManager) _playerManager = context.Owner.GetComponent<PlayerManager>();
            _condition ??= context.Owner.GetComponent<IUltCondition>();

            if (_playerManager == null || _condition == null) return false;
            
            if (!context.CurrentButtons.GetPressed(context.PreviousButtons).IsSet(_button) ||
                !PlayerDatabase.Instance.PlayerDataDic.TryGet(NetworkRunner.Instances[0].LocalPlayer, out _)) return false;
            
            if (context.AbilityRef.Phase != AbilityBase.AbilityPhase.Available
                || _playerManager.CurrentPlayerControlState != PlayerManager.PlayerControlState.Normal
                || _playerManager.IsStun) return false;
            

            if (!_condition.IsAvailable()) return false;
            
            return true;
        }
    }
}
