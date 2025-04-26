using Cinemachine;
using Fusion;
using September.Common;
using UnityEngine;
using NaughtyAttributes;

namespace September.InGame
{
    public class OkabeMove : NetworkBehaviour
    {
        [SerializeField] float _moveSpeed = 5f;
        [SerializeField] Transform _body;

        [SerializeField] Rigidbody _rigidbody;

        //[Networked] public NetworkButtons ButtonsPrevious { get; set; }
        Animator _animator;
        [SerializeField, Label("カメラ設定")] private GameObject _lockAtTarget;
        [SerializeField] private float _speed = 0;
        private CinemachineFreeLook _freeLook;

        private FlightController _flightController;

        public override void Spawned()
        {
            Cursor.lockState = CursorLockMode.Locked;
            _rigidbody = GetComponent<Rigidbody>();
            _animator = GetComponent<Animator>();
            _flightController = FindObjectOfType<FlightController>();
            _flightController.FindOkabeMove(this);
            _freeLook = FindObjectOfType<CinemachineFreeLook>();
            if (HasInputAuthority)
            {
                _freeLook.LookAt = _lockAtTarget.transform;
                _freeLook.Follow = _lockAtTarget.transform;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!GetInput<MyInput>(out var input)) 
                return;
            var velocity = _rigidbody.linearVelocity;

            var dir = new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y) * _moveSpeed;
            if (dir != Vector3.zero)
            {
                transform.forward = dir;
            }

            velocity.x = dir.x;
            velocity.z = dir.z;
            _rigidbody.linearVelocity = velocity;
        }

        public void HidePlayer()
        {
            gameObject.SetActive(false);
        }

        public void AppearPlayer(Transform appearTransform)
        {
            _freeLook.Follow = _lockAtTarget.transform;
            _freeLook.LookAt = _lockAtTarget.transform;
            gameObject.transform.position = appearTransform.position;
            gameObject.SetActive(true);
        }


        public void OnFootstep()
        {
            //animationEvent用のメソッド
        }
    }

}