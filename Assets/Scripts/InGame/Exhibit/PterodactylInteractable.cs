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
        
        [Header("Sound Settings")]
        [SerializeField] private string _crySE = "Pteranodon_cry";
        [SerializeField] private string _flapSE = "Pteranodon_Flapping_1";

        [Header("Movement Settings")] 
        [SerializeField] private float _moveSpeed;
        private Rigidbody _rigidbody;
        
        [Header("Camera Settings")]
        private CameraController _cameraController;

        [Header("Animation Settings")]
        [SerializeField,Label("アニメーション最低値")]private float _targetBlendValue = 0.01f;
        private Animator _animator;
        
        [Header("Network Settings")]
        [Networked, OnChangedRender(nameof(OnChangeOwnerRef))] private PlayerRef OwnerPlayerRef { get; set; }
        
        [Networked] public float CurrentSpeed { get; set; }

        private PlayerManager _ownerPlayerManager;
        
        private float _currentBlendValue = 0.01f;

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
        }

       private void Start()
       {
           _rigidbody.isKinematic = true;
       }

       public override void FixedUpdateNetwork()
       {
           if (HasStateAuthority)
           {
               if (GetInput<PlayerInput>(out var input))
               {
                   Vector2 moveDirection = input.MoveDirection;
                   Move(moveDirection);
                   CurrentSpeed = moveDirection.magnitude;
                   if (input.Buttons.IsSet(PlayerButtons.Attack))
                   {
                       _cameraController.CameraReset();
                   }
               }
           }
       }

        private void LateUpdate()
        {
            if (HasInputAuthority)
            {
                if (GameInput.I.Player.Aim.triggered)
                {
                    _cameraController.CameraReset();
                }
                
                _cameraController.RotateCamera(GameInput.I.Player.Look.ReadValue<Vector2>(), Time.deltaTime);
            }

            _currentBlendValue = Mathf.Lerp(_currentBlendValue, CurrentSpeed, Time.deltaTime * 5f);
            float clampedBlend = Mathf.Clamp(_currentBlendValue,0.01f,1.0f);
            _animator.SetFloat(_flyStateBlend, clampedBlend);
        }

        // キャラクターごとにスキルを変更する
        protected override void OnInteract(IInteractableContext context)
        {
            var requester = PlayerRef.FromEncoded(context.Interactor);

            if (OwnerPlayerRef == PlayerRef.None)
                RPC_RequestGetOn(requester);
            else if(OwnerPlayerRef == requester)
                GetOff();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.StateAuthority)]
        private void RPC_RequestGetOn(PlayerRef requester)
        {
            GetOn(requester);
        }
        
        // 動き周り
        private void Move(Vector2 moveDirection)
        {
            if(_rigidbody.isKinematic)
                return;
            
            Vector3 cameraForward = _cameraController.GetCameraForward();
            Vector3 cameraRight = _cameraController.GetCameraRight();
            Vector3 moveDir = cameraForward * moveDirection.y + cameraRight * moveDirection.x;
            
            _rigidbody.linearVelocity = moveDir * _moveSpeed;

            if (moveDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
            }
        }
        
        private void GetOn(PlayerRef ownerPlayerRef)
        {
            if(!Runner.IsServer || OwnerPlayerRef != PlayerRef.None)
                return;
            
            OwnerPlayerRef = ownerPlayerRef;
            Object.AssignInputAuthority(OwnerPlayerRef);
            CRIAudio.PlaySE("Exhibit",_flapSE);

            _ownerPlayerManager = StaticServiceLocator.Instance.Get<InGameManager>()
                .PlayerDataDic[OwnerPlayerRef].GetComponent<PlayerManager>();

            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.ForcedControl);
            _ownerPlayerManager.RPC_SetColliderActive(false);
            _ownerPlayerManager.RPC_SetMeshActive(false);
    
            transform.position += Vector3.up * 0.3f;
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
            _rigidbody.isKinematic = true;
        }

        private void OnChangeOwnerRef()
        {
            if (OwnerPlayerRef == Runner.LocalPlayer)
            {
                _cameraController.SetCameraPriority(15);
                _cameraController.CameraReset();
            }
            else
            {
                _cameraController.SetCameraPriority(5);
            }
        }
        
        protected override bool OnValidateInteraction(IInteractableContext context, CharacterType charaType)
        {
            // すでにキャラクターが乗っていたらインタラクト不可能にする
            return OwnerPlayerRef == PlayerRef.None || OwnerPlayerRef == PlayerRef.FromEncoded(context.Interactor);
        }
        
        public void OnPlaySE()
        {
            RPC_PlaySE(transform.position, _crySE);
        }

        [Rpc(RpcSources.All,RpcTargets.All)]
        private void RPC_PlaySE(Vector3 position, string cueName)
        {
            CRIAudio.PlaySE(position,"Exhibit", cueName);
        }
    }
}
