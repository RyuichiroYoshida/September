using InGame.Common;
using UnityEngine;

namespace InGame.Player
{
    public class PlayerAnimBase : AnimationClipPlayer
    {
        [SerializeField] private string _paramIsStan = "IsStan";
        [SerializeField] private string _pramSpeed = "Speed";
        [SerializeField] private string _pramIsGround = "IsGround";
        
        private PlayerManager _playerManager;
        private PlayerMovement _playerMovement;

        public void Init(PlayerManager playerManager)
        {
            if (!playerManager.IsLocalPlayer)
            {
                enabled = false;
                return;
            }
            
            _playerManager = playerManager;
            _playerMovement = GetComponent<PlayerMovement>();
        }

        private void Update()
        {
            UpdateAnimParams();
        }

        void UpdateAnimParams()
        {
            _animator.SetBool(_paramIsStan, _playerManager.IsStun);
            _animator.SetFloat(_pramSpeed, _playerMovement.GetSpeedOnPlane());
            _animator.SetBool(_pramIsGround, _playerMovement.IsGround);
        }
    }
}
