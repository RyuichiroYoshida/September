using System;
using Cinemachine;
using Fusion;
using UnityEngine;

namespace InGame.Player
{
    /// <summary>
    /// Playerのどこまでの機能を入れるかは未定
    /// </summary>
    public class PlayerManager : NetworkBehaviour
    {
        [SerializeField] CinemachineVirtualCameraBase _playerCameraPrefab;
        [SerializeField] Transform _lookAtTf;

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                // カメラの設定
                var virtualCamera = Instantiate(_playerCameraPrefab, transform);
                virtualCamera.Follow = transform;
                virtualCamera.LookAt = _lookAtTf;
                
                // カーソルを消す todo:ゲームロジックがやるべき
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
    }
}
