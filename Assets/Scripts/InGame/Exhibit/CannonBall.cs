using System;
using System.Collections.Generic;
using Cinemachine;
using Fusion;
using Fusion.Addons.Physics;
using InGame.Interact;
using September.Common;
using UnityEditor;
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

        [Header("参照")]
        [SerializeField] private NetworkObject _networkObject;
        [SerializeField] NetworkRigidbody3D _networkRigidbody;
        [SerializeField] private Transform _visual;
        [SerializeField] private Collider _visualCollider;
        [Header("設定")]
        [SerializeField] private float _launchForce = 20f;
        [SerializeField] private float _raycastDistance = 2.0f;
        [SerializeField] private float _resetIgnoreDuration = 1.5f;
        [SerializeField] private float _maxFlightDuration = 5.0f;

        [Networked]
        private BallState CurrentBallState { get; set; } = BallState.Idle;
        
        [Networked]
        private PlayerRef Owner { get; set; } = PlayerRef.None;
        
        private static string HandPositionPath => "Geometry/KyleRobot/ROOT/Hips/Spine1/Spine2/RightShoulder/RightUpperArm/RightArm/RightHand";
        private Transform _cachedHandTransform;
        private PlayerRef _cachedHandOwner = PlayerRef.None;
        private bool _readyToFire = false; 
        private bool _wasPressedLastFrame = false;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private float _launchedTime = -1f;

        private void Start()
        {
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
        }

        private void Update()
        {
            if (CurrentBallState == BallState.Launched)
            {
                float flightTime = Time.time - _launchedTime;
                if (flightTime >= _maxFlightDuration)
                {
                    Debug.Log("[CannonBall] Max flight time exceeded. Forcing reset.");
                    ResetCannonBall();
                    return;
                }

                if (flightTime < _resetIgnoreDuration)
                    return;
                Debug.DrawRay(transform.position, Vector3.down * _raycastDistance, Color.red);
                // Ray を真下に飛ばして地面との距離を確認
                var hits = Physics.RaycastAll(transform.position, Vector3.down, _raycastDistance);
                foreach (var hit in hits)
                {
                    if (hit.collider.CompareTag("Ground"))
                    {
                        ResetCannonBall();
                        break;
                    }
                }
            }
            else if (CurrentBallState == BallState.Cooldown)
            {
                Debug.Log($"クールダウン中: {Runner.SimulationTime - LastInteractTime} / {LastUsedCooldownTime}");
                // クールダウン中の処理（必要なら）
                if (Runner.SimulationTime - LastInteractTime >= LastUsedCooldownTime)
                {
                    CurrentBallState = BallState.Idle; // クールダウン完了
                    _visual.gameObject.SetActive(true);
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (CurrentBallState == BallState.InUse)
            {
                if (!Object.HasInputAuthority || !GetInput(out PlayerInput input)) return;
                bool isPressed = input.Buttons.IsSet(PlayerButtons.Interact);

                if (!_wasPressedLastFrame && isPressed && _readyToFire)
                {
                    RPC_Launch(Owner);
                    _readyToFire = false; // 再発射禁止（必要なら別条件で許可）
                }

                if (!_wasPressedLastFrame && !isPressed)
                {
                    _readyToFire = true; // 初回にボタンを離したときに発射準備完了とみなす
                }

                _wasPressedLastFrame = isPressed;
            }
        }
        
        public override void Render()
        {
            if (CurrentBallState == BallState.InUse)
            {
                if (Owner != _cachedHandOwner)
                {
                    _cachedHandOwner = Owner;
                    _cachedHandTransform = null;

                    if (Runner.TryGetPlayerObject(Owner, out var playerObj))
                    {
                        _cachedHandTransform = playerObj.transform.Find(HandPositionPath);
                    }
                }

                if (_cachedHandTransform)
                {
                    _visual.position = _cachedHandTransform.position;
                    _visual.rotation = _cachedHandTransform.rotation;
                }
            }
            else if (CurrentBallState == BallState.Launched || CurrentBallState == BallState.Cooldown)
            {
                // Visual を自分自身の位置に戻す
                _visual.position = transform.position;
                _visual.rotation = transform.rotation;
            }
        }


        protected override void OnInteract(IInteractableContext context)
        {
            Owner = PlayerRef.FromEncoded(context.Interactor);
            var player = Runner.GetPlayerObject(Owner);
            var hand = player.transform.Find(HandPositionPath);

            if (!hand)
            {
                Debug.LogWarning("ハンドが見つかりません");
                return;
            }

            // 物理と所有権設定
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            _networkObject.AssignInputAuthority(Owner);
            CurrentBallState = BallState.InUse;
            _readyToFire = false;
            _wasPressedLastFrame = true;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_Launch(PlayerRef playerRef)
        {
            if (!Runner.TryGetPlayerObject(playerRef, out var player)) return;

            var cam = player.GetComponentInChildren<CinemachineVirtualCamera>();
            if (!cam)
            {
                Debug.LogWarning("カメラが見つかりません");
                return;
            }

            var rb = GetComponent<Rigidbody>();
            if (!rb)
            {
                Debug.LogWarning("Rigidbodyがありません");
                return;
            }

            transform.position = _visual.position;
            transform.rotation = _visual.rotation;

            rb.isKinematic = false;
            rb.detectCollisions = true;

            _networkObject.RemoveInputAuthority();

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            rb.AddForce(cam.transform.forward * _launchForce, ForceMode.Impulse);

            CurrentBallState = BallState.Launched;
            _launchedTime = Runner.SimulationTime;
        }

        protected override bool OnValidateInteraction(IInteractableContext context, CharacterType charaType)
        {
            return CurrentBallState != BallState.InUse && CurrentBallState != BallState.Launched && CurrentBallState != BallState.Cooldown;
        }
        
        private void ResetCannonBall()
        {
            var rb = GetComponent<Rigidbody>();

            _networkRigidbody.Teleport(_initialPosition, _initialRotation);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;

            _visual.SetParent(transform);
            _visual.localPosition = Vector3.zero;
            _visual.localRotation = Quaternion.identity;

            CurrentBallState = BallState.Cooldown;
            _visual.gameObject.SetActive(false);
            
            // クールダウン処理を追加
            if (PlayerDatabase.Instance.PlayerDataDic.TryGet(Owner, out var playerData))
            {
                var charaType = playerData.CharacterType;
                float cooldown = _cooldownTimeDictionary.Dictionary.GetValueOrDefault(charaType, 0f);

                // InteractableBaseのNetworkedプロパティに代入
                LastInteractTime = Runner.SimulationTime;
                LastUsedCooldownTime = cooldown;
            }
        }

        protected override bool IsInCooldown()
        {
            return CurrentBallState == BallState.Cooldown;
        }
    }
}