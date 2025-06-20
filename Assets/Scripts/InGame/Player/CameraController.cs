using Cinemachine;
using DG.Tweening;
using Unity.Mathematics;
using UnityEngine;

namespace InGame.Player
{
    /// <summary> プレイヤーのカメラ操作 </summary>
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private float _sens;
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
        Quaternion _defaultRotation;
        private float _cameraPitch;
        private float _cameraYaw;
        private bool _isInRotation;
        Tweener _cameraTweener;
        
        // camera position
        private CameraSettings _cameraSettings;
        private CameraSettings _defaultCameraSettings;

        public void Init(bool use)
        {
            _cameraPivot.gameObject.SetActive(use);
            if (!use) return;

            // Prefabの初期状態をデフォルトとして保存
            _defaultRotation = _cameraPivot.localRotation;
            _cameraSettings = new CameraSettings(_cameraTf.localPosition);
            _defaultCameraSettings = new CameraSettings(_cameraTf.localPosition);
        }

        private void LateUpdate()
        {
            CheckCameraDistance();
        }

        /// <summary> 入力からカメラを回転させる </summary>
        public void RotateCamera(Vector2 mouseInput, float deltaTime)
        {
            // 他で回転中なら
            if (_isInRotation) return;
            
            float deltaX = mouseInput.y, deltaY = mouseInput.x;
            _cameraPitch -= deltaX * deltaTime * _sens;
            _cameraPitch = Mathf.Clamp(_cameraPitch, -90, 90);
            _cameraYaw += deltaY * _sens * deltaTime;
            _cameraYaw = ToAngle(_cameraYaw);
            
            _cameraPivot.rotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0);
        }

        /// <summary> 障害物に応じてカメラの距離を変える </summary>
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

        /// <summary> カメラを指定方向に回転させる </summary>
        void SmoothRotateCameraTo(Quaternion targetWorldRotation)
        {
            _isInRotation = true;

            // 遷移途中(Pauseを含む)
            if (_cameraTweener.IsActive() && !_cameraTweener.IsComplete())
            {
                _cameraTweener.Kill();
            }
            
            Quaternion endRotation = Quaternion.LookRotation(targetWorldRotation * Vector3.forward, transform.up);

            // 現在のRotationとendRotationを入力のFloatにする
            float targetPitch = endRotation.eulerAngles.x,
                startPitch = _cameraPitch,
                targetYaw = endRotation.eulerAngles.y,
                startYaw = _cameraYaw;

            // 移動Tweenの発火
            _cameraTweener =　DOTween.To(
                () => 0f,
                n =>
                {
                    _cameraPivot.rotation = Quaternion.Euler(math.lerp(startPitch, targetPitch, n), Mathf.LerpAngle(startYaw, targetYaw, n), 0);
                },
                1f,
                _motionDuration
                )
                .OnComplete(() =>
                {
                    _cameraPitch = _cameraPivot.rotation.eulerAngles.x;
                    _cameraYaw = _cameraPivot.rotation.eulerAngles.y;
                    _isInRotation = false;
                    
                    CheckCameraDistance();
                })
                .SetUpdate(UpdateType.Late)
                .SetEase(_motionEase);
        }

        /// <summary> デフォルト位置にリセットする </summary>
        public void CameraReset()
        {
            // デフォルトのLocalQuaternionをWorldにする
            Quaternion relativeRotation = _characterTf.rotation * _defaultRotation;
            SmoothRotateCameraTo(relativeRotation);
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
