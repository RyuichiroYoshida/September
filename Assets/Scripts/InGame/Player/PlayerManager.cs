using System;
using Fusion;
using InGame.Common;
using InGame.Health;
using September.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = September.Common.PlayerInput;

namespace InGame.Player
{
    /// <summary>
    /// Playerのどこまでの機能を入れるかは未定
    /// </summary>
    public class PlayerManager : NetworkBehaviour, IAfterTick
    {
        [SerializeField] PlayerParameter _playerParameter;
        [SerializeField] GameObject _colliderObj;
        [SerializeField] GameObject _meshObj;
        [SerializeField] private float _stunTime; // PlayerParameter に入れるべきか
        
        PlayerMovement _playerMovement;
        CameraController _cameraController;
        PlayerHealth _playerHealth;
        PlayerControlState _playerControlState = PlayerControlState.Normal;
        TickTimer _stunTickTimer;

        private bool _shouldWarp = false;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;

        public void SetWarpTarget(Vector3 targetPosition,Quaternion targetRotation)
        {
            if(!HasStateAuthority)
                return;
            
            _targetPosition = targetPosition;
            _targetRotation = targetRotation;
            _shouldWarp = true;
        }
        
        public bool IsLocalPlayer => HasInputAuthority;
        public PlayerParameter PlayerParameter => _playerParameter;
        
        [Networked] private NetworkButtons PreviousButtons { get; set; }
        [Networked, HideInInspector] public NetworkBool IsStun { get; private set; }

        public override void Spawned()
        {
            InitComponents();
        }

        /// <summary> Player関連コンポーネントの初期化 </summary>
        void InitComponents()
        {
            if (TryGetComponent(out PlayerMovement movement))
            {
                _playerMovement = movement;
                movement.Init(_playerParameter.Stamina, _playerParameter.StaminaConsumption, _playerParameter.StaminaRegen);
            }

            if (TryGetComponent(out CameraController cameraController))
            {
                _cameraController = cameraController;
                cameraController.Init(IsLocalPlayer);
            }

            if (TryGetComponent(out PlayerHealth health))
            {
                _playerHealth = health;
                health.Init(_playerParameter.Health);
                health.OnDeath += OnDeath;
            }

            if (TryGetComponent(out PlayerAnimBase playerAnimBase))
            {
                playerAnimBase.Init(this);
            }
        }

        private void LateUpdate()
        {
            // Localでの処理にInputを送る
            if (HasInputAuthority)
            {
                if (GameInput.I.Player.Aim.triggered)
                {
                    _cameraController.CameraReset();
                }
                
                _cameraController.RotateCamera(GameInput.I.Player.Look.ReadValue<Vector2>(), Time.deltaTime);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                if (_stunTickTimer.Expired(Runner) && IsStun)
                {
                    Restart();
                }
            }
            
            // プレイヤーの入力の管理
            if (GetInput<PlayerInput>(out var input) && !IsStun && _playerControlState == PlayerControlState.Normal)
            {
                // player movement に入力を与えて更新する
                _playerMovement.UpdateMovement(input.MoveDirection, input.Buttons.IsSet(PlayerButtons.Dash), 
                    input.CameraYaw, input.Buttons.WasPressed(PreviousButtons, PlayerButtons.Jump), Runner.DeltaTime);
                
                if (input.Buttons.WasPressed(PreviousButtons, PlayerButtons.Jump)) _playerMovement.AddForce(Vector3.up * 10);
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

        /// <summary> 気絶が終わったとき </summary>
        void Restart()
        {
            IsStun = false;
            _playerHealth.IsInvincible = false;
        }

        void OnDeath(HitData lastHitData)
        {
            IsStun = true;
            _stunTickTimer = TickTimer.CreateFromSeconds(Runner, _stunTime);
            _playerHealth.IsInvincible = true;
        }

        public void SetControlState(PlayerControlState controlState)
        {
            _playerControlState = controlState;

            if (_playerControlState == PlayerControlState.ForcedControl)
            {
                _playerMovement.Stop();
            }
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

        /// <summary> スタンの経過時間を取得する </summary>
        public float GetRemainingStunTime => _stunTickTimer.RemainingTime(Runner) ?? 0;
        
        public enum PlayerControlState
        {
            Normal,
            InputLocked,
            ForcedControl
        }

        [SerializeField] private AnimationClip _clip;
        AnimationClipPlayer _clipPlayer;

        private void Start()
        {
            _clipPlayer = GetComponent<AnimationClipPlayer>();
        }

        private void Update()
        {
            // if (Input.GetKeyDown(KeyCode.E))
            // {
            //     _clipPlayer.PlayClip(_clip);
            // }
        }
    }
}
