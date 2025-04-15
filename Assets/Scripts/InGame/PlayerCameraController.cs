using Cinemachine;
using Cinemachine.Utility;
using Fusion;
using September.Common;
using UnityEngine;

namespace September.InGame
{
    public class PlayerCameraController : NetworkBehaviour
    {
        [SerializeField] Transform _headTransform;
        [SerializeField] float _xAxisSensibility = 1f;
        [SerializeField] float _yAxisSensibility = 1f;
        [SerializeField] float _maxXAxisAngle = 40f;
        [SerializeField] float _minXAxisAngle = -30f;
        float _rotationXAxis;
        float _rotationYAxis;
        CinemachineFreeLook _freeLook;
        
        public override void Spawned()
        {
            if (!HasInputAuthority) return;
            _freeLook = FindObjectOfType<CinemachineFreeLook>();
            if (_freeLook == null) return;
            _freeLook.Follow = _headTransform;
            _freeLook.LookAt = _headTransform;
        }
    }
}