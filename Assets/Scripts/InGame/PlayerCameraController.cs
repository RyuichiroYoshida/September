using Cinemachine;
using Fusion;
using UnityEngine;

namespace September.InGame
{
    public class PlayerCameraController : NetworkBehaviour
    {
        [SerializeField] Transform _headTransform;
        CinemachineFreeLook _freeLook;
        private void Awake()
        {
            //Cursor.lockState = CursorLockMode.Locked;
        }

        public override void Spawned()
        {
            if (!HasInputAuthority) return;
            _freeLook = FindObjectOfType<CinemachineFreeLook>();
            LookHeadTransform();
        }

        public void LookHeadTransform()
        {
            if (_freeLook == null) return;
            _freeLook.Follow = _headTransform;
            _freeLook.LookAt = _headTransform;
        }
    }
}