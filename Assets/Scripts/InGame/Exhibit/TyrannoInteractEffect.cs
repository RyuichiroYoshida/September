using System.Collections.Generic;
using Fusion;
using InGame.Health;
using InGame.Interact;
using InGame.Player;
using September.Common;
using September.InGame.Common;
using UnityEngine;


namespace InGame.Exhibit
{
    public class TyrannoInteractEffect : CharacterInteractEffectBase
    {
        [SerializeField]private float _moveSpeed = 5f;
        [SerializeField] private float _maxRotateValue;
        [SerializeField] private Vector3 _moveVelocity;
        [SerializeField] private Vector3 _gravity;
        [SerializeField] private Vector3 _groundNormal;
        [SerializeField] private Transform _getOffPoint;
        [SerializeField]private Vector3 _rayDirection;
        [SerializeField]private float _rayDistance;
        [SerializeField]private Transform _modelTransform;
        
        
        private Transform _transform;
        private CameraController _cameraController;
        private PlayerRef _ownerPlayerRef;
        private PlayerManager _ownerPlayerManager;
        private Animator _animator;
        private Rigidbody _rigidbody;
        private GameInput _gameInput;
        private NetworkObject _networkObject;
        private NetworkRunner _networkRunner;
        private bool _isInteracting;
        private bool _isGround;
        private HitData _hitData;
        private int _damageAmount;

        #region AttackParam

        private MeleeHitboxExecutor _executor;
        [SerializeField]private List<Transform> _points;
        [SerializeField] private float _hitboxRadius = 0.2f;
        [SerializeField]private LayerMask _hitMask = default;
        [SerializeField]private int _startFrame = 0;
        [SerializeField]private int _endFrame = 34;
        private int _currentFrame = 0;
        private bool _isAttacking;
        #endregion
        
        public override void OnInteractStart(IInteractableContext context, InteractableBase target)
        {
            if (_isInteracting)
            {
                GetOff();
                _animator.SetBool("IsInteracting", false);
                return;
            }
            _cameraController = target.GetComponent<CameraController>();
            _rigidbody = target.GetComponent<Rigidbody>();
            _cameraController.Init(true);
            _gameInput = new GameInput();
            _gameInput.Enable();
            _animator = target.GetComponentInChildren<Animator>();
            _networkObject = target.GetComponent<NetworkObject>();
            _networkRunner = _networkObject.Runner;
            _transform = target.transform;
            var charaType = context.CharacterType;
            var playerRef = PlayerRef.FromEncoded(context.Interactor);
            if (charaType == CharacterType.OkabeWright)
            {
                GetOn(playerRef);
                _animator.SetBool("IsInteracting", true);
            }
            _executor = new MeleeHitboxExecutor(_points,_hitboxRadius, _hitMask,_startFrame,_endFrame)
            {
                OnHit = collider =>
                {
                    if (collider.TryGetComponent(out IDamageable damageable))
                    {
                        var hitData = new HitData(HitActionType.Damage, _damageAmount, playerRef, damageable.OwnerPlayerRef);
                        damageable.TakeHit(ref hitData);
                    }
                }
            };
        }

        public override void OnInteractLateUpdate(float deltaTime)
        {
            if (_networkObject != null && _networkObject.HasInputAuthority)
            {
                if (_gameInput.Player.Aim.triggered)
                {
                    _cameraController.CameraReset();
                }

                _cameraController.RotateCamera(_gameInput.Player.Look.ReadValue<Vector2>(), deltaTime);
            }
        }

        public override void OnInteractFixedNetworkUpdate(PlayerInput playerInput)
        {
            if (_networkObject == null || !_networkObject.HasInputAuthority)
                return;
            CheckIsGround();
            AddGravity(_networkRunner.DeltaTime);
            Move(playerInput.MoveDirection, _networkRunner.DeltaTime);
            _animator.SetBool("Run", playerInput.MoveDirection == Vector2.zero ? false : true);
            if (playerInput.Buttons.IsSet(PlayerButtons.Attack))
            {
                _isAttacking = true;
                _animator.SetTrigger("Attack");
            }
            OnAttackUpdate(_networkRunner.DeltaTime);
        }

