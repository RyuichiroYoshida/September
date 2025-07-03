using System;
using Fusion;
using InGame.Interact;
using InGame.Player;
using September.Common;
using September.InGame.Common;
using UnityEngine;


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
        private void Start()
        {
            _cameraController = GetComponent<CameraController>();
        }
        
        public override void FixedUpdateNetwork()
        {
            if(!HasInputAuthority) 
                return;
            
            if (GetInput<PlayerInput>(out var input))
            {
                Move(input.MoveDirection);
                
                // if (input.Buttons.WasPressed(PreviousButtons, PlayerButtons.Interact)) 
                // {
                //     GetOff();
                // }
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
        }

        private void Move(Vector2 inputMoveDirection)
        {
            Vector3 cameraForward = _cameraController.GetCameraForward();
            Vector3 cameraRight = _cameraController.GetCameraRight();
            Vector3 moveDirection = cameraForward * inputMoveDirection.y + cameraRight * inputMoveDirection.x;
            moveDirection.y = 0;
            _rigidbody.linearVelocity = moveDirection * _speed;

            if (inputMoveDirection.magnitude > 0)
            {
                var rot = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(moveDirection), 10f);
                transform.rotation = rot;
            }
        }
    }
}

