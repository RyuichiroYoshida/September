using System;
using System.Collections.Generic;
using Cinemachine;
using Fusion;
using Fusion.Addons.Physics;
using InGame.Interact;
using September.Common;
using UnityEngine;

namespace InGame.Exhibit
{
    public class CannonBall : InteractableBase
    {
        private enum BallState
        {
            Idle,
            InUse,
            Launched,
            Cooldown,
        }

        private static string HandPath => "Geometry/KyleRobot/ROOT/Hips/Spine1/Spine2/RightShoulder/RightUpperArm/RightArm/RightHand";

        [Header("参照")]
        [SerializeField] private NetworkObject _networkObject;
        [SerializeField] private NetworkRigidbody3D _networkRigidbody;
        [SerializeField] private Transform _visual;
        [SerializeField] private Collider _visualCollider;

        [Header("設定")]
        [SerializeField] private float _launchForce = 20f;
        [SerializeField] private float _raycastDistance = 2.0f;
        [SerializeField] private float _resetIgnoreDuration = 1.5f;
        [SerializeField] private float _maxFlightDuration = 5.0f;

        [Networked] private BallState CurrentState { get; set; }
        [Networked] private PlayerRef Owner { get; set; } = PlayerRef.None;

        private Transform _cachedHandTransform;
        private bool _readyToFire = false;
        private bool _wasPressedLastFrame = false;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private float _launchedTime = -1f;
        private bool _isSpawned = false;

        private void Start()
        {
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
        }

        public override void Spawned()
        {
            _isSpawned = true;
        }

        private void Update()
        {
            if (!_isSpawned) return;
            switch (CurrentState)
            {
                case BallState.InUse:
                    UpdateHeldVisual();
                    break;
                case BallState.Launched:
                    CheckGroundAndTimeout();
                    break;
                case BallState.Cooldown:
                    CheckCooldownComplete();
                    break;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (CurrentState == BallState.InUse && Object.HasInputAuthority && GetInput(out PlayerInput input))
            {
                bool isPressed = input.Buttons.IsSet(PlayerButtons.Interact);

                if (!_wasPressedLastFrame && isPressed && _readyToFire)
                {
                    RPC_Launch(Owner);
                    _readyToFire = false;
                }

                if (!_wasPressedLastFrame && !isPressed)
                {
                    _readyToFire = true;
                }

                _wasPressedLastFrame = isPressed;
            }
        }

        protected override void OnInteract(IInteractableContext context)
        {
            if (!Object.HasStateAuthority) return;

            Owner = PlayerRef.FromEncoded(context.Interactor);
            CurrentState = BallState.InUse;
            _readyToFire = false;
            _wasPressedLastFrame = true;

            _networkObject.AssignInputAuthority(Owner);
            RPC_UpdateVisualState(CurrentState, Owner);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_UpdateVisualState(BallState state, PlayerRef owner)
        {
            CurrentState = state;
            Owner = owner;

            Transform hand = null;
            if (Runner.TryGetPlayerObject(owner, out var player))
            {
                hand = player.transform.Find(HandPath);
            }

            switch (state)
            {
                case BallState.InUse:
                    SetVisualTransform(hand);
                    SetPhysicsState(true);
                    break;
                case BallState.Launched:
                case BallState.Cooldown:
                    SetVisualTransform(transform);
                    SetPhysicsState(false);
                    break;
                case BallState.Idle:
                    _visual.gameObject.SetActive(true);
                    break;
            }

            _cachedHandTransform = hand;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_Launch(PlayerRef playerRef)
        {
            if (!Runner.TryGetPlayerObject(playerRef, out var player)) return;

            var cam = player.GetComponentInChildren<CinemachineVirtualCamera>();
            if (!cam) return;

            transform.SetPositionAndRotation(_visual.position, _visual.rotation);

            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.detectCollisions = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce(cam.transform.forward * _launchForce, ForceMode.Impulse);

            _networkObject.RemoveInputAuthority();

            _launchedTime = Runner.SimulationTime;
            RPC_UpdateVisualState(BallState.Launched, PlayerRef.None);
        }

        private void CheckCooldownComplete()
        {
            if (Runner.SimulationTime - LastInteractTime >= LastUsedCooldownTime)
            {
                CurrentState = BallState.Idle;
                _visual.gameObject.SetActive(true);
            }
        }

        private void CheckGroundAndTimeout()
        {
            float elapsed = Runner.SimulationTime - _launchedTime;
            if (elapsed >= _maxFlightDuration ||
                (elapsed >= _resetIgnoreDuration && IsGrounded()))
            {
                ResetCannonBall();
            }
        }

        private bool IsGrounded()
        {
            var hits = Physics.RaycastAll(transform.position, Vector3.down, _raycastDistance);
            foreach (var hit in hits)
            {
                if (hit.collider.CompareTag("Ground")) return true;
            }
            return false;
        }

        private void UpdateHeldVisual()
        {
            if (_cachedHandTransform)
            {
                _visual.position = _cachedHandTransform.position;
                _visual.rotation = _cachedHandTransform.rotation;
            }
        }

        private void ResetCannonBall()
        {
            var rb = GetComponent<Rigidbody>();
            _networkRigidbody.Teleport(_initialPosition, _initialRotation);
            rb.isKinematic = true;

            CurrentState = BallState.Cooldown;
            _visual.gameObject.SetActive(false);

            if (PlayerDatabase.Instance.PlayerDataDic.TryGet(Owner, out var data))
            {
                LastInteractTime = Runner.SimulationTime;
                LastUsedCooldownTime = _cooldownTimeDictionary.Dictionary.GetValueOrDefault(data.CharacterType, 0f);
            }
        }

        private void SetVisualTransform(Transform parent)
        {
            _visual.SetParent(parent);
            Debug.Log($"クライアント状態　{CurrentState} のときの親: {parent?.name ?? "null"}");
            _visual.localPosition = Vector3.zero;
            _visual.localRotation = Quaternion.identity;
        }

        private void SetPhysicsState(bool isKinematic)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb == null) return;
            rb.isKinematic = isKinematic;
            rb.detectCollisions = !isKinematic;
        }

        protected override bool OnValidateInteraction(IInteractableContext context, CharacterType charaType)
        {
            return CurrentState == BallState.Idle;
        }

        protected override bool IsInCooldown()
        {
            return CurrentState == BallState.Cooldown;
        }
    }
}
