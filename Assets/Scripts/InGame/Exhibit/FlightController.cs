using System;
using UnityEngine;
using Cinemachine;
using Cysharp.Threading.Tasks;
using Fusion;
using NaughtyAttributes;
using September.Common;

namespace September.InGame
{
    [RequireComponent(typeof(Rigidbody))]
    public class FlightController : NetworkBehaviour
    {
        [Header("Flight Settings")] 
        [SerializeField, Label("飛行スピード")]
        private float _moveSpeed = 10f;

        [SerializeField, Label("回転速度")] private float _rotationSpeed = 100f;
        [SerializeField, Label("上昇速度")] private float _verticalSpeed = 5f;
        [SerializeField, Label("飛行可能時間")] private float _flightDuration = 30f;
        [SerializeField, Label("初期上昇")] private float _liftDuration = 1f;
        [SerializeField, Label("飛行クールダウン")] private float _flightCoolDown = 30f;
        [SerializeField, Label("Clashスキル時間")] private float _clashTime = 30f;

        [Header("Projectile Settings")] 
        [SerializeField, Label("弾")]
        private GameObject _projectilePrefab;

        [SerializeField, Label("発射されるPoint")] private Transform _firePoint;
        [SerializeField, Label("弾の速度")] private float _projectileSpeed;

        [SerializeField, Label("Interact可能範囲")]
        private float _detectionRadius = 20f;

        [SerializeField, Label("固有Abilityを持つLayer")]
        private LayerMask _playerLayer;

        [Header("Camera Settings")] 
        private CinemachineFreeLook _virtualCamera;

        [SerializeField] private GameObject _lockAtTarget;

        [SerializeField] private Material _material;

        public bool _isFlying;

        private bool _hasLanded = false;
        private bool _canFly = true;

        private Rigidbody _rigidbody;
        private RideAbility _rideAbility;

        private Transform _originalFollowTarget;
        private Transform _originalLookAtTarget;

        [Networked] private TickTimer FlightTimer { get; set; }
        private void OnDisable()
        {
            _material.color = Color.white;
        }
        
        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = true;
            _isFlying = false;
        }

        private void Start()
        {
            _virtualCamera = GameObject.Find("PlayerVirtualCamera").GetComponent<CinemachineFreeLook>();
            
            if(_virtualCamera == null) 
                Debug.LogWarning(_virtualCamera);
        }

        public override void FixedUpdateNetwork()
        {
            if (!_isFlying)
            {
                return;
            }
            
            if (FlightTimer.Expired(Runner))
            {
                StopFlight();
                Debug.Log("飛行を終了");
                return;
            }

            if (!GetInput<MyInput>(out var input))
            { 
                Debug.LogWarning($"MyInput<{nameof(MyInput)}> is null");
                return;   
            }
            
            float h = input.MoveDirection.x;
            float v = input.MoveDirection.y;
            float upDown = input.UpDown;

            Rotate(h);
            Move(v,upDown);
        }
        
        private void Rotate(float input)
        {
            float yaw = input * _rotationSpeed * Time.fixedDeltaTime;
            _rigidbody.MoveRotation(_rigidbody.rotation * Quaternion.Euler(0f, yaw, 0f));
        }

        private void Move(float forwardInput, float upDownInput)
        {
            Debug.Log($"[Move] forward: {forwardInput}, upDown: {upDownInput}");
            Vector3 direction = transform.forward * forwardInput * _moveSpeed +
                                Vector3.up * upDownInput * _verticalSpeed;
            _rigidbody.linearVelocity = direction;
        }

        public void StartFlight(RideAbility rideAbility)
        {
            if (!_canFly) 
                return;
            
            _rideAbility = rideAbility;
            
            _rigidbody.isKinematic = false;
            _isFlying = false;
            _rigidbody.useGravity = false;
            
            if (_virtualCamera != null && Object.HasInputAuthority)
            {
                _originalFollowTarget = _virtualCamera.Follow;
                _originalLookAtTarget = _virtualCamera.LookAt;

                _virtualCamera.Follow = _lockAtTarget.transform;
                _virtualCamera.LookAt = _lockAtTarget.transform;
            }

            _rideAbility.HidePlayer();
            StartFlightRoutine().Forget();
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_Clash()
        {
            RunClashRoutine().Forget();
        }

        private async UniTask RunClashRoutine()
        {
            await Stop(_clashTime);
        }

        // 飛行処理
        private async UniTaskVoid StartFlightRoutine()
        {
            if (!Object.HasInputAuthority)
            {
                Debug.LogWarning("所有者ではありません");
                return;
            }
            
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = false;
            
            float liftHeight = 1.5f;
            Vector3 targetPosition = transform.position + Vector3.up * liftHeight;
            Vector3 start = transform.position;
            float elapsed = 0f;

            while (elapsed < _liftDuration)
            {
                float t = Mathf.Clamp01(elapsed / _liftDuration);
                transform.position = Vector3.Lerp(start, targetPosition, t);
                elapsed += Time.deltaTime;
                await UniTask.Yield();
            }

            transform.position = targetPosition;
            FlightTimer = TickTimer.CreateFromSeconds(Runner, _flightDuration);
            _isFlying = true;
        }
        

        // 着陸待ち
        private async UniTaskVoid WaitForLanding()
        {
            _hasLanded = false;
            
            await UniTask.WaitUntil(() => _hasLanded);
            
            _rigidbody.isKinematic = true;
            _rigidbody.linearVelocity = Vector3.zero;
            
            if (_virtualCamera != null && Object.HasInputAuthority)
            {
                _virtualCamera.Follow = _originalFollowTarget;
                _virtualCamera.LookAt = _originalLookAtTarget;
            }
            
            _canFly = false;
            _rideAbility.AppearPlayer(transform);

            await Stop(_flightCoolDown);
        }
        
        private void StopFlight()
        {
            _isFlying = false;
            _rigidbody.useGravity = true;
            WaitForLanding().Forget();
        }

        // 飛行終了
        private async UniTask Stop(float delay)
        {
            _material.color = Color.red;
            
            if (_rideAbility && !_rideAbility.gameObject.activeInHierarchy)
                _rideAbility.AppearPlayer(transform);
            
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