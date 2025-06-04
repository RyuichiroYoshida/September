using Fusion;
using InGame.Health;
using September.Common;
using UnityEngine;

namespace InGame.Player
{
    /// <summary>
    /// Playerのどこまでの機能を入れるかは未定
    /// </summary>
    public class PlayerManager : NetworkBehaviour, IAfterTick
    {
        [SerializeField] PlayerParameter _playerParameter;
        [SerializeField] private float _stunTime; // PlayerParameter に入れるべきか
        
        PlayerMovement _playerMovement;
        PlayerCameraController _playerCameraController;
        PlayerHealth _playerHealth;
        GameInput _gameInput;
        TickTimer _stunTickTimer;
        
        public bool IsLocalPlayer => HasInputAuthority;
        public PlayerParameter PlayerParameter => _playerParameter;
        
        [Networked] private NetworkButtons PreviousButtons { get; set; }
        [Networked, HideInInspector] public NetworkBool IsStun { get; private set; }

        private void Start()
        {
            if (HasInputAuthority)
            {
                _gameInput = new GameInput();
                _gameInput.Enable();
                
                // カーソルを消す todo:ゲームロジックがやるべき
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

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

            if (TryGetComponent(out PlayerCameraController cameraController))
            {
                _playerCameraController = cameraController;
                cameraController.Init(IsLocalPlayer);
            }

            if (TryGetComponent(out PlayerHealth health))
            {
                _playerHealth = health;
                health.Init(_playerParameter.Health);
                health.OnDeath += OnDeath;
            }
        }

        private void LateUpdate()
        {
            // Localでの処理にInputを送る
            if (HasInputAuthority)
            {
                if (_gameInput.Player.Aim.triggered)
                {
                    _playerCameraController.CameraReset();
                }
            
                _playerCameraController.RotateCamera(_gameInput.Player.Look.ReadValue<Vector2>(), Time.deltaTime);
            }
        }

        public override void FixedUpdateNetwork()
        {
            // プレイヤーの入力の管理
            if (GetInput<PlayerInput>(out var input) && !IsStun)
            {
                // player movement に入力を与えて更新する
                _playerMovement.UpdateMovement(input.MoveDirection, input.Buttons.IsSet(PlayerButtons.Dash), 
                    input.CameraYaw, input.Buttons.WasPressed(PreviousButtons, PlayerButtons.Jump), Runner.DeltaTime);
            }

            if (HasStateAuthority)
            {
                if (_stunTickTimer.Expired(Runner) && IsStun)
                {
                    Restart();
                }
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

        /// <summary> スタンの経過時間を取得する </summary>
        public float GetRemainingStunTime => _stunTickTimer.RemainingTime(Runner) ?? 0;
    }
}
