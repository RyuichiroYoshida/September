using September.Common;
using September.InGame.Common;

namespace InGame.Player.Ability.Condition
{
    /// <summary>擬態解除時の攻撃の発動条件</summary>
    public class AbilityRevealAttackCondition : IAbilityExecuteCondition
    {
        public string TargetAbilityName => nameof(AbilityRevealAttack);

        PlayerMovement _playerMovement;
        PlayerManager _playerManager;

        public bool IsConditionMatch(in TriggerEventContext context)
        {
            if (!context.Owner) return false;
            if (!_playerMovement) _playerMovement = context.Owner.GetComponent<PlayerMovement>();
            if (!_playerManager) _playerManager = context.Owner.GetComponent<PlayerManager>();

            // 展示物に擬態しているかどうかを判定
            var isMimicking = _playerMovement is TakamuraMovement typed
                && typed.CurrentMimicryState == MimicryState.MimicExhibit
                && typed.CurrentAbilityPhase == ScanAbilityPhase.Default;

            //Available状態でAttackボタンが押されたら条件を満たす
            return _playerMovement.IsGround && !_playerManager.IsStun && !IsGameEnded() &&
                   _playerManager.CurrentPlayerControlState == PlayerManager.PlayerControlState.Normal &&
                   context.AbilityRef.Phase == AbilityBase.AbilityPhase.Available &&
                   context.AbilityRef.CanStartAbilityOverride() &&
                   context.CurrentButtons.GetPressed(context.PreviousButtons).IsSet(PlayerButtons.Attack)
                   && isMimicking;
        }

        private bool IsGameEnded()
        {
            try
            {
                var inGameManager = StaticServiceLocator.Instance.Get<InGameManager>();
                if (inGameManager == null) return false;

                // EndingStateかPlayingState以外の場合は攻撃を無効にする
                return inGameManager.CurrentStateName != "PlayingState";
            }
            catch (System.Exception)
            {
                // エラーが発生した場合は安全側に攻撃を無効にする
                return true;
            }
        }
    }
}
