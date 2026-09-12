using Fusion;
using Ingame.Tanihira;
using InGame.Health;
using September.Common;
using September.InGame.Common;
using September.InGame.Common.Stats;
using UnityEngine;
using PlayerInput = September.Common.PlayerInput;

namespace InGame.Player
{
    /// <summary>
    /// Playerのどこまでの機能を入れるかは未定
    /// </summary>
    public class PlayerManager : NetworkBehaviour, IAfterTick
    {
        [SerializeField] private PlayerInputManager _playerInputManager;
        [SerializeField] private PlayerRespawn _playerRespawn;
        [SerializeField] GameObject _colliderObj;
        [SerializeField] GameObject _meshObj;
        [SerializeField] private float _stunTime; // PlayerParameter に入れるべきか
        [SerializeField] private Vector3 _respawnPosition;
        [SerializeField] private GameObject _attackWeapon;
        [Header("ロックオン設定")]
        [SerializeField, Min(0f), Tooltip("ロックオン開始時に対象を検索する最大距離")]
        private float _lockOnSearchRadius = 20f;
        [SerializeField, Min(0f), Tooltip("ロックオンを維持できる対象との最大高低差")]
        private float _lockOnVerticalRange = 1.5f;
        [Header("ビルドシステム関連の参照")]
        [SerializeField] BuildGenerator _buildGenerator;
        [SerializeField] PlayerStatus _playerStatus;

        PlayerMovement _playerMovement;
        CameraController _cameraController;
        PlayerHealth _playerHealth;
        PlayerEffectController _playerEffectController;
        Rigidbody _rigidbody;
        private bool _shouldWarp = false;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private bool _isVaultingLastFrame = false;
        private Transform _rideTarget;
        private Vector3 _rideOffset;
        private bool _rideActive;

        // 乗車中のプレイヤー本体とカメラの追従を開始する。
        public void BeginRideView(Transform trolley, Vector3 offset)
        {
            if (_rideActive) EndRideView();
            _rideActive = true;
            _rideTarget = trolley;
            _rideOffset = offset;
            UpdateRidePose();
            if (HasInputAuthority && _cameraController != null)
                _cameraController.BeginRideView(trolley, offset);
        }

        public void EndRideView()
        {
            // 通常降車・途中終了の両方から呼ぶ。二重に呼ばれても復元は一度だけ行う。
            if (!_rideActive) return;
            _rideActive = false;
            _rideTarget = null;
            if (HasInputAuthority && _cameraController != null) _cameraController.EndRideView();
        }

        private void UpdateRidePose()
        {
            if (_rideTarget == null)
            {
                EndRideView();
                return;
            }

            // Rootを動かし、Colliderやプレイヤーに追従する各コンポーネントも台車へ合わせる。
            transform.SetPositionAndRotation(_rideTarget.position + _rideOffset, _rideTarget.rotation);
        }
        private RigidbodyConstraints _defaultConstraints;

        [Networked] public PlayerControlState CurrentPlayerControlState { get; private set; } = PlayerControlState.Normal;

        public void Start()
        {
            _playerRespawn.OnOutFieldEvent += () =>
            {
                IsMovable = false;
            };
            _playerRespawn.OnRevivalFieldEvent += () =>
            {
                IsMovable = true;
            };
            _playerRespawn.OnRevivalFieldEvent += () => Respawn();
        }

        public void SetWarpTarget(Vector3 targetPosition, Quaternion targetRotation)
        {
            if (!HasStateAuthority)
                return;

            _targetPosition = targetPosition;
            _targetRotation = targetRotation;
            _shouldWarp = true;
        }

        public bool IsLocalPlayer => HasInputAuthority;

        [Networked] private NetworkButtons PreviousButtons { get; set; }
        [Networked, HideInInspector] public NetworkBool IsStun { get; private set; }
        [Networked, HideInInspector] public NetworkBool IsMovable { get; private set; } = true;
        [Networked, HideInInspector] public NetworkBool IsLockOnActive { get; private set; }
        [Networked] private NetworkId LockOnTargetId { get; set; }
        [Networked] private TickTimer StunTickTimer { get; set; }

        public override void Spawned()
        {
            InitComponents();

            _respawnPosition = transform.position;
            _defaultConstraints = _rigidbody.constraints;
        }

