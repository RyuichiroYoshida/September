using UnityEngine;

namespace September.InGame
{
    [RequireComponent(typeof(Rigidbody))]
    public class FlightController : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 10f;
        [SerializeField] private float _rotationSpeed = 100f;
        [SerializeField] private float _verticalSpeed = 5f;
        [SerializeField] private float _flightDuration = 5f;
        [SerializeField] private float _timer;

        public bool _isFlying;

        private Rigidbody _rigidbody;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _isFlying = false;
        }

        private void FixedUpdate()
        {
            if (!_isFlying)
                return;
            
            _timer -= Time.fixedDeltaTime;
            if (_timer <= 0f)
            {
                StopFlight();
                return;
            }

            // 入力取得
            float horizontalInput = Input.GetAxis("Horizontal");
            float verticalInput = Input.GetAxis("Vertical");
            float upDownInput = 0f;

            if (Input.GetKey(KeyCode.Space))
                upDownInput += 1f;
            if (Input.GetKey(KeyCode.LeftControl))
                upDownInput -= 1f;

            Rotate(horizontalInput);
            Move(verticalInput, upDownInput);
        }

        private void Rotate(float input)
        {
            float yaw = input * _rotationSpeed * Time.fixedDeltaTime;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            _rigidbody.MoveRotation(_rigidbody.rotation * rotation);
        }

        private void Move(float forwardInput, float upDownInput)
        {
            Vector3 moveDirection = transform.forward * (forwardInput * _moveSpeed);
            moveDirection += Vector3.up * (upDownInput * _verticalSpeed);
            _rigidbody.linearVelocity = moveDirection;
        }

        public void StartFlight()
        {
            _isFlying = true;
            _timer = _flightDuration;
            _rigidbody.useGravity = false;
        }

        private void StopFlight()
        {
            _isFlying = false;
            _rigidbody.useGravity = true;
            _rigidbody.linearVelocity = Vector3.zero;
        }
    }
}