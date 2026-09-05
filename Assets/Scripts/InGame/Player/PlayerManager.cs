using Fusion;
using Ingame.Tanihira;
using InGame.Health;
using September.Common;
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
        [Networked] private TickTimer StunTickTimer { get; set; }

        /// <summary> このプレイヤーのカメラ前方。ホスト・他クライアントからも参照できる </summary>
        [Networked, HideInInspector] public Vector3 LookDirection { get; private set; }
        /// <summary> このプレイヤーのカメラのヨー角 (0 <= yaw < 360) </summary>
        [Networked, HideInInspector] public float LookYaw { get; private set; }
        /// <summary> このプレイヤーのカメラ位置 </summary>
        [Networked, HideInInspector] public Vector3 LookOrigin { get; private set; }

        public override void Spawned()
        {
            InitComponents();

            _respawnPosition = transform.position;
            _defaultConstraints = _rigidbody.constraints;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_cameraController != null)
            {
                StaticServiceLocator.Instance.Unregister<ILookInputReceiver>(_cameraController);
            }
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

                if (IsLocalPlayer)
                {
                    // InputProvider が入力収集の直前に視点入力を適用できるよう、ローカルのカメラリグを公開する
                    StaticServiceLocator.Instance.Register<ILookInputReceiver>(cameraController);
                }
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
            // if (_animationClipPlayer)
            // {
            //     var maxSpeed = _playerMovement.DashMoveSpeed;
            //     var walkSpeed = _playerMovement.WalkSpeed;
            //     var moveSpeed = _playerMovement._moveVelocity.magnitude;
            //     var weight = 0f;
            //     if (moveSpeed <= walkSpeed)
            //     {
            //         // 0..Walk -> 0..1
            //         weight = Mathf.InverseLerp(0f, walkSpeed, moveSpeed);
            //     }
            //     else
            //     {
            //         // Walk..Max -> 1..2
            //         weight = Mathf.InverseLerp(walkSpeed, maxSpeed, moveSpeed) + 1f;
            //     }
            //     
            //     _animationClipPlayer.SetLocoWeight(weight);
            //     
            //     switch ()
            //     {
            //         case false when _playerMovement.DoingVault:
            //             _animationClipPlayer.SetTopPriorityClip(true);
            //             break;
            //         case true when !_playerMovement.DoingVault:
            //             _animationClipPlayer.SetTopPriorityClip(false);
            //             break;
            //     }
            // }

            // Localでの処理にInputを送る
            if (HasInputAuthority)
            {
                if (GameInput.I.Player.Aim.triggered)
                {
                    _cameraController.CameraReset();
                }

                // 同一フレームで InputProvider.OnInput が先に回していれば、ここでは何もしない
                _cameraController.TryApplyLookInput(GameInput.I.Player.Look.ReadValue<Vector2>(), Time.deltaTime);
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
                RecordLookState(input);

                if (!IsStun && IsMovable && CurrentPlayerControlState == PlayerControlState.Normal)
                {
                    // player movement に入力を与えて更新する_playerInputManager
                    _playerMovement.UpdateMovement(input.MoveDirection, input.Buttons.IsSet(PlayerButtons.Dash),
                        input.CameraYaw, input.Buttons.WasPressed(PreviousButtons, PlayerButtons.Jump), input.Buttons.WasPressed(PreviousButtons, PlayerButtons.Evasion), Runner.DeltaTime);
                }

                _playerMovement.MoveTick(Runner.DeltaTime);

                if (input.Buttons.WasPressed(PreviousButtons, PlayerButtons.Warp))
                {
                    Respawn();
                }
            }

            if (_shouldWarp)
            {
                transform.position = _targetPosition;
                transform.rotation = _targetRotation;
                _cameraController.CameraReset();
                _shouldWarp = false;
            }


        }

        public void AfterTick()
        {
            PreviousButtons = GetInput<PlayerInput>().GetValueOrDefault().Buttons;
        }

        /// <summary> 入力に載ってきたカメラ姿勢を Networked に写し、どこからでも各プレイヤーの向きを取れるようにする </summary>
        private void RecordLookState(in PlayerInput input)
        {
            LookDirection = input.DesiredLookDirection;
            LookYaw = input.CameraYaw;
            LookOrigin = input.CameraPosition;
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
