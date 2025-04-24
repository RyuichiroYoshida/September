using System;
using UnityEngine;
using Cinemachine;
using Cysharp.Threading.Tasks;
using Fusion;
using NaughtyAttributes;

namespace September.InGame
{
    [RequireComponent(typeof(Rigidbody))]
    public class FlightController : NetworkBehaviour
    {
        [Header("Flight Settings")] [SerializeField, Label("飛行スピード")]
        private float _moveSpeed = 10f;

        [SerializeField, Label("回転速度")] private float _rotationSpeed = 100f;
        [SerializeField, Label("上昇速度")] private float _verticalSpeed = 5f;
        [SerializeField, Label("飛行可能時間")] private float _flightDuration = 30f;
        [SerializeField, Label("初期上昇")] private float _liftDuration = 1f;
        [SerializeField, Label("飛行クールダウン")] private float _flightCoolDown = 30f;
        [SerializeField, Label("Clashスキル時間")] private float _clashTime = 30f;

        [Header("Projectile Settings")] [SerializeField, Label("弾")]
        private GameObject _projectilePrefab;

        [SerializeField, Label("発射されるPoint")] private Transform _firePoint;
        [SerializeField, Label("弾の速度")] private float _projectileSpeed;

        [SerializeField, Label("Interact可能範囲")]
        private float _detectionRadius = 20f;

        [SerializeField, Label("固有Abilityを持つLayer")]
        private LayerMask _playerLayer;

        [Header("Camera Settings")] [SerializeField]
        private CinemachineFreeLook _virtualCamera;

        [SerializeField] private GameObject _lockAtTarget;

        [Header("Misc")] [SerializeField] private Material _material;

        public bool _isFlying;

        private bool _hasLanded = false;
        private bool _canFly = true;
        private float _timer;

        private Rigidbody _rigidbody;
        private OkabeMove _okabeMove;

        private Transform _originalFollowTarget;
        private Transform _originalLookAtTarget;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = true;
            _isFlying = false;
        }

        private void OnDisable()
        {
            _material.color = Color.white;
        }

        private void Update()
        {
            if (_isFlying && Input.GetMouseButtonDown(0))
            {
                FireProjectile();
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!_isFlying) return;

            _timer -= Time.fixedDeltaTime;
            if (_timer <= 0f)
            {
                StopFlight();
                return;
            }

            float hInput = Input.GetAxis("Horizontal");
            float vInput = Input.GetAxis("Vertical");
            float upDownInput = 0f;
            if (Input.GetKey(KeyCode.Space)) 
                upDownInput += 1f;
            if (Input.GetKey(KeyCode.LeftControl)) 
                upDownInput -= 1f;

            Rotate(hInput);
            Move(vInput, upDownInput);
        }

        private void FireProjectile()
        {
            if (_projectilePrefab == null || _firePoint == null) return;

            Collider[] hits = Physics.OverlapSphere(transform.position, _detectionRadius, _playerLayer);
            Transform closestTarget = null;
            float closestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closestTarget = hit.transform;
                }
            }

            Vector3 shootDir = closestTarget != null
                ? (closestTarget.position - _firePoint.position).normalized
                : _firePoint.forward;

            GameObject projectile = Instantiate(_projectilePrefab, _firePoint.position, Quaternion.identity);

            if (projectile.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.linearVelocity = shootDir * _projectileSpeed;
            }
        }

        public void FindOkabeMove(OkabeMove okabeMove)
        {
            _okabeMove = okabeMove.GetComponent<OkabeMove>();
            if (_okabeMove == null)
                Debug.LogWarning("OkabeMoveが見つかりませんでした");
        }

        private void Rotate(float input)
        {
            float yaw = input * _rotationSpeed * Time.fixedDeltaTime;
            _rigidbody.MoveRotation(_rigidbody.rotation * Quaternion.Euler(0f, yaw, 0f));
        }

        private void Move(float forwardInput, float upDownInput)
        {
            Vector3 direction = transform.forward * forwardInput * _moveSpeed +
                                Vector3.up * upDownInput * _verticalSpeed;
            _rigidbody.linearVelocity = direction;
        }

        public void StartFlight()
        {
            if (!_canFly) return;

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
        
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        public void RPC_Clash()
        {
            RunClashRoutine().Forget();
        }

        public void Clash()
        {
            if (!HasInputAuthority)
            {
                Debug.LogWarning("InputAuthorityがないのでRPCを送れません");
                return;
            }
            
            RPC_Clash();
        }

        private async UniTask RunClashRoutine()
        {
            await Stop(_clashTime);
        }

        private async UniTaskVoid StartFlightRoutine()
        {
            float liftHeight = 1.5f;
            Vector3 targetPosition = transform.position + Vector3.up * liftHeight;
            Vector3 start = transform.position;
            float elapsed = 0f;

            while (elapsed < _liftDuration)
            {
                float t = elapsed / _liftDuration;
                transform.position = Vector3.Lerp(start, targetPosition, t);
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

            await Stop(_flightCoolDown);
        }

        private async UniTask Stop(float delay)
        {
            _material.color = Color.red;

            if (!_okabeMove.gameObject.activeInHierarchy)
                _okabeMove.gameObject.SetActive(true);

            await UniTask.Delay(TimeSpan.FromSeconds(delay));
            _canFly = true;
            _material.color = Color.white;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_isFlying && collision.gameObject.CompareTag("Ground"))
                _hasLanded = true;
        }
    }
}