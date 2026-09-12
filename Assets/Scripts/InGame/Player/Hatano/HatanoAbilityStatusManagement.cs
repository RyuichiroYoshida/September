using System.Collections.Generic;
using Fusion;
using InGame.Common;
using UnityEngine;
using September.Common;

namespace InGame.Player.Hatano
{
    /// <summary>
    /// ハタノ・エリクソンのAbility選択状態を管理する
    /// </summary>
    public class HatanoAbilityStatusManagement : NetworkBehaviour
    {
        [SerializeField] private AnimationClipPlayer _animClipPlayer;
        [SerializeField] private HatanoChangeAnimationController _changeAnimation;
        [Header("切替アニメーション（D→L）"), SerializeField] private AnimationClip _changeDLClip;
        [Header("切替アニメーション（L→D）"), SerializeField] private AnimationClip _changeLDClip;
        [Header("切替アニメーション（構えD→L）"), SerializeField] private AnimationClip _changeAimDLClip;
        [Header("切替アニメーション（構えL→D）"), SerializeField] private AnimationClip _changeAimLDClip;
        [Header("レーザー銃"), SerializeField] private GameObject _laser;
        [Header("二丁拳銃"), SerializeField] private List<GameObject> _doubles;
        [Header("現在の選択中のAbility")]
        [Networked] private HatanoAbilityStatus _abilityStatus {get; set;}
        public HatanoAbilityStatus AbilityStatus => _abilityStatus;
        public HatanoAbilityStatus _lastAbilityStatus;

        private HatanoAbilityStatusUIManager _abilityStatusUIManager;
        private AimCameraController _aimCameraController;
        private bool _isChangeAbilityInput; //Abilityの変更入力

        private void Awake()
        {
            _abilityStatusUIManager = GetComponent<HatanoAbilityStatusUIManager>();
            _aimCameraController = GetComponent<AimCameraController>();
        }

        public override void Spawned()
        {
            _abilityStatus = HatanoAbilityStatus.DoubleBarreledGun;
        }

        public override void FixedUpdateNetwork()
        {
            if (_abilityStatus != _lastAbilityStatus)
            {
                _lastAbilityStatus = _abilityStatus;

                // UI更新
                _abilityStatusUIManager.SelectedAbilityUITextChanged(_abilityStatus);
            }
            
            if (!HasInputAuthority) return;
            // 入力がなかったら処理を行わない
            if (!GetInput<PlayerInput>(out var input)) return;

            if (input.Buttons.IsSet(PlayerButtons.Ability1) && !_isChangeAbilityInput)
            {
                _isChangeAbilityInput = true;
                var next = GetNextHatanoAbilityStatus();

                // アビリティの変更
                if (HasStateAuthority)
                {
                    _abilityStatus = next;
                }
                else
                {
                    RPC_ChangeAbilityStatus(next);
                }
                // アビリティの変更があったタイミングで切り替え等の処理を実行
                ChangeAbility(_abilityStatus);
            }

            if (!input.Buttons.IsSet(PlayerButtons.Ability1) && _isChangeAbilityInput)
            {
                _isChangeAbilityInput = false;
            }
        }

        /// <summary>
        /// 現在のアビリティに応じてアビリティの変更を行う
        /// </summary>
        /// <returns>変更後のアビリティ</returns>
        private HatanoAbilityStatus GetNextHatanoAbilityStatus()
        {
            return _abilityStatus switch
            {
                HatanoAbilityStatus.DoubleBarreledGun => HatanoAbilityStatus.LaserGun,
                HatanoAbilityStatus.LaserGun => HatanoAbilityStatus.DoubleBarreledGun
            };
        }

        /// <summary>
        /// アビリティの変更を行う
        /// </summary>
        /// <param name="status">変更後のアビリティ</param>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_ChangeAbilityStatus(HatanoAbilityStatus status)
        {
            _abilityStatus = status;
        }

        /// <summary>
        /// アビリティの変更
        /// </summary>
        /// <param name="status">変更後のアビリティ</param>
        private void ChangeAbility(HatanoAbilityStatus status)
        {
            // 表示する銃とアニメーションを変更
            switch (status)
            {
                case HatanoAbilityStatus.LaserGun:
                    _animClipPlayer.PlayOnUpperBody(null);
                    _animClipPlayer.PlayClip(_aimCameraController.IsAim ? _changeAimDLClip : _changeDLClip);
                    break;
                case HatanoAbilityStatus.DoubleBarreledGun:
                    _animClipPlayer.PlayOnUpperBody(null);
                    _animClipPlayer.PlayClip(_aimCameraController.IsAim ? _changeAimLDClip : _changeLDClip);
                    break;
            }
            
            _changeAnimation.ChangeMoveAnimation(status);
        }
    }
}
