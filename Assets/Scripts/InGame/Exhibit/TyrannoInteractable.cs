using System;
using Fusion;
using InGame.Interact;
using InGame.Player;
using September.Common;
using September.InGame.Common;
using UnityEngine;
using UnityEngine.Serialization;


namespace  InGame.Exhibit
{
    public class TyrannoInteractable : InteractableBase
    {
        private CameraController _cameraController;
        private PlayerRef _ownerPlayerRef;
        private PlayerManager _ownerPlayerManager;   
        private Animator _animator;
        private Rigidbody _rigidbody;
        [SerializeField] private float _speed;
        private GameInput _gameInput;
       [SerializeField] private float _maxRotateValue;
       [SerializeField]private Vector3 _moveVelocity;
       [SerializeField]private Vector3 _gravity;
       [SerializeField] private Vector3 _groundNormal;
        private void Awake()
        {
            _cameraController = GetComponent<CameraController>();
            _rigidbody = GetComponent<Rigidbody>();
            _cameraController.Init(true);
            _gameInput = new GameInput();
            _gameInput.Enable();
        }
        
        public override void FixedUpdateNetwork()
        {
            if(!HasInputAuthority) 
                return;
            
            if (GetInput<PlayerInput>(out var input))
            {
                Move(input.MoveDirection,Runner.DeltaTime);
                // if (input.Buttons.WasPressed(PreviousButtons, PlayerButtons.Interact)) 
                // {
                //     GetOff();
                // }
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

        protected override void OnInteract(IInteractableContext context)
        {
            if(!HasStateAuthority)
                return;
            var charaType = context.CharacterType;
            var playerRef = PlayerRef.FromEncoded(context.Interactor);
            if (charaType == CharacterType.OkabeWright)
            {
                GetOn(playerRef);
            }
        }
        
        protected override bool OnValidateInteraction(IInteractableContext context, CharacterType charaType)
        {
            // すでにキャラクターが乗っていたらインタラクト不可能にする
            return _ownerPlayerRef == PlayerRef.None || _ownerPlayerRef == PlayerRef.FromEncoded(context.Interactor);
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
            
            if (_ownerPlayerManager == null)
                Debug.LogError("PlayerManager is null");
            
            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.ForcedControl);
            _ownerPlayerManager.RPC_SetColliderActive(false);
            _ownerPlayerManager.RPC_SetMeshActive(false);
            _rigidbody.isKinematic = false;
        }

        private void Move(Vector2 inputMoveDirection,float deltaTime)
        {
            if(inputMoveDirection == Vector2.zero) return;
            Vector3 cameraForward = _cameraController.GetCameraForward();
            Vector3 cameraRight = _cameraController.GetCameraRight();
            Vector3 moveDirection = cameraForward * inputMoveDirection.y + cameraRight * inputMoveDirection.x;
            _moveVelocity = Vector3.ProjectOnPlane(moveDirection, _groundNormal).normalized;
            _moveVelocity.y = 0;
            _rigidbody.linearVelocity = _moveVelocity * _speed;
            AddGravity(deltaTime);
            Rotate(deltaTime);
        }

        private void AddGravity(float deltaTime)
        {
            _rigidbody.AddForce(_gravity * deltaTime, ForceMode.Acceleration);
        }

        private void Rotate(float deltaTime)
        {
            var rot = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(_moveVelocity), _maxRotateValue * deltaTime);
            transform.rotation = rot;
        }

        private void OnCollisionStay(Collision other)
        {
            if (HasStateAuthority)
            {
                OnGround(other);
            }
        }

        private void OnGround(Collision other)
        {
            if(other == null)return;
            //地面かどうかの判定がhoshii　タグ指定とか？
            foreach (var contact in other.contacts)
            {
                if (Vector3.Angle(contact.normal, Vector3.up) < 90)
                {
                    _groundNormal = contact.normal;
                    return;
                }
            }
        }
    }
}

