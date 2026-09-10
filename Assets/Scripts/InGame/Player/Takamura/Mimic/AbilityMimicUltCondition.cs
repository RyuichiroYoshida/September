using System;
using Fusion;
using InGame.Player.Ability;
using InGame.Player.Ult;
using September.Common;
using UnityEngine;

namespace InGame.Player.Takamura.Mimic
{
    /// <summary>
    /// Ability3押下時にULTの共通条件と擬態対象の有無を確認する。
    /// 対象がいない場合はAbilityを開始しないため、ULTゲージも消費されない。
    /// </summary>
    [Serializable]
    public sealed class AbilityMimicUltCondition : IAbilityExecuteCondition
    {
        [SerializeField] private string _targetAbilityName = nameof(AbilityMimicUlt);
        [SerializeField] private PlayerButtons _button = PlayerButtons.Ultimate;

        private PlayerManager _playerManager;
        private PlayerInputManager _inputManager;
        private IUltCondition _ultCondition;

        public string TargetAbilityName => _targetAbilityName;

        public bool IsConditionMatch(in TriggerEventContext context)
        {
            if (context.AbilityRef is not AbilityMimicUlt mimicUlt)
                return false;

            // 必要な参照の確認
            if (!_playerManager)
                _playerManager = context.Owner.GetComponent<PlayerManager>();
            if (!_inputManager)
                _inputManager = context.Owner.GetComponent<PlayerInputManager>();
            _ultCondition ??= context.Owner.GetComponent<IUltCondition>();

            // 参照が一つでもそろわなかったら終了
            if (!_playerManager || !_inputManager || _ultCondition == null)
                return false;

            // 発動ボタンを押したかどうか
            var pressed = context.CurrentButtons
                .GetPressed(context.PreviousButtons)
                .IsSet(_button);

            // 発動条件を満たしていないなら終了
            if (!pressed
                || context.AbilityRef.Phase != AbilityBase.AbilityPhase.Available
                || _playerManager.CurrentPlayerControlState != PlayerManager.PlayerControlState.Normal
                || _playerManager.IsStun
                || !_ultCondition.IsAvailable())
            {
                return false;
            }

            if (!_inputManager.GetPlayerInput(out var input))
                return false;

            // 擬態の準備をする
            return mimicUlt.TryPrepareTarget(context.Owner, in input);
        }
    }
}
