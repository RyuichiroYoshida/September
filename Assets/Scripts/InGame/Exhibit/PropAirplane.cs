using Cysharp.Threading.Tasks.Triggers;
using ExitGames.Client.Photon.StructWrapping;
using Fusion;
using InGame.Interact;
using InGame.Player;
using September.Common;
using September.InGame.Common;
using UnityEngine;

namespace InGame.Exhibit
{
    public class PropAirplane : InteractableBase
    {
        [Header("Ride")]
        [SerializeField] private Transform _getOffPoint;
        [Header("Move")]
        [SerializeField] private float _grav;
        [SerializeField] private float _jerk;
        [SerializeField] private float _maxForwardSpeed;
        [SerializeField] private float _takeOffSpeed;
        [SerializeField] private float _rotSpeedPitch;
        [SerializeField] private float _rotSpeedRoll;
        [SerializeField] private GameObject _wheelObj;
        [Header("Prop")]
        [SerializeField] private Transform _prop;
        [SerializeField] private float _propSpeedRate;

        private Rigidbody _rb;
        private CameraController _cameraController;
        private GameInput _gameInput;
        private PlayerRef _ownerPlayerRef;
        private PlayerManager _ownerPlayerManager;
        // move
        private bool _onGround;
        private bool IsGround => _onGround && _forwardSpeed <= _takeOffSpeed;
        private float _forwardSpeed;
        private Vector3 _velocity;
        
        [Networked] private float CurrentAccel { get; set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _cameraController = GetComponent<CameraController>();
            _cameraController.Init(true);

            _gameInput = new GameInput();
            _gameInput.Enable();
        }

        public override void FixedUpdateNetwork()
        {
            // apply grav
            if (IsGround) _velocity.y = 0;
            else _velocity.y -= _grav * Time.deltaTime;
            
            if (GetInput<PlayerInput>(out var input))
            {
                AddSpeed();
                Move(input.MoveDirection);
                // playerオブジェクトのpositionを固定する
                _ownerPlayerManager.transform.position = transform.position;
            }
            
            _rb.linearVelocity = _velocity;
            _onGround = false;
        }

        void AddSpeed()
        {
            CurrentAccel += _jerk * Runner.DeltaTime;
            _forwardSpeed = Mathf.Min(_forwardSpeed + CurrentAccel * Runner.DeltaTime, _maxForwardSpeed);
        }

        void Move(Vector2 moveDir)
        {
            if (IsGround)
            {
                Vector3 worldForward = transform.forward;
                worldForward.y = 0;
                _velocity = _forwardSpeed * worldForward.normalized;
            }
            else
            {
                Quaternion pitchRotation = Quaternion.AngleAxis(moveDir.y * _rotSpeedPitch * Runner.DeltaTime, transform.right);
                Quaternion rollRotation  = Quaternion.AngleAxis(moveDir.x * _rotSpeedRoll  * Runner.DeltaTime, -transform.forward);

                transform.rotation = pitchRotation * rollRotation * transform.rotation;
                
                _velocity = _forwardSpeed * transform.forward;
            }
        }

        void GetOn(PlayerRef ownerPlayerRef)
        {
            // 既に誰か乗っていたら乗れない
            if (!Runner.IsServer || _ownerPlayerRef != PlayerRef.None) return;
            
            // set input authority 
            _ownerPlayerRef = ownerPlayerRef;
            Object.AssignInputAuthority(_ownerPlayerRef);
            // camera の切り替え
            _cameraController.SetCameraPriority(15);
            // playerの状態切り替え
            _ownerPlayerManager = StaticServiceLocator.Instance.Get<InGameManager>().PlayerDataDic[_ownerPlayerRef].GetComponent<PlayerManager>();
            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.ForcedMovement);
            _ownerPlayerManager.RPC_SetGhostMode(true);
        }

        void GetOff()
        {
            if (!Runner.IsServer || _ownerPlayerRef == PlayerRef.None) return;
            
            // set input authority 
            _ownerPlayerRef = PlayerRef.None;
            Object.RemoveInputAuthority();
            // camera の切り替え
            _cameraController.SetCameraPriority(5);
            // playerの状態切り替え
            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.Normal);
            _ownerPlayerManager.RPC_SetGhostMode(false);
            // 降りる場所にセット
            _ownerPlayerManager.transform.position = _getOffPoint.position;
        }

        private void LateUpdate()
        {
            // camera 操作
            if (HasInputAuthority)
            {
                if (_gameInput.Player.Aim.triggered)
                {
                    _cameraController.CameraReset();
                }
            
                _cameraController.RotateCamera(_gameInput.Player.Look.ReadValue<Vector2>(), Time.deltaTime);
            }
            
            // rotate prop
            Vector3 euler = _prop.eulerAngles;
            euler.z += CurrentAccel * _propSpeedRate * Time.deltaTime;
            euler.z = euler.z % 360f < 0 ? euler.z % 360f + 360f : euler.z % 360f;
            _prop.eulerAngles = euler;
        }

        protected override bool OnValidateInteraction(IInteractableContext context, CharacterType charaType)
        {
            return _ownerPlayerRef == PlayerRef.None || _ownerPlayerRef == PlayerRef.FromEncoded(context.Interactor);
        }

        protected override void OnInteract(IInteractableContext context)
        {
            if (_ownerPlayerRef == PlayerRef.None) GetOn(PlayerRef.FromEncoded(context.Interactor));
            else if (_ownerPlayerRef == PlayerRef.FromEncoded(context.Interactor)) GetOff();
        }

        private void OnCollisionStay(Collision other)
        {
            if (_velocity.y > 0) return;
            if (other.contacts[0].thisCollider.gameObject == _wheelObj) _onGround = true;
        }
    }
}
