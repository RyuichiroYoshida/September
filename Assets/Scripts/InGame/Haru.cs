using Cinemachine;
using UnityEngine;

namespace September.InGame
{
    public class Haru : MonoBehaviour
    {
        Rigidbody _rigidbody;
        Animator _animator;
        [SerializeField] private float _speed = 0;
        private CinemachineFreeLook _freeLook;

        private FlightController _flightController;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            _rigidbody = GetComponent<Rigidbody>();
            _animator = GetComponent<Animator>();
            _flightController = FindObjectOfType<FlightController>();
            _freeLook = FindObjectOfType<CinemachineFreeLook>();
        }

        private void FixedUpdate()
        {
            Moving();
        }

        private void Moving()
        {
            var velo = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")).normalized;
            Vector3 cameraForward = Camera.main.transform.forward;
            cameraForward.y = 0;
            cameraForward.Normalize();
            Vector3 cameraRight = Camera.main.transform.right;
            cameraRight.y = 0;
            cameraRight.Normalize();
            Vector3 moveDirection = cameraForward * velo.z + cameraRight * velo.x;
            _rigidbody.linearVelocity = moveDirection * _speed;

            if (velo.magnitude > 0)
            {
                var rot = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(moveDirection), 10f);
                transform.rotation = rot;
            }

            _animator.SetFloat("Speed", _rigidbody.linearVelocity.magnitude);
        }
    }
}