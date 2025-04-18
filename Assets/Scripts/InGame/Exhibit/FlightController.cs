using System;
using UnityEngine;
using Cinemachine;
using Cysharp.Threading.Tasks;

namespace September.InGame
{
    [RequireComponent(typeof(Rigidbody))]
    public class FlightController : MonoBehaviour
    {
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private Transform _firePoint;
        [SerializeField] private float _projectileSpeed;
        [SerializeField] private float _flightCoolDown = 30f;
        [SerializeField] private float _moveSpeed = 10f;
        [SerializeField] private float _rotationSpeed = 100f;
        [SerializeField] private float _verticalSpeed = 5f;
        [SerializeField] private float _flightDuration = 5f;
        [SerializeField] private CinemachineFreeLook _virtualCamera;
        [SerializeField] private GameObject _lockAtTarget;
        [SerializeField] private float _liftDuration = 1f;
        [SerializeField] private float _timer;
        [SerializeField] private float _detectionRadius = 20f;
        [SerializeField] private LayerMask _playerLayer;

        public bool _isFlying;
        private bool _hasLanded = false;
        private bool _canFly = true;

        private Rigidbody _rigidbody;
        private OkabeMove _okabeMove;
        
        private Transform _originalFollowTarget;
        private Transform _originalLookAtTarget;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _isFlying = false;
            _rigidbody.isKinematic = true;
        }

        private void Start()
        {
            _okabeMove = FindOkabeMove();
        }

        private void Update()
        {
            if (_isFlying && Input.GetMouseButtonDown(0)) 
            {
                FireProjectile();
            }
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
        
        private void FireProjectile()
        {
            if (_projectilePrefab == null || _firePoint == null) 
                return;
            
            Collider[] hits = Physics.OverlapSphere(transform.position, _detectionRadius, _playerLayer);

            Transform closestTarget = null;
            float closestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                float distance = Vector3.Distance(transform.position, hit.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = hit.transform;
                }
            }
            
            Vector3 shootDirection = _firePoint.forward; 
            if (closestTarget != null)
            {
                shootDirection = (closestTarget.position - _firePoint.position).normalized;
            }
            
            GameObject projectile = Instantiate(_projectilePrefab, _firePoint.position, Quaternion.identity);

            if (projectile.TryGetComponent<Rigidbody>(out var rb))
            {
                Debug.Log(shootDirection * _projectileSpeed);
                rb.linearVelocity = shootDirection * _projectileSpeed;
            }
        }
        
        private OkabeMove FindOkabeMove()
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

            foreach (GameObject player in players)
            {
                OkabeMove okabe = player.GetComponent<OkabeMove>();
                if (okabe != null)
                {
                    return okabe;
                }
            }

            Debug.LogWarning("OkabeMoveが見つかりませんでした");
            return null;
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
            if(!_canFly)
                return;
            
            _rigidbody.isKinematic = false;
            _isFlying = false;
            _rigidbody.useGravity = false;
            _okabeMove.HidePlayer();
            
            if (_virtualCamera != null)
            {
                _originalFollowTarget = _virtualCamera.Follow;
                _originalLookAtTarget = _virtualCamera.LookAt;

                _virtualCamera.Follow = _lockAtTarget.transform;
                _virtualCamera.LookAt = _lockAtTarget.transform;
            }
            
            StartFlightRoutine().Forget();
        }
        
        private async UniTaskVoid StartFlightRoutine()
        {
            float liftHeight = 1.5f;
            Vector3 targetPosition = transform.position + Vector3.up * liftHeight;
            float elapsed = 0f;

            Vector3 startPosition = transform.position;

            while (elapsed < _liftDuration)
            {
                float t = elapsed / _liftDuration;
                transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                elapsed += Time.deltaTime;
                await UniTask.Yield();
            }

            transform.position = targetPosition;
            
            _timer = _flightDuration;
            _isFlying = true;
        }

        private void StopFlight()
        {
            _isFlying = false;
            _rigidbody.useGravity = true;
            WaitForLanding().Forget();
        }
        
        private async UniTaskVoid WaitForLanding()
        {
            _hasLanded = false;

            await UniTask.WaitUntil(() => _hasLanded);

            _rigidbody.isKinematic = true;
            _rigidbody.linearVelocity = Vector3.zero;
            
            if (_virtualCamera != null)
            {
                _virtualCamera.Follow = _originalFollowTarget;
                _virtualCamera.LookAt = _originalLookAtTarget;
            }

            _canFly = false;
            _okabeMove.AppearPlayer(transform);
            
            await UniTask.Delay(System.TimeSpan.FromSeconds(_flightCoolDown));
            _canFly = true;
        }
        
        private void OnCollisionEnter(Collision collision)
        {
            if (!_isFlying && collision.gameObject.CompareTag("Ground")) 
            {
                _hasLanded = true;
            }
        }
    }
}