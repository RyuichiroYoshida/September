using System;
using System.Collections.Generic;
using Fusion;
using InGame.Common;
using InGame.Health;
using September.Common;
using September.InGame.Common.Stats;
using September.InGame.Effect;
using UnityEngine;

namespace InGame.Player.Ability
{
    /// <summary>
    /// 入力された回数に応じて連続して攻撃を行うAbility。
    /// </summary>
    [Serializable]
    public class AbilityContinuousAttack : AbilityBase
    {
        [SerializeField] private AnimationClipPlayer _animationClipPlayer;
        [SerializeField] private AttackData[] _attackDatas;
        [SerializeField] private PlayerInputManager _playerInputManager;
        [SerializeField] private PlayerButtons _continueAttackButton;

        [Header("Hit Box 設定")]
        [SerializeField] private Vector3 _boxHalfExtents = new Vector3(0.45f, 0.85f, 0.45f);
        [SerializeField] private Vector3 _boxLocalOffset = new Vector3(0f, 0.9f, 0.6f);
        [SerializeField] private float _boxCastDistance = 1.0f;
        [SerializeField] private LayerMask _hitLayer = ~0;
        [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore;
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];
        protected readonly HashSet<Collider> _alreadyHit = new HashSet<Collider>();
        [Header("ヒットエフェクト")]
        [SerializeField] protected EffectType _hitEffect = EffectType.HitNormal;

        [Header("剣")]
        [SerializeField] private Animator _animator;
        [SerializeField] private GameObject _swordObject;
        [SerializeField] private Transform _swordSocket;
        [SerializeField] private HumanBodyBones _swordHandBone;

        [Header("ビルドシステム関連の参照")]
        [SerializeField] protected BuildGenerator _buildGenerator;
        [SerializeField] private PlayerStatus _playerStatus;

        [Serializable]
        public class AttackData
        {
            [SerializeField] private AnimationClip _startAnimationClip;
            [SerializeField] private AnimationClip _endAnimationClip;
            [SerializeField] private int _damageStartFrame;
            [SerializeField] private int _damageEndFrame;
            [SerializeField] private int _damageAmount;
            [SerializeField] private int _nextInputStartFrame;
            [SerializeField] private int _drawSwordFrame;
            [SerializeField] private int _sheathSwordFrame;

            public AnimationClip StartAnimationClip => _startAnimationClip;
            public AnimationClip EndAnimationClip => _endAnimationClip;
            public int DamageStartFrame => _damageStartFrame;
            public int DamageEndFrame => _damageEndFrame;
            public int DamageAmount => _damageAmount;
            public int NextInputStartFrame => _nextInputStartFrame;
            public int DrawSwordFrame => _drawSwordFrame;
            public int SheathSwordFrame => _sheathSwordFrame;
        }

        private enum AttackState
        {
            InputDisabled,
            WaitingForInput,
            Finished
        }

        private AttackState _state;

        private int _currentAttackIndex;
        private int _attackStartTick;

        private int _damageStartTick;
        private int _damageEndTick;
        private int _nextInputStartTick;

        private int _startAnimationEndTick;
        private int _endAnimationEndTick;

        private int _drawSwordTick;
        private int _sheathSwordTick;

        private bool _shouldDrawSword;
        private bool _shouldSheathSword;
        private bool _isNextAttackInputReceived;

        private Vector3 _attackDirection;

        private NetworkButtons _previousButtons;

        private PlayerMovement _playerMovement;
        private EffectSpawner _effectSpawner;

        protected override void OnStart()
        {
            if (!_effectSpawner)
                _effectSpawner = StaticServiceLocator.Instance.Get<EffectSpawner>();

            _playerMovement = Parameter.Owner.GetComponent<PlayerMovement>();

            _attackDirection = _playerInput.DesiredLookDirection;
            _attackDirection.y = 0f;

            if (_attackDirection.sqrMagnitude <= Mathf.Epsilon)
                _attackDirection = Parameter.Owner.transform.forward;

            _playerMovement.SetRotationImmediately(_attackDirection);

            _playerMovement.IgnoreMoveInput = true;
            _playerMovement.IgnoreEvasionInput = true;

            _currentAttackIndex = -1;

            StartAttack(0);
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (_playerMovement == null)
                return;

            _playerMovement.SetRotationImmediately(_attackDirection);

            int elapsed = Runner.Tick - _attackStartTick;

            CheckContinueInput();

            UpdateState(elapsed);
            SetSwordEquipped(elapsed);

            //攻撃する
            if(elapsed >= _damageStartTick && elapsed <= _damageEndTick)
            {
                CastAndApplyHits();
            }
        }

