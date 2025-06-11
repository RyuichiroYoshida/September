using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using InGame.Interact;
using September.Common;

namespace InGame.Interact
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class PlayerInteractionController : NetworkBehaviour
    {
        [SerializeField] private float _interactRadius = 2.5f;
        [SerializeField] private LayerMask _interactMask;
        [SerializeField] private Transform _interactOrigin;
        [SerializeField] private float _baseInteractTime = 1.0f;
        [SerializeField] private float _ogreInteractMultiplier = 1.0f;
        [SerializeField] private CharacterType _characterType = CharacterType.OkabeWright;

        private readonly Collider[] _hitBuffer = new Collider[16];

        private bool _isInteracting = false;
        private float _currentInteractTime = 0f;
        private float _requiredInteractTime = 1.0f;

        private InteractableBase _target;
        private NetworkObject _targetNetObj;

        public bool IsInteracting => _isInteracting;
        public float CurrentInteractTime => _currentInteractTime;

        private void Awake()
        {
            if (!_interactOrigin)
            {
                _interactOrigin = transform;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasInputAuthority) return;
            if (!GetInput(out PlayerInput input)) return;

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
                    }
                }
            }
            else
            {
                CancelInteraction();
            }
        }

        private void TryStartInteraction()
        {
            int count = Physics.OverlapSphereNonAlloc(_interactOrigin.position, _interactRadius, _hitBuffer, _interactMask);

            for (int i = 0; i < count; i++)
            {
                var col = _hitBuffer[i];
                var go = col.gameObject;
                var interactable = go.GetComponentInParent<InteractableBase>() 
                                   ?? go.GetComponent<InteractableBase>() 
                                   ?? go.GetComponentInChildren<InteractableBase>();

                var netObj = go.GetComponentInParent<NetworkObject>() 
                             ?? go.GetComponent<NetworkObject>() 
                             ?? go.GetComponentInChildren<NetworkObject>();

                if (interactable == null || netObj == null)
                    continue;

                float baseTime = interactable.RequiredInteractTimeDictionary.Dictionary
                    .GetValueOrDefault(_characterType, 0f);

                float multiplier = 1f;

                if (PlayerDatabase.Instance.PlayerDataDic.TryGet(Object.InputAuthority, out var playerData) && playerData.IsOgre)
                {
                    multiplier = _ogreInteractMultiplier;
                }

                _requiredInteractTime = baseTime * multiplier;
                _currentInteractTime = 0f;

                _target = interactable;
                _targetNetObj = netObj;
                _isInteracting = true;

                return;
            }
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
                _target?.Interact(context);
            }
            else
            {
                RPC_RequestInteract(context.Interactor, _targetNetObj, context.RequiredInteractTime);
            }

            _target = null;
            _targetNetObj = null;
        }

        private void CancelInteraction()
        {
            _isInteracting = false;
            _currentInteractTime = 0f;
            _target = null;
            _targetNetObj = null;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_RequestInteract(int interactor, NetworkObject target, float interactTime)
        {
            if (target != null && target.TryGetComponent(out InteractableBase interactable))
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
    }
}