        /// <summary> Player関連コンポーネントの初期化 </summary>
        void InitComponents()
        {
            _playerMovement = GetComponent<PlayerMovement>();
            _playerEffectController = GetComponentInChildren<PlayerEffectController>();
            _rigidbody = GetComponent<Rigidbody>();
            if (TryGetComponent(out CameraController cameraController))
            {
                _cameraController = cameraController;
                cameraController.Init(IsLocalPlayer);
            }

            if (TryGetComponent(out PlayerHealth health))
            {
                _playerHealth = health;
                health.OnDeath += OnDeath;
            }

#if UNITY_EDITOR
            if (_buildGenerator & _playerStatus)
                Debug.Log("ビルドシステムが正常に動きます");
            else
                Debug.LogWarning("ビルドに関する参照がないためビルドシステムが正常に動作しません\nプレハブを確認してください");
            // 後でパスを登録
#endif
        }

        protected virtual void LateUpdate()
        {
            if (_rideActive) UpdateRidePose();

            // Localでの処理にInputを送る
            if (HasInputAuthority)
            {
                if (GameInput.I.Player.Aim.triggered)
                {
                    _cameraController.CameraReset();
                }

                if (ShouldTrackLockOnTarget(out Transform target))
                    _cameraController.RotateCameraYawTowards(target.position, Time.deltaTime);
                else
                    _cameraController.RotateCamera(GameInput.I.Player.Look.ReadValue<Vector2>(), Time.deltaTime);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                if (StunTickTimer.Expired(Runner) && IsStun)
                {
                    Restart();
                }
            }

            // プレイヤーの入力の管理
            if (_playerInputManager != null && _playerInputManager.GetPlayerInput(out var input))
            {
                UpdateLockOn(input);

                if (!IsStun && IsMovable && CurrentPlayerControlState == PlayerControlState.Normal)
                {
                    // player movement に入力を与えて更新する_playerInputManager
                    _playerMovement.UpdateMovement(input.MoveDirection, input.Buttons.IsSet(PlayerButtons.Dash),
                        input.CameraYaw, input.Buttons.WasPressed(PreviousButtons, PlayerButtons.Jump), input.Buttons.WasPressed(PreviousButtons, PlayerButtons.Evasion), Runner.DeltaTime);
                }

                // ジップライン乗車中は台車に移動を任せ、それ以外は接地・落下・速度を更新する。
                if (!_rideActive)
                    _playerMovement.MoveTick(Runner.DeltaTime);

                if (input.Buttons.WasPressed(PreviousButtons, PlayerButtons.Warp))
                {
                    Respawn();
                }
            }
            else if (HasStateAuthority)
            {
                // Ground probing and gravity must also run while input is missing.
                // 入力がない場合も、ジップライン乗車中以外はホスト側で移動更新を継続する。
                if (!_rideActive)
                    _playerMovement.MoveTick(Runner.DeltaTime);
            }

            if (_shouldWarp)
            {
                transform.position = _targetPosition;
                transform.rotation = _targetRotation;
                _cameraController.CameraReset();
                _shouldWarp = false;
            }


        }

        /// <summary>
        /// ロックオン入力と対象の有効性を更新する
        /// </summary>
        private void UpdateLockOn(PlayerInput input)
        {
            // キャラクター固有のエイム能力に関係なく、入力中と搭乗中は解除する。
            if (input.Buttons.IsSet(PlayerButtons.Aim)
                || CurrentPlayerControlState != PlayerControlState.Normal)
            {
                DisableLockOn();
                return;
            }

            if (input.Buttons.WasPressed(PreviousButtons, PlayerButtons.LockOn)
                && !IsStun
                && IsMovable
                && CurrentPlayerControlState == PlayerControlState.Normal)
            {
                if (IsLockOnActive)
                    DisableLockOn();
                else
                    EnableLockOn();
            }

            if (IsLockOnActive
                && (!TryGetLockOnTarget(out Transform target) || IsOutsideLockOnVerticalRange(target)))
            {
                DisableLockOn();
            }
        }

        private bool ShouldTrackLockOnTarget(out Transform target)
        {
            target = null;
            return IsLockOnActive
                && !GameInput.I.Player.Aim.IsPressed()
                && !IsStun
                && IsMovable
                && CurrentPlayerControlState == PlayerControlState.Normal
                && !_playerMovement.IgnoreMoveInput
                && !_playerMovement.IsEvading
                && !_playerMovement.DoingVault
                && !_playerMovement.IsHookLocked
                && TryGetLockOnTarget(out target)
                && !IsOutsideLockOnVerticalRange(target);
        }

        private void EnableLockOn()
        {
            NetworkObject target = FindClosestLockOnTarget();
            if (!target)
                return;

            LockOnTargetId = target.Id;
            IsLockOnActive = true;
        }

        private void DisableLockOn()
        {
            IsLockOnActive = false;
            LockOnTargetId = default;
        }