        private void UpdateState(int elapsed)
        {
            switch (_state)
            {
                case AttackState.InputDisabled:
                    UpdateInputDisabledState(elapsed);
                    break;

                case AttackState.WaitingForInput:
                    UpdateWaitingForInputState(elapsed);
                    break;

                case AttackState.Finished:
                    UpdateFinishedState(elapsed);
                    break;
            }
        }

        private void SetSwordEquipped(int elapsed)
        {
            // 剣を取り出す
            if (_shouldDrawSword && elapsed >= _drawSwordTick)
            {
                var parent = _animator.GetBoneTransform(_swordHandBone);
                _swordObject.transform.parent = parent;
                _swordObject.transform.localRotation = Quaternion.identity;
                _swordObject.transform.localPosition = Vector3.zero;
                _shouldDrawSword = false;
            }

            // 剣をしまう
            if (_shouldSheathSword && elapsed >= _sheathSwordTick)
            {
                _swordObject.transform.parent = _swordSocket;
                _swordObject.transform.localRotation = Quaternion.identity;
                _swordObject.transform.localPosition = Vector3.zero;
                _shouldSheathSword = false;
            }
        }

        private void UpdateInputDisabledState(int elapsed)
        {
            if (elapsed < _nextInputStartTick)
                return;

            _isNextAttackInputReceived = false;
            _state = AttackState.WaitingForInput;
        }

        private void UpdateWaitingForInputState(int elapsed)
        {
            if (elapsed < _startAnimationEndTick)
                return;

            if (_isNextAttackInputReceived)
            {
                StartAttack(_currentAttackIndex + 1);
                return;
            }

            EndAttack();
        }

        private void UpdateFinishedState(int elapsed)
        {
            if (elapsed >= _endAnimationEndTick)
                FinishContinuousAttack();
        }

        private void CheckContinueInput()
        {
            if (_isNextAttackInputReceived)
                return;

            if (_playerInputManager == null)
                return;

            if (!_playerInputManager.GetPlayerInput(out var input))
                return;

            if (input.Buttons.GetPressed(_previousButtons).IsSet(_continueAttackButton))
                _isNextAttackInputReceived = true;

            _previousButtons = input.Buttons;
        }

        private void StartAttack(int attackIndex)
        {
            if (!IsValidAttackIndex(attackIndex))
            {
                EndAttack();
                return;
            }

            _currentAttackIndex = attackIndex;

            var attackData = _attackDatas[attackIndex];
            var startAnimationClip = attackData.StartAnimationClip;

            if (startAnimationClip == null)
            {
                EndAttack();
                return;
            }

            _state = AttackState.InputDisabled;

            _attackStartTick = Runner.Tick;

            // 攻撃Tick
            _damageStartTick = FrameToTick(startAnimationClip, attackData.DamageStartFrame);

            _damageEndTick = FrameToTick(startAnimationClip, attackData.DamageEndFrame);

            _nextInputStartTick = FrameToTick(startAnimationClip, attackData.NextInputStartFrame);

            // アニメーションTick
            _startAnimationEndTick = FrameToTick(startAnimationClip, GetEndFrame(startAnimationClip));

            // 剣のTick
            _shouldDrawSword = attackData.DrawSwordFrame >= 0;
            _shouldSheathSword = false;

            if (_shouldDrawSword)
            {
                _drawSwordTick = FrameToTick(startAnimationClip, attackData.DrawSwordFrame);
            }

            if (_animationClipPlayer)
                _animationClipPlayer.PlayClip(startAnimationClip);

            //2重攻撃Clear
            _alreadyHit.Clear();
        }

