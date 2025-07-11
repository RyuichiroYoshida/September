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

        private float _currentSpeed;

        private PlayerManager _ownerPlayerManager;
        
        [Networked] private float _currentBlendValue { get; set; }

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
           if (!HasStateAuthority) 
               return;
           
           if (GetInput<PlayerInput>(out var input))
           {
               Vector2 moveDirection = input.MoveDirection;
               Move(moveDirection);
               
               _currentBlendValue = Mathf.Lerp(_currentBlendValue, moveDirection.magnitude, Runner.DeltaTime * 5f);
               
               if (input.DesiredLookDirection.sqrMagnitude > 0.001f)
               {
                   Vector3 flatDir = new(input.DesiredLookDirection.x, 0f, input.DesiredLookDirection.z);
                   Quaternion targetRotation = Quaternion.LookRotation(flatDir, Vector3.up);
                   transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Runner.DeltaTime * 5f);
               }
           }
       }

        private void LateUpdate()
        {
            if (HasInputAuthority)
            {
                if (GameInput.I.Player.Aim.triggered)
                    _cameraController.CameraReset();
                
                _cameraController.RotateCamera(GameInput.I.Player.Look.ReadValue<Vector2>(), Time.deltaTime);
            }

            float clampedBlend = Mathf.Clamp(_currentBlendValue, 0.01f, 1f);
            _animator.SetFloat(_flyStateBlend, clampedBlend);
        }

        // キャラクターごとにスキルを変更する
        protected override void OnInteract(IInteractableContext context)
        {
            PlayerRef requester = PlayerRef.FromEncoded(context.Interactor);

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
            
            if (!GetInput<PlayerInput>(out var input)) 
                return;
            
            Vector3 lookDir = input.DesiredLookDirection;
            Vector3 cameraForward = lookDir.normalized;
            Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward);

            Vector3 moveDir = cameraForward * moveDirection.y + cameraRight * moveDirection.x;

            // 飛行挙動
            Vector3 velocityTarget = moveDir.normalized * _moveSpeed;
            Vector3 velocityDelta = velocityTarget - _rigidbody.linearVelocity;
            _rigidbody.AddForce(velocityDelta, ForceMode.VelocityChange);
        }
        
        private void GetOn(PlayerRef ownerPlayerRef)
        {
            if(!Runner.IsServer || OwnerPlayerRef != PlayerRef.None)
                return;
            
            OwnerPlayerRef = ownerPlayerRef;
            Object.AssignInputAuthority(OwnerPlayerRef);
            OnPlaySE(_crySE);

            _ownerPlayerManager = StaticServiceLocator.Instance.Get<InGameManager>()
                .PlayerDataDic[OwnerPlayerRef].GetComponent<PlayerManager>();

            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.ForcedControl);
            _ownerPlayerManager.RPC_SetColliderActive(false);
            _ownerPlayerManager.RPC_SetMeshActive(false);
            
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

        [Rpc(RpcSources.All,RpcTargets.All)]
        private void RPC_PlaySE(Vector3 position, string cueName)
        {
            CRIAudio.PlaySE(position,"Exhibit", cueName);
        }
        
        private void OnPlaySE(string cueName)
        {
            RPC_PlaySE(transform.position, cueName);
        }
    }
}
