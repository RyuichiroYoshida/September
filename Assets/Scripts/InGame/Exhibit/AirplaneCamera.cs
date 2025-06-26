using Cinemachine;
using DG.Tweening;
using UnityEngine;

namespace InGame.Exhibit
{
    /// <summary> プレイヤーのカメラ操作 </summary>
    public class AirplaneCamera : MonoBehaviour
    {
        [SerializeField] private float _sens;
        [SerializeField] private float _padSens;
        [SerializeField] private Transform _characterTf;
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private Transform _cameraTf;
        [SerializeField] private CinemachineVirtualCameraBase _camera;
        [Header("CameraCollision")]
        [SerializeField] private LayerMask _collideAgainst = ~0;
        [SerializeField] private float _cameraRadius;
        [Header("CameraMotion")]
        [SerializeField] private float _motionDuration;
        [SerializeField] private Ease _motionEase;
        
        // camera rotation
        private float _defaultPitch;
        private float _cameraPitch;
        private float _cameraYaw;
        private bool _isInRotation;
        Tweener _cameraTweener;
        private bool _isOperated;
        
        // camera position
        private CameraSettings _defaultCameraSettings;

        void Start()
        {
            // Prefabの初期状態をデフォルトとして保存
            _defaultPitch = _cameraPivot.rotation.eulerAngles.x;
            _defaultCameraSettings = new CameraSettings(_cameraTf.localPosition);
        }

        private void LateUpdate()
        {
            CheckCameraDistance();
        }

        /// <summary> 入力からカメラを回転させる </summary>
        public void InputToCamera(Vector2 mouseInput, float deltaTime)
        {
            if (mouseInput == Vector2.zero)
            {
                if (_isOperated) CameraReset();
                if (!_isInRotation) DefaultRotateCamera(deltaTime);
                _isOperated = false;
            }
            else
            {
                if (_isInRotation)
                {
                    _isInRotation = false;
                    _cameraTweener?.Kill();
                }

                _isOperated = true;
                RotateCamera(mouseInput, deltaTime);
            }
            
            _cameraPivot.rotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0);
        }

        void DefaultRotateCamera(float deltaTime)
        {
            _cameraYaw = _characterTf.rotation.eulerAngles.y;
        }

        void RotateCamera(Vector2 mouseInput, float deltaTime)
        {
            float sens = GameInput.I.UseDeviceType == GameInput.DeviceType.KeyboardMouse ? _sens : _padSens;
            float deltaX = mouseInput.y, deltaY = mouseInput.x;
            _cameraPitch -= deltaX * deltaTime * sens;
            _cameraPitch = Mathf.Clamp(_cameraPitch, -90, 90);
            _cameraYaw += deltaY * sens * deltaTime;
            _cameraYaw = ToAngle(_cameraYaw);
        }

        /// <summary> 障害物に応じてカメラの距離を変える </summary>
        void CheckCameraDistance()
        {
            var isHit = Physics.SphereCast(_cameraPivot.position, _cameraRadius, 
                _cameraTf.position - _cameraPivot.position, out var hit, _defaultCameraSettings.MaxDistance, _collideAgainst);
            
            if (isHit)
            {
                Vector3 sphereCenter = hit.point + hit.normal * _cameraRadius;
                _cameraTf.position = sphereCenter;
            }
            else
            {
                _cameraTf.localPosition = _defaultCameraSettings.DefaultPosition;
            }
        }

        /// <summary> デフォルト位置にリセットする </summary>
        void CameraReset()
        {
            _isInRotation = true;

            // 遷移途中(Pauseを含む)
            if (_cameraTweener.IsActive() && !_cameraTweener.IsComplete())
            {
                _cameraTweener.Kill();
            }

            // 現在のRotationとendRotationを入力のFloatにする
            float startPitch = _cameraPitch,
                startYaw = _cameraYaw;

            // 移動Tweenの発火
            _cameraTweener =　DOTween.To(
                    () => 0f,
                    n =>
                    {
                        _cameraPitch = Mathf.Lerp(startPitch, _defaultPitch, n);
                        _cameraYaw = Mathf.LerpAngle(startYaw, _characterTf.eulerAngles.y, n);
                    },
                    1f,
                    _motionDuration
                )
                .OnComplete(() =>
                {
                    _cameraPitch = _defaultPitch;
                    _cameraYaw = _characterTf.eulerAngles.y;
                    _isInRotation = false;
                    
                    CheckCameraDistance();
                })
                .SetUpdate(UpdateType.Late)
                .SetEase(_motionEase);
        }

        public void SetCameraPriority(int priority)
        {
            _camera.Priority = priority;
        }

        /// <summary> 0 <= return < 360 </summary>
        private static float ToAngle(float angle)
        {
            while (true)
            {
                if (angle >= 360) angle -= 360;
                else if (angle < 0) angle += 360;
                else return angle;
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