        private void EndAttack()
        {
            if (_currentAttackIndex < 0 ||
                !IsValidAttackIndex(_currentAttackIndex))
            {
                FinishContinuousAttack();
                return;
            }

            var attackData = _attackDatas[_currentAttackIndex];
            var endAnimationClip = attackData.EndAnimationClip;

            _state = AttackState.Finished;

            if (endAnimationClip == null)
            {
                FinishContinuousAttack();
                return;
            }

            _attackStartTick = Runner.Tick;

            _endAnimationEndTick = FrameToTick(endAnimationClip, GetEndFrame(endAnimationClip));

            //剣Tick
            _shouldDrawSword = false;
            _shouldSheathSword = attackData.SheathSwordFrame >= 0;

            if (_shouldSheathSword)
            {
                _sheathSwordTick = FrameToTick(endAnimationClip, attackData.SheathSwordFrame);
            }

            if (_animationClipPlayer)
                _animationClipPlayer.PlayClip(endAnimationClip);
        }
        protected void CastAndApplyHits()
        {
            var t = Parameter.Owner.transform;

            // BoxCast の原点と向き
            var origin = t.position + t.TransformVector(_boxLocalOffset);
            var dir = t.forward;
            var rot = t.rotation;

            // 掃引（NonAlloc で GC しない）
            int hitCount = Physics.BoxCastNonAlloc(
                origin,
                _boxHalfExtents,
                dir,
                _hitBuffer,
                rot,
                _boxCastDistance,
                _hitLayer,
                _triggerInteraction
            );

            for (int i = 0; i < hitCount; i++)
            {
                var hit = _hitBuffer[i];
                var col = hit.collider;
                if (col == null) continue;

                // 自分自身除外
                if (col.GetComponentInParent<NetworkObject>() == Parameter.Owner) continue;

                // 二度当たり防止
                if (_alreadyHit.Contains(col)) continue;
                _alreadyHit.Add(col);

                // ヒット位置が 0 のことがあるのでフォールバック
                var hitPos = hit.point;
                if (hitPos == Vector3.zero)
                    hitPos = origin + dir * Mathf.Max(0.1f, _boxCastDistance * 0.5f);

                OnHitEnemy(col, hitPos);
            }
            // バッファ初期化（念のため）
            Array.Clear(_hitBuffer, 0, hitCount);
        }

        private void OnHitEnemy(Collider hitInfo, Vector3 hitPosition)
        {
            if (hitInfo.GetComponentInParent<NetworkObject>() == Parameter.Owner) return;
            var damageable = hitInfo.GetComponentInParent<IDamageable>();
            if (damageable == null) return;

            var hitData = new HitData(
                HitActionType.Damage,
                _attackDatas[_currentAttackIndex].DamageAmount,
                Parameter.Owner.InputAuthority,
                damageable.OwnerPlayerRef);
            damageable.TakeHit(ref hitData);
            _buildGenerator?.UpdateBuild(BuildRouteType.AttackPower);

            //エフェクトの再生
            _effectSpawner.RequestPlayOneShotEffect(_hitEffect, hitInfo.ClosestPoint(hitInfo.bounds.ClosestPoint(hitPosition)), Quaternion.identity);
        }

        public override void SetPlayerComponent(GameObject player)
        {
            _animationClipPlayer =
                player.GetComponentInChildren<AnimationClipPlayer>();

            _buildGenerator =
                player.GetComponentInChildren<BuildGenerator>();

            _playerStatus =
                player.GetComponentInChildren<PlayerStatus>();

            _playerMovement =
                player.GetComponent<PlayerMovement>();

            _animator =
                player.GetComponentInChildren<Animator>();
        }

        private void FinishContinuousAttack()
        {
            _state = AttackState.Finished;

            if (_playerMovement != null)
            {
                _playerMovement.IgnoreMoveInput = false;
                _playerMovement.IgnoreEvasionInput = false;
            }

            RequestEndAbility();
        }

        private bool IsValidAttackIndex(int index)
        {
            if (_attackDatas == null)
                return false;

            if (index < 0 || index >= _attackDatas.Length)
                return false;

            return _attackDatas[index] != null;
        }

        private int FrameToTick(AnimationClip animationClip, int frame)
        {
            if (animationClip == null)
                return 0;

            float frameRate = animationClip.frameRate;

            if (frameRate <= 0f)
                return 0;

            float deltaTime =
                Runner != null ? Runner.DeltaTime : Time.fixedDeltaTime;

            if (deltaTime <= 0f)
                return 0;

            return Mathf.RoundToInt((frame / frameRate) / deltaTime);
        }

        private int GetEndFrame(AnimationClip clip)
        {
            if (clip == null)
                return 0;

            return Mathf.CeilToInt(clip.length * clip.frameRate);
        }
    }
}