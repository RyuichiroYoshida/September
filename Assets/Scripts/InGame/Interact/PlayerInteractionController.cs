using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using September.Common;
using September.InGame.UI;

namespace InGame.Interact
{
    [DisallowMultipleComponent]
    public class PlayerInteractionController : NetworkBehaviour
    {
        [SerializeField] private float _interactRadius = 2.5f;
        [SerializeField] private LayerMask _interactMask;
        [SerializeField, Range(0f, 180f)] private float _interactAngle = 90f; // 前方180度
        [SerializeField] private Transform _interactOrigin;
        [SerializeField] private float _baseInteractTime = 1.0f;
        [SerializeField] private float _ogreInteractMultiplier = 1.0f;
        [SerializeField] private CharacterType _characterType = CharacterType.OkabeWright;
        [SerializeField] private float _interactResponseTimeout = 3f;
        [SerializeField] private float _interactAngleBuffer = 10f; // 角度に+10°
        [SerializeField] private float _interactRadiusBuffer = 0.3f; // 距離に+0.3m
        
        private bool _isWaitingForResponse = false;
        private float _interactWaitTimer = 0f;
        private readonly Collider[] _hitBuffer = new Collider[32];
        private InteractableBase _focusedObj;
        private bool _isInteracting = false;
        private float _currentInteractTime = 0f;
        private float _requiredInteractTime = 1.0f;

        private void Awake()
        {
            if (!_interactOrigin)
                _interactOrigin = transform;
        }
        
        private void Update()
        {
            if (!HasInputAuthority) return;

            // ローカルでインタラクト対象を毎フレーム検出（カメラ向きで変化するため）
            UpdateFocusedInteractable();

            if (_isInteracting)
            {
                UIController.I.SetInteractProgress(Mathf.Clamp01(_currentInteractTime / _requiredInteractTime));
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasInputAuthority) return;
            if (!GetInput(out PlayerInput input)) return;

            // Fusionのシミュレーション内でのみ行う処理
            if (_isWaitingForResponse)
            {
                _interactWaitTimer += Runner.DeltaTime;
                if (_interactWaitTimer >= _interactResponseTimeout)
                {
                    Debug.LogWarning("インタラクト応答タイムアウト: ロック解除");
                    _isWaitingForResponse = false;
                    _interactWaitTimer = 0f;
                }
                return;
            }

            bool isHolding = input.Buttons.IsSet(PlayerButtons.Interact);

            if (isHolding)
            {
                if (!_isInteracting)
                    TryStartInteraction();

                if (_isInteracting)
                {
                    _currentInteractTime += Runner.DeltaTime;
                    if (_currentInteractTime >= _requiredInteractTime)
                    {
                        CompleteInteraction();
                        UIController.I.ShowInteractUI(false); // 終了時に消すだけならここでもOK
                    }
                }
            }
            else
            {
                CancelInteraction();
            }
        }

        private void UpdateFocusedInteractable()
        {
            // 現在の focusedObj がまだ有効な範囲内かチェック
            if (_focusedObj)
            {
                if (!IsInInteractRange(_focusedObj.transform.position, InteractRangeCheckMode.Buffered))
                {
                    _focusedObj = null;
                }
            }

            // より近い候補があれば差し替え
            int count = Physics.OverlapSphereNonAlloc(_interactOrigin.position, _interactRadius, _hitBuffer,
                _interactMask);
            float closestDistanceSqr = _focusedObj? (_focusedObj.transform.position - _interactOrigin.position).sqrMagnitude
                : float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                GameObject go = _hitBuffer[i].gameObject;
                var interactable = go.GetComponentInParent<InteractableBase>()
                                   ?? go.GetComponent<InteractableBase>()
                                   ?? go.GetComponentInChildren<InteractableBase>();
                if (interactable == null) continue;

                Vector3 targetPos = interactable.transform.position;
                if (!IsInInteractRange(targetPos)) continue;

                float distanceSqr = (targetPos - _interactOrigin.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    _focusedObj = interactable;
                }
            }

            if (_focusedObj)
            {
                UIController.I.ShowInteractUI(!_focusedObj.IsInCooldown(), _focusedObj?.gameObject);
            }
            else
            {
                UIController.I.ShowInteractUI(false, _focusedObj?.gameObject);
            }
            //if (Runner.IsClient) Debug.Log(_focusedObj is not null);
        }

