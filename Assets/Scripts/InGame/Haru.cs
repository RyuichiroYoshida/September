using Cinemachine;
using Fusion;
using NaughtyAttributes;
using UnityEngine;

namespace September.InGame
{
    public class Haru : NetworkBehaviour
    {
        Rigidbody _rigidbody;
        Animator _animator;
        [SerializeField] private float _speed = 0;
        private CinemachineFreeLook _freeLook;

        private FlightController _flightController;
        [SerializeField, Label("カメラ設定")] private GameObject _lockAtTarget;

        public override void Spawned()
        {
            base.Spawned();
            Cursor.lockState = CursorLockMode.Locked;
            _rigidbody = GetComponent<Rigidbody>();
            _animator = GetComponent<Animator>();
            _flightController = FindObjectOfType<FlightController>();
            _freeLook = FindObjectOfType<CinemachineFreeLook>();
            
            if (HasInputAuthority)
            {
                _freeLook.LookAt = _lockAtTarget.transform;
                _freeLook.Follow = _lockAtTarget.transform;
            }
        }
    }
}