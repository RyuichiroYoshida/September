using Fusion;
using InGame.Interact;
using InGame.Player;
using September.Common;
using September.InGame.Common;
using UnityEngine;

namespace InGame.Exhibit
{
    public class PterodactylObject : InteractableBase
    {
        [Header("Flight Settings")] 
        [SerializeField] private Transform _getOffPoint;

        [Header("Movement Settings")] 
        [SerializeField] private float _moveSpeed;
        private Rigidbody _rigidbody;
        private GameInput _gameInput;
        
        [Header("Camera Settings")]
        private CameraController _cameraController;

        private Animator _animator;
        private PlayerRef _ownerPlayerRef;
        private PlayerManager _ownerPlayerManager;

        #region AnimationHash

        private static readonly int _flyStateBlend = Animator.StringToHash("FlyStateBlend");

        #endregion
        
       private void Awake()
        {
            _cameraController = GetComponent<CameraController>();
            _animator = GetComponent<Animator>();
            if(_animator is null)
                Debug.LogError("Animator is null");
            
            _rigidbody = GetComponent<Rigidbody>();
            _cameraController.Init(true);

            _gameInput = new GameInput();
            _gameInput.Enable();
        }

        public override void FixedUpdateNetwork()
        {
            if (GetInput<PlayerInput>(out var input))
            {
                Move(input.MoveDirection);
            }
        }

        private void LateUpdate()
        {
            if (HasInputAuthority)
            {
                if (_gameInput.Player.Aim.triggered)
                {
                    _cameraController.CameraReset();
                }
                _cameraController.RotateCamera(_gameInput.Player.Look.ReadValue<Vector2>(), Time.deltaTime);
            }
        }

        protected override bool OnValidateInteraction(IInteractableContext context, CharacterType charaType)
        {
            return _ownerPlayerRef == PlayerRef.None || _ownerPlayerRef == PlayerRef.FromEncoded(context.Interactor);
        }

        protected override void OnInteract(IInteractableContext context)
        {
            if (_ownerPlayerRef == PlayerRef.None)
                GetOn(PlayerRef.FromEncoded(context.Interactor));
            else if (_ownerPlayerRef == PlayerRef.FromEncoded(context.Interactor))
                GetOff();
        }

        private float _currentBlendValue = 0.01f;
        
        // 動き周り
        private void Move(Vector2 moveDirection)
        {
            Vector3 cameraForward = _cameraController.GetCameraForward();
            Vector3 cameraRight = _cameraController.GetCameraRight();
            
            // 方向キーの入力値とカメラの向きから、移動方向を決定
            Vector3 moveDir = (cameraForward * moveDirection.y + cameraRight * moveDirection.x).normalized;
            
            // 移動方向にスピードをかける。ジャンプや落下がある場合は、別途Y軸方向の速度ベクトルを足す。
            _rigidbody.linearVelocity = moveDir * _moveSpeed;

            if (moveDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
            }
            
            float speed = moveDir.magnitude;
            
            _currentBlendValue = Mathf.Lerp(_currentBlendValue, speed, Time.deltaTime * 5f);
            float clampedBlend = Mathf.Clamp(_currentBlendValue,0.01f,1.0f);
            
            _animator.SetFloat(_flyStateBlend, clampedBlend);
        }

        private void GetOn(PlayerRef ownerPlayerRef)
        {
            if (!Runner.IsServer || _ownerPlayerRef != PlayerRef.None) 
                return;

            _ownerPlayerRef = ownerPlayerRef;
            Object.AssignInputAuthority(_ownerPlayerRef);

            _cameraController.SetCameraPriority(15);

            _ownerPlayerManager = StaticServiceLocator.Instance.Get<InGameManager>()
                .PlayerDataDic[_ownerPlayerRef].GetComponent<PlayerManager>();
            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.ForcedMovement);
            _ownerPlayerManager.RPC_SetColliderActive(false);
            _ownerPlayerManager.RPC_SetMeshActive(false);
        }

        private void GetOff()
        {
            if (!Runner.IsServer || _ownerPlayerRef == PlayerRef.None) 
                return;

            _ownerPlayerRef = PlayerRef.None;
            Object.RemoveInputAuthority();

            _cameraController.SetCameraPriority(5);

            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.Normal);
            _ownerPlayerManager.RPC_SetColliderActive(true);
            _ownerPlayerManager.RPC_SetMeshActive(true);
            _ownerPlayerManager.transform.position = _getOffPoint.position;
        }
    }
}
