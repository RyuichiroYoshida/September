using System;
using Fusion;
using InGame.Interact;
using InGame.Player;
using September.Common;
using September.InGame.Common;
using UnityEngine;

namespace InGame.Exhibit
{
    [Serializable]
    public class PterodactylInteractEffect : CharacterInteractEffectBase
    {
        public float MoveSpeed = 5f;
        public float TargetBlendValue = 0.01f;
        public string CrySE = "Pteranodon_cry";
        public string FlapSE = "Pteranodon_Flapping_1";
        public Transform GetOffPoint; 
        
        private Rigidbody _rigidbody;
        private Animator _animator;
        private CameraController _cameraController;
        private PlayerManager _ownerPlayerManager;
        private GameInput _gameInput;
        private NetworkObject _networkObject;
        private Transform _transform;
        
        private float _currentBlendValue = 0.01f;
        private float _suppressOffTime;
        private PlayerRef _ownerPlayerRef;
        private bool _isFlying;
        private NetworkRunner _networkRunner;
        
        private static readonly int _flyStateBlend = Animator.StringToHash("FlyStateBlend");
        
        public override void OnInteractStart(IInteractableContext context, InteractableBase target)
        {
            if (_isFlying)
            {
                GetOff();
                return;
            }
            
            _transform = target.transform;
            _networkObject = target.GetComponent<NetworkObject>();
            _rigidbody = target.GetComponent<Rigidbody>();
            _animator = target.GetComponent<Animator>();
            _cameraController = target.GetComponent<CameraController>();
            _networkRunner = _networkObject.Runner;
            

            _gameInput = new GameInput();
            _gameInput.Enable();
            _cameraController.Init(true);

            _ownerPlayerRef = PlayerRef.FromEncoded(context.Interactor);
            GetOn();
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
            if (!_isFlying || _networkObject == null || !_networkObject.HasInputAuthority) 
                return;

            if (_suppressOffTime > 0f)
                _suppressOffTime -= Time.fixedDeltaTime;

            Move(playerInput.MoveDirection);
        }
        
        private void Move(Vector2 moveDirection)
        {
            if (_currentBlendValue < 0.01f)
                _currentBlendValue = TargetBlendValue;

            Vector3 forward = _cameraController.GetCameraForward();
            Vector3 right = _cameraController.GetCameraRight();
            Vector3 moveDir = forward * moveDirection.y + right * moveDirection.x;

            _rigidbody.linearVelocity = moveDir * MoveSpeed;

            if (moveDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                _transform.rotation = Quaternion.Slerp(_transform.rotation, targetRot, Time.deltaTime * 5f);
            }

            float speed = moveDir.magnitude;
            _currentBlendValue = Mathf.Lerp(_currentBlendValue, speed, Time.deltaTime * 5f);
            float blend = Mathf.Clamp(_currentBlendValue, 0.01f, 1.0f);
            _animator.SetFloat(_flyStateBlend, blend);
        }
        
        private void GetOn()
        {
            if (!_networkRunner.IsServer || _ownerPlayerRef == default) 
                return;

            _networkObject.AssignInputAuthority(_ownerPlayerRef);
            CRISound.CRIAudio.PlaySE("Exhibit", CrySE);

            _ownerPlayerManager = StaticServiceLocator.Instance.Get<InGameManager>()
                .PlayerDataDic[_ownerPlayerRef].GetComponent<PlayerManager>();

            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.ForcedControl);
            _ownerPlayerManager.RPC_SetColliderActive(false);
            _ownerPlayerManager.RPC_SetMeshActive(false);

            _transform.position += Vector3.up * 0.3f;
            _rigidbody.isKinematic = false;
            _isFlying = true;
            
            // カメラの設定
            if (_ownerPlayerRef == _networkRunner.LocalPlayer)
            {
                _cameraController.SetCameraPriority(15);
                _cameraController.CameraReset();
            }
            else
            {
                _cameraController.SetCameraPriority(5);
            }
        }
        
        public void GetOff()
        {
            if (!_networkRunner.IsServer || _ownerPlayerRef == default) 
                return;

            _networkObject.RemoveInputAuthority();

            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.Normal);
            _ownerPlayerManager.RPC_SetColliderActive(true);
            _ownerPlayerManager.RPC_SetMeshActive(true);
            _ownerPlayerManager.transform.position = GetOffPoint.position;
            
            _cameraController.SetCameraPriority(5);

            _isFlying = false;
            _ownerPlayerRef = default;
        }
        
        public void PlayFlapSE()
        {
            CRISound.CRIAudio.PlaySE(_transform.position, "Exhibit", FlapSE);
        }

        public override CharacterInteractEffectBase Clone()
        {
            return new PterodactylInteractEffect
            {
                GetOffPoint = GetOffPoint,
                MoveSpeed = MoveSpeed,
                TargetBlendValue = TargetBlendValue,
                CrySE = CrySE,
                FlapSE = FlapSE
            };
        }
    }
}