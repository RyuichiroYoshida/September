using System;
using Cinemachine;
using UnityEngine;

namespace InGame.Player
{
    public class PlayerCameraController : MonoBehaviour
    {
        [SerializeField] private float _sens;
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private Transform _cameraTf;
        [Header("CameraCollision")]
        [SerializeField] private LayerMask _collideAgainst = ~0;
        [SerializeField] private float _cameraRadius;
        
        private GameInput _gameInput;
        private CameraSettings _cameraSettings;
        private float _cameraPitch;
        private float _cameraYaw;

        private void Start()
        {
            if (!GetComponent<PlayerManager>().IsLocalPlayer)
            {
                _cameraPivot.gameObject.SetActive(false);
                return;
            }
            
            _gameInput = new GameInput();
            _gameInput.Enable();
            
            _cameraSettings = new CameraSettings(_cameraTf.localPosition);
        }

        private void LateUpdate()
        {
            RotateCamera(_gameInput.Player.Look.ReadValue<Vector2>(), Time.deltaTime);
            CheckCameraDistance();
        }

        void RotateCamera(Vector2 mouseInput, float deltaTime)
        {
            float deltaX = mouseInput.y, deltaY = mouseInput.x;
            _cameraPitch -= deltaX * deltaTime * _sens;
            _cameraPitch = Mathf.Clamp(_cameraPitch, -90, 90);
            _cameraYaw += deltaY * _sens * deltaTime;
            
            if (_cameraYaw > 360) _cameraYaw -= 360;
            else if (_cameraYaw < 0) _cameraYaw += 360;
            
            _cameraPivot.rotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0);
        }

        void CheckCameraDistance()
        {
            var isHit = Physics.SphereCast(_cameraPivot.position, _cameraRadius, 
                _cameraTf.position - _cameraPivot.position, out var hit, _cameraSettings.MaxDistance, _collideAgainst);
            
            if (isHit)
            {
                Vector3 sphereCenter = hit.point + hit.normal * _cameraRadius;
                _cameraTf.position = sphereCenter;
            }
            else
            {
                _cameraTf.localPosition = _cameraSettings.DefaultPosition;
            }
        }
    }

    public struct CameraSettings
    {
        public Vector3 DefaultPosition;
        public readonly float MaxDistance;

        public CameraSettings(Vector3 defaultPosition)
        {
            DefaultPosition = defaultPosition;
            MaxDistance = DefaultPosition.magnitude;
        }
    }
}
