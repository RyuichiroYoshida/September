using Fusion;
using September.Common;
using UnityEngine;

namespace InGame.Player
{
    /// <summary>
    /// Playerのどこまでの機能を入れるかは未定
    /// </summary>
    public class PlayerManager : NetworkBehaviour
    {
        [SerializeField] PlayerStatus _playerStatus;
        
        PlayerMovement _playerMovement;
        PlayerCameraController _playerCameraController;
        PlayerHealth _playerHealth;
        GameInput _gameInput;
        
        public bool IsLocalPlayer => HasInputAuthority;

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
                movement.Init(_playerStatus.Stamina, _playerStatus.StaminaConsumption, _playerStatus.StaminaRegen);
            }

            if (TryGetComponent(out PlayerCameraController cameraController))
            {
                _playerCameraController = cameraController;
                cameraController.Init(IsLocalPlayer);
            }

            if (TryGetComponent(out PlayerHealth health))
            {
                _playerHealth = health;
                health.Init(_playerStatus.Health);
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
            if (GetInput<PlayerInput>(out var input))
            {
                _playerMovement.Move(input.MoveDirection, input.Buttons.IsSet(PlayerButtons.Dash), input.CameraYaw, Runner.DeltaTime);
            }
        }
    }
}