        private void GetOn(PlayerRef ownerPlayerRef)
        {
            if (!_networkRunner.IsServer || _ownerPlayerRef != PlayerRef.None) 
                return;

            _ownerPlayerRef = ownerPlayerRef;
            _networkObject.AssignInputAuthority(_ownerPlayerRef);
            _cameraController.SetCameraPriority(15);
            _ownerPlayerManager = StaticServiceLocator.Instance.Get<InGameManager>()
                .PlayerDataDic[_ownerPlayerRef].GetComponent<PlayerManager>();
            
            if (_ownerPlayerManager == null)
                Debug.LogError("PlayerManager is null");
            
            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.ForcedControl);
            _ownerPlayerManager.RPC_SetColliderActive(false);
            _ownerPlayerManager.RPC_SetMeshActive(false);
            _rigidbody.isKinematic = false;
            _isInteracting = true;
        }

        private void GetOff()
        {
            if (!_networkRunner.IsServer || _ownerPlayerRef == default)
                return;
            _networkObject.RemoveInputAuthority();
            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.Normal);
            _ownerPlayerManager.RPC_SetColliderActive(true);
            _ownerPlayerManager.RPC_SetMeshActive(true);
            _ownerPlayerManager.transform.position = _getOffPoint.position;
            _cameraController.SetCameraPriority(5);
            _isInteracting = false;
            _rigidbody.isKinematic = true;
            _ownerPlayerRef = default;
        }
        private void Move(Vector2 inputMoveDirection, float deltaTime)
        {
            if (inputMoveDirection == Vector2.zero) return;
            Vector3 cameraForward = _cameraController.GetCameraForward();
            Vector3 cameraRight = _cameraController.GetCameraRight();
            Vector3 moveDirection = cameraForward * inputMoveDirection.y + cameraRight * inputMoveDirection.x;
            _moveVelocity = Vector3.ProjectOnPlane(moveDirection, _groundNormal).normalized;
            _rigidbody.linearVelocity = _moveVelocity * _moveSpeed;
            Rotate(deltaTime);
        }

        private void AddGravity(float deltaTime)
        {
            _rigidbody.AddForce(_gravity * deltaTime, ForceMode.Acceleration);
        }

        private void Rotate(float deltaTime)
        {
            var rot = Quaternion.RotateTowards(_modelTransform.rotation, Quaternion.LookRotation(_moveVelocity),
                _maxRotateValue * deltaTime);
            _modelTransform.rotation = rot;
        }

        private void CheckIsGround()
        {
            bool ray = Physics.Raycast(_transform.position + Vector3.up, _rayDirection, out RaycastHit hit, _rayDistance);
            var normal = hit.normal;
            if (ray && Vector3.Angle(normal, Vector3.up) < 90)
            {
                _isGround = true;
                _groundNormal = normal;
                return;
            }
            if(!ray || Vector3.Angle(normal, Vector3.up) >= 90)
            {
                _isGround = false;
                _groundNormal = Vector3.up;
            }
        }

        private void OnAttackUpdate(float deltaTime)
        {
            if(!_isAttacking) return;
            _executor?.Tick(deltaTime);
            Debug.Log("Attacking");
            if (_executor.IsFinished)
            {
                _isAttacking = false;
            }
        }
        public override CharacterInteractEffectBase Clone()
        {
            return new TyrannoInteractEffect()
            {
                _moveSpeed =  _moveSpeed,
                _maxRotateValue = _maxRotateValue,
                _moveVelocity = _moveVelocity,
                _gravity = _gravity,
                _groundNormal  = _groundNormal,
                _getOffPoint =  _getOffPoint,
                _rayDirection = _rayDirection,
                _rayDistance = _rayDistance,
                _modelTransform = _modelTransform,
                _points =  _points,
                _hitboxRadius = _hitboxRadius,
                _hitMask = _hitMask,
                _startFrame = _startFrame,
                _endFrame = _endFrame
            };

           
        }
    }
}