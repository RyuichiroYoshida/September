using CRISound;
using Fusion;
using InGame.Interact;
using InGame.Player;
using NaughtyAttributes;
using September.Common;
using September.InGame.Common;
using UnityEngine;

namespace InGame.Exhibit
{
    public class PterodactylInteractable : InteractableBase
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
        [Networked, OnChangedRender(nameof(OnChangeOwnerRef))] private PlayerRef OwnerPlayerRef { get; set; }
        private PlayerManager _ownerPlayerManager;
        private bool _isFlying;
        [SerializeField,Label("アニメーション最低値")]private float _targetBlendValue = 0.01f;
        private float _currentBlendValue = 0.01f;

        #region AnimationHash

        private static readonly int _flyStateBlend = Animator.StringToHash("FlyStateBlend");
        [Networked]
        public NetworkButtons PreviousButtons { get; set; }
        private float _suppressOffTime = 0f;
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
            _isFlying = false;
        }

       private void Start()
       {
           _rigidbody.isKinematic = true;
       }

        public override void FixedUpdateNetwork()
        {
            if(!_isFlying || !HasInputAuthority) 
                return;

            if (_suppressOffTime > 0f)
                _suppressOffTime -= Runner.DeltaTime;

            if (GetInput<PlayerInput>(out var input))
            {
                Move(input.MoveDirection);
                
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

        protected override bool OnValidateInteraction(IInteractableContext context, CharacterType charaType)
        {
            // すでにキャラクターが乗っていたらインタラクト不可能にする
            return OwnerPlayerRef == PlayerRef.None || OwnerPlayerRef == PlayerRef.FromEncoded(context.Interactor);
        }

        // キャラクターごとにスキルを変更する
        protected override void OnInteract(IInteractableContext context)
        {
            if(!HasStateAuthority)
                return;
            
            var requester = PlayerRef.FromEncoded(context.Interactor);
            
            if (OwnerPlayerRef == PlayerRef.None)
                GetOn(requester);
        }
        
        // 動き周り
        private void Move(Vector2 moveDirection)
        {
            if (_currentBlendValue < 0.01)
            {
                _currentBlendValue = _targetBlendValue;
            }
            Vector3 cameraForward = _cameraController.GetCameraForward();
            Vector3 cameraRight = _cameraController.GetCameraRight();
            
            // 方向キーの入力値とカメラの向きから、移動方向を決定
            Vector3 moveDir = cameraForward * moveDirection.y + cameraRight * moveDirection.x;
            
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
            if (!Runner.IsServer || OwnerPlayerRef != PlayerRef.None) 
                return;

            OwnerPlayerRef = ownerPlayerRef;
            Object.AssignInputAuthority(OwnerPlayerRef);
            CRIAudio.PlaySE("Pteranodon","Pteranodon_cry");

            _ownerPlayerManager = StaticServiceLocator.Instance.Get<InGameManager>()
                .PlayerDataDic[OwnerPlayerRef].GetComponent<PlayerManager>();
            
            if (_ownerPlayerManager == null)
                Debug.LogError("PlayerManager is null");
            
            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.ForcedControl);
            _ownerPlayerManager.RPC_SetColliderActive(false);
            _ownerPlayerManager.RPC_SetMeshActive(false);
            var floatOffset = Vector3.up * 0.3f;
            transform.position += floatOffset;
            _isFlying = true;
            _rigidbody.isKinematic = false;
        }

        private void GetOff()
        {
            if (!Runner.IsServer || OwnerPlayerRef == PlayerRef.None) 
                return;

            OwnerPlayerRef = PlayerRef.None;
            Object.RemoveInputAuthority();

            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.Normal);
            _ownerPlayerManager.RPC_SetColliderActive(true);
            _ownerPlayerManager.RPC_SetMeshActive(true);
            _ownerPlayerManager.transform.position = _getOffPoint.position;
            _isFlying = false;
        }

        private void OnChangeOwnerRef()
        {
            if (OwnerPlayerRef == Runner.LocalPlayer)
            {
                _cameraController.SetCameraPriority(15);
            }
            else
            {
                _cameraController.SetCameraPriority(5);
            }
        }
        
        public void OnPlaySE()
        {
            RPC_PlaySE(transform.position, "Pteranodon_Flapping_1");
        }

        [Rpc(RpcSources.All,RpcTargets.All)]
        private void RPC_PlaySE(Vector3 position, string cueName)
        {
            CRIAudio.PlaySE(position,"Pteranodon", cueName);
        }
    }
}
