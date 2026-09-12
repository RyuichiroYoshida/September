using System;
using System.Collections.Generic;
using DG.Tweening;
using Fusion;
using InGame.Common;
using InGame.Health;
using September.Common;
using September.InGame.Effect;
using Unity.Mathematics;
using UnityEngine;

namespace InGame.Player.Sarutobi
{
    public class UltKunai : NetworkBehaviour
    {
        [Header("基本設定")]
        [SerializeField] private PlayerButtons _throwButton = PlayerButtons.Attack;
        [SerializeField] private PlayerMovement _playerMovement;

        [Header("エイムアニメーション設定")]
        [SerializeField] private AnimationClipPlayer _animationClipPlayer;
        [SerializeField] private AnimationClip _idleClip;

        [Header("攻撃設定")]
        [SerializeField] private float _hitStartTime;
        [SerializeField] private float _hitEndTime;
        [SerializeField] private float _hitRadius;
        [SerializeField] private LayerMask _hitLayerMask;
        [SerializeField] private LayerMask _groundLayerMask;
        [SerializeField] private int _damage;

        [Header("エフェクト")]
        [SerializeField] private EffectType _kunaiEffect;
        [SerializeField] private Transform _aimEffect;
        [SerializeField] private Vector3 _aimEffectOffset = new(0f, 0.01f, 0f);

        private enum UltState
        {
            InActive,
            Aiming,
            Attacking,
        }

        [Networked, OnChangedRender(nameof(OnStateChanged))] private UltState State { get; set; }
        [Networked] public float RotationRatio { get; set; }

        private const float RotateDuration = 0.3f;

        // === ホストのみ利用 ===
        public event Action OnThrow;

        private Vector3 _thrownTargetPosition;

        private TickTimer _hitStartTimer;
        private TickTimer _hitEndTimer;

        private readonly Collider[] _hitBuffer = new Collider[10];
        private readonly List<Collider> _alreadyHits = new();
        private bool _isHitChecked;

        public void StartStance()
        {
            State = UltState.Aiming;

            _alreadyHits.Clear();
            _isHitChecked = false;

            _hitStartTimer = default;
            _hitEndTimer = default;
        }

        public override void FixedUpdateNetwork()
        {
            if (State == UltState.Aiming)
            {
                if (!GetInput(out PlayerInput input)) return;

                Vector3 throwTarget = GetThrowPos(input.CameraPosition, input.DesiredLookDirection);
                UpdatePlayerRotation(input.DesiredLookDirection);
                UpdateAim(input);

                if (input.Buttons.IsSet(_throwButton))
                {
                    Fire(throwTarget);
                    State = UltState.Attacking;
                }
            }

            if (State == UltState.Attacking && HasStateAuthority)
            {
                UpdateHit();
            }
        }

        private void OnStateChanged()
        {
            Debug.Log($"[{nameof(UltKunai)}] OnStateChanged: {State}");
            _aimEffect.gameObject.SetActive(State == UltState.Aiming);

            if (State == UltState.Aiming)
            {
                _animationClipPlayer.PlayClipLoop(_idleClip);
            }
        }

        private void Fire(Vector3 targetPosition)
        {
            EndLook();
            Throw(targetPosition);
            State = UltState.Attacking;
            HitboxDebugUtility.DrawWireSphere(targetPosition, _hitRadius, Color.red, 10f);
        }

        private void Throw(Vector3 targetPosition)
        {
            if (!HasStateAuthority) return;

            OnThrow?.Invoke();
            _thrownTargetPosition = targetPosition;
            _hitStartTimer = TickTimer.CreateFromSeconds(Runner, _hitStartTime);
            _hitEndTimer = TickTimer.CreateFromSeconds(Runner, _hitEndTime);
            StaticServiceLocator.Instance.Get<EffectSpawner>()
                .RequestPlayOneShotEffect(_kunaiEffect, targetPosition, quaternion.identity);
        }

        private void UpdateAim(PlayerInput input)
        {
            Vector3 pos = GetThrowPos(input.CameraPosition, input.DesiredLookDirection);
            Vector3 aimPosition = pos + _aimEffectOffset;
            _aimEffect.transform.position = aimPosition;
        }

        private Vector3 GetThrowPos(Vector3 cameraPosition, Vector3 lookDirection)
        {
            Ray ray = new(cameraPosition, lookDirection);
            Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, _groundLayerMask);
            return hit.point;
        }

        private void UpdateHit()
        {
            if (_hitStartTimer.Expired(Runner) && !_isHitChecked)
            {
                // 最低一回はヒット検出を行う
                HitCheck();
                _isHitChecked = true;
            }
            else if (_hitEndTimer.Expired(Runner) && !_hitStartTimer.Expired(Runner))
            {
                HitCheck();
            }

            if (_hitEndTimer.Expired(Runner))
            {
                State = UltState.InActive;
            }
        }

        private void HitCheck()
        {
            int count = Physics.OverlapSphereNonAlloc(_thrownTargetPosition, _hitRadius, _hitBuffer, _hitLayerMask);

            for (int i = 0; i < count; i++)
            {
                if (_alreadyHits.Contains(_hitBuffer[i])) continue;

                _alreadyHits.Add(_hitBuffer[i]);

                IDamageable damageable = _hitBuffer[i].GetComponentInParent<IDamageable>();
                if (damageable == null) continue;

                HitData hitData = new(HitActionType.RangedDamage, _damage, Object.InputAuthority, damageable.OwnerPlayerRef);
                damageable.TakeHit(ref hitData);
            }
        }

        private void UpdatePlayerRotation(Vector3 desiredLookDirection)
        {
            if (RotationRatio > 0f)
                _playerMovement.SetRotationDirection(desiredLookDirection);
        }

        public void StartLook()
        {
            DOTween.To(() => RotationRatio, v => RotationRatio = v, 1f, RotateDuration);
        }

        public void EndLook()
        {
            DOTween.To(() => RotationRatio, x => RotationRatio = x, 0f, RotateDuration);
        }
    }
}