        private bool TryGetLockOnTarget(out Transform target)
        {
            target = null;
            if (LockOnTargetId == default
                || Runner == null
                || !Runner.TryFindObject(LockOnTargetId, out NetworkObject targetObject)
                || !IsValidLockOnTarget(targetObject))
            {
                return false;
            }

            target = targetObject.transform;
            return true;
        }

        private NetworkObject FindClosestLockOnTarget()
        {
            if (!StaticServiceLocator.Instance.TryGet(out InGameManager inGameManager))
                return null;

            float closestSqrDistance = _lockOnSearchRadius * _lockOnSearchRadius;
            NetworkObject closestTarget = null;
            foreach (NetworkObject target in inGameManager.PlayerDataDic.Values)
            {
                if (!IsValidLockOnTarget(target)
                    || IsOutsideLockOnVerticalRange(target.transform))
                    continue;

                Vector3 targetOffset = target.transform.position - transform.position;
                targetOffset.y = 0f;
                float sqrDistance = targetOffset.sqrMagnitude;
                if (sqrDistance >= closestSqrDistance)
                    continue;

                closestSqrDistance = sqrDistance;
                closestTarget = target;
            }

            return closestTarget;
        }

        private bool IsOutsideLockOnVerticalRange(Transform target)
        {
            return Mathf.Abs(target.position.y - transform.position.y) > _lockOnVerticalRange;
        }

        private bool IsValidLockOnTarget(NetworkObject target)
        {
            if (!target || target == Object || !target.gameObject.activeInHierarchy)
                return false;

            return target.TryGetComponent(out PlayerManager targetPlayer) && !targetPlayer.IsStun;
        }

        public void AfterTick()
        {
            PreviousButtons = GetInput<PlayerInput>().GetValueOrDefault().Buttons;
        }

        /// <summary> 気絶が終わったとき </summary>
        void Restart()
        {
            _playerHealth.IsInvincible = false;
            IsStun = false;
            _playerEffectController.StopStunEffect();
            _buildGenerator?.UpdateBuild(BuildRouteType.StunResistance);
        }

        void OnDeath(HitData lastHitData)
        {
            _playerHealth.IsInvincible = true;
            // ビルドの減衰分を乗算
            StunTickTimer = TickTimer.CreateFromSeconds(Runner, _stunTime * (_playerStatus ? _playerStatus.StunDurationMultiply : 1));
            IsStun = true;
            _playerEffectController.PlayStunEffect();
        }

        public void SetControlState(PlayerControlState controlState)
        {
            CurrentPlayerControlState = controlState;

            // 入力が届かない Tick でも搭乗時のロックオンを持ち越さない。
            if (CurrentPlayerControlState != PlayerControlState.Normal)
                DisableLockOn();

            if (CurrentPlayerControlState == PlayerControlState.ForcedControl)
            {
                _playerMovement.Stop();
            }
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_SetWeaponVisible(bool visible)
        {
            if (_attackWeapon == null) return;

            _attackWeapon.SetActive(visible);
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_SetColliderActive(NetworkBool active)
        {
            _colliderObj.SetActive(active);
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_SetMeshActive(NetworkBool active)
        {
            _meshObj.SetActive(active);
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_SetUseGrav(NetworkBool active)
        {
            _rigidbody.useGravity = active;
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_SetPositionLock(NetworkBool isLocked)
        {
            _rigidbody.constraints = isLocked ?
                RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation :
                _defaultConstraints;
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_SetInvisible(NetworkBool active)
        {
            SetControlState(active ? PlayerControlState.ForcedControl : PlayerControlState.Normal);
            _colliderObj.SetActive(!active);
            _meshObj.SetActive(!active);
            _rigidbody.useGravity = !active;
            _rigidbody.constraints = active ?
                RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation :
                _defaultConstraints;
        }

        /// <summary> 非常用リスポーン </summary>
        void Respawn()
        {
            if (!HasStateAuthority) return;

            _playerMovement.TeleportImmediate(_respawnPosition);
            Debug.Log($"[PlayerRespawn] {Object.InputAuthority}: returned to initial spawn {_respawnPosition}", this);

            //タニヒラ用の処理を追記
            if (this.gameObject.TryGetComponent<FormationManager>(out FormationManager formationManager))
            {
                formationManager.WarpFriendNearPlayer(_respawnPosition, Quaternion.identity);
            }
        }

        /// <summary> スタンの残り時間を取得する </summary>
        public float GetRemainingStunTime => StunTickTimer.RemainingTime(Runner) ?? 0;

        public virtual bool GetPlayerInput(out PlayerInput input)
        {
            return GetInput(out input);
        }

        public enum PlayerControlState
        {
            Normal,
            InputLocked,
            ForcedControl
        }
    }
}