        /// <summary>
        /// 指定されたワールド座標が、インタラクトの有効範囲内（前方角度・距離）にあるかチェック
        /// </summary>
        private enum InteractRangeCheckMode
        {
            Strict, // 通常判定
            Buffered // バッファ許容
        }

        private bool IsInInteractRange(Vector3 targetPosition, InteractRangeCheckMode mode = InteractRangeCheckMode.Strict)
        {
            Vector3 toTarget = targetPosition - _interactOrigin.position;
            float radius = mode == InteractRangeCheckMode.Strict
                ? _interactRadius
                : _interactRadius + _interactRadiusBuffer;

            float angleLimit = mode == InteractRangeCheckMode.Strict
                ? _interactAngle
                : _interactAngle + _interactAngleBuffer;

            return toTarget.sqrMagnitude <= radius * radius;
            if (toTarget.sqrMagnitude > radius * radius)
                return false;

            float angle = Vector3.Angle(_interactOrigin.forward, toTarget);
            return angle <= angleLimit;
        }

        private void TryStartInteraction()
        {
            if (!_focusedObj || _focusedObj.IsInCooldown()) return;
            
            _requiredInteractTime = GetRequireInteractTime();
            _currentInteractTime = 0f;
            _isInteracting = true;
        }

        private float GetRequireInteractTime()
        {
            if (!_focusedObj)
                return _baseInteractTime;
            float baseTime = _focusedObj.RequiredInteractTimeDictionary.Dictionary
                .GetValueOrDefault(_characterType, _baseInteractTime);

            float multiplier = 1f;
            if (PlayerDatabase.Instance.PlayerDataDic.TryGet(Object.InputAuthority, out var playerData) &&
                playerData.IsOgre)
                multiplier = _ogreInteractMultiplier;

            return baseTime * multiplier;
        }

        private void CompleteInteraction()
        {
            _isInteracting = false;

            var context = new InteractableContext
            {
                Interactor = Object.InputAuthority.RawEncoded,
                WorldPosition = _interactOrigin.position,
                RequiredInteractTime = _requiredInteractTime
            };

            if (HasStateAuthority)
            {
                _focusedObj?.Interact(context);
            }
            else
            {
                if (!_focusedObj)
                {
                    Debug.LogWarning("[Interact] _focusedObj is null");
                    return;
                }

                var netObj = _focusedObj.GetComponent<NetworkObject>();
                if (!netObj)
                {
                    Debug.LogWarning($"[Interact] {_focusedObj.name} に NetworkObject が存在しません");
                    return;
                }

                // 応答待ちモードに入る
                _isWaitingForResponse = true;
                _interactWaitTimer = 0f;

                Debug.Log($"[Client] RPC_RequestInteract 送信: {context.Interactor} -> {_focusedObj.name} NetObj is null? {!netObj}");
                RPC_RequestInteract(context.Interactor, netObj, context.RequiredInteractTime);
            }
        }

        private void CancelInteraction()
        {
            _isInteracting = false;
            _currentInteractTime = 0f;
            UIController.I.SetInteractProgress(0f);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_RequestInteract(int interactor, NetworkObject target, float interactTime)
        {
            Debug.Log($"[PlayerInteractionController] <UNK>: {interactor} -> {target.name}");
            if (target && target.TryGetComponent(out InteractableBase interactable))
            {
                var context = new InteractableContext
                {
                    Interactor = interactor,
                    WorldPosition = transform.position,
                    RequiredInteractTime = interactTime
                };

                interactable.Interact(context);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_interactOrigin == null)
                _interactOrigin = transform;

            // Sphere範囲表示
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_interactOrigin.position, _interactRadius);

            // 前方角度表示
            Vector3 forward = _interactOrigin.forward;

            int segments = 30;
            float step = _interactAngle / segments;

            Gizmos.color = Color.cyan;

            for (int i = -segments; i <= segments; i++)
            {
                float angle = i * step;
                Quaternion rot = Quaternion.AngleAxis(angle, Vector3.up);
                Vector3 dir = rot * forward;
                Gizmos.DrawLine(_interactOrigin.position, _interactOrigin.position + dir * _interactRadius);
            }

            // 現在の選択対象を表示
            if (_focusedObj)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(_focusedObj.transform.position, 0.2f);
            }
        }
#endif
    }
}