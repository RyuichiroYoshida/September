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

        private readonly Collider[] _hitBuffer = new Collider[32];
        private InteractableBase _focusedInteractable;
        private GameObject _focusedObj;

        private bool _isInteracting = false;
        private float _currentInteractTime = 0f;
        private float _requiredInteractTime = 1.0f;

        public bool IsInteracting => _isInteracting;
        public float CurrentInteractTime => _currentInteractTime;

        private void Awake()
        {
            if (!_interactOrigin)
                _interactOrigin = transform;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasInputAuthority) return;
            if (!GetInput(out PlayerInput input)) return;

            UpdateFocusedInteractable();

            bool isHolding = input.Buttons.IsSet(PlayerButtons.Interact);

            if (isHolding)
            {
                if (!_isInteracting)
                    TryStartInteraction();

                if (_isInteracting)
                {
                    _currentInteractTime += Runner.DeltaTime;
                    UIController.I.SetInteractProgress(Mathf.Clamp01(_currentInteractTime / _requiredInteractTime));
                    if (_currentInteractTime >= _requiredInteractTime)
                    {
                        CompleteInteraction();
                        UIController.I.ShowInteractUI(false);
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
            _focusedInteractable = null;
            _focusedObj = null;

            int count = Physics.OverlapSphereNonAlloc(_interactOrigin.position, _interactRadius, _hitBuffer, _interactMask);
            float closestDistanceSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var col = _hitBuffer[i];
                var go = col.gameObject;
                var interactable = go.GetComponentInParent<InteractableBase>() 
                                   ?? go.GetComponent<InteractableBase>() 
                                   ?? go.GetComponentInChildren<InteractableBase>();

                if (!interactable)
                    continue;
                
                var context = new InteractableContext
                {
                    Interactor = Object.InputAuthority.RawEncoded,
                    WorldPosition = _interactOrigin.position,
                    RequiredInteractTime = GetRequireInteractTime()
                };
                if (!interactable.ValidateInteraction(context))
                    continue;

                Vector3 direction = (interactable.transform.position - _interactOrigin.position).normalized;
                float angle = Vector3.Angle(_interactOrigin.forward, direction);

                if (angle > _interactAngle)
                    continue;

                float distanceSqr = (interactable.transform.position - _interactOrigin.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    _focusedInteractable = interactable;
                    _focusedObj = interactable.gameObject;
                }
            }
            UIController.I.ShowInteractUI(_focusedObj, _focusedObj?.gameObject);
        }

        private void TryStartInteraction()
        {
            if (!_focusedInteractable || !_focusedObj)
                return;

            _requiredInteractTime = GetRequireInteractTime();
            _currentInteractTime = 0f;
            _isInteracting = true;
        }

        private float GetRequireInteractTime()
        {
            if (!_focusedInteractable)
                return _baseInteractTime;
            float baseTime = _focusedInteractable.RequiredInteractTimeDictionary.Dictionary
                .GetValueOrDefault(_characterType, _baseInteractTime);

            float multiplier = 1f;
            if (PlayerDatabase.Instance.PlayerDataDic.TryGet(Object.InputAuthority, out var playerData) && playerData.IsOgre)
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
                _focusedInteractable?.Interact(context);
            }
            else
            {
                RPC_RequestInteract(context.Interactor, _focusedObj.GetComponent<NetworkObject>(), context.RequiredInteractTime);
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
            if (_focusedInteractable != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(_focusedInteractable.transform.position, 0.2f);
            }
        }
#endif

    }
}
