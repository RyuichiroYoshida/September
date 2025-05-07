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
        private Projectile _projectilePrefab;

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

        private PlayerController _ownerPlayer;

        private bool _hasLanded = false;
        private bool _canFly = true;

        private Rigidbody _rigidbody;
        private RideAbility _rideAbility;

        private Transform _originalFollowTarget;
        private Transform _originalLookAtTarget;
        
        private float _airborneTime = 0f; 
        private const float _maxAirborneDuration = 3f;

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
                return;
            
            if (FlightTimer.Expired(Runner))
            {
                EndFlight();
                return;
            }

            UpdateLanded();

            if (!GetInput<MyInput>(out var input))
            { 
                Debug.LogWarning($"MyInput<{nameof(MyInput)}> is null");
                return;   
            }
            
            Rotate(input.MoveDirection.x);
            Move(input.MoveDirection, input.UpDown);

            if (input.Shot)
                FireProjectile();
        }
        
        private void Move(Vector2 inputMove, float upDownInput)
        {
            if (_virtualCamera == null) 
                return;
            // カメラの前方向と右方向を取得（水平だけ）
            Vector3 camForward = _virtualCamera.transform.forward;
            Vector3 camRight = _virtualCamera.transform.right;
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            // 入力に応じた移動方向
            Vector3 moveDir = camForward * inputMove.y + camRight * inputMove.x;

            // 上下入力を加える
            moveDir += Vector3.up * upDownInput;

            _rigidbody.linearVelocity = moveDir.normalized * _moveSpeed;
        }

        // 強制着地
        private void UpdateLanded()
        {
            // 着地していなければタイマー進行
            if (!_hasLanded)
            {
                _airborneTime += Runner.DeltaTime;
                if (_airborneTime >= _maxAirborneDuration)
                {
                    _hasLanded = true;
                    _airborneTime = 0f;
                    Debug.Log("強制着地判定: 空中に3秒滞在");
                }
            }
            else
                _airborneTime = 0f;
        }
        
        private void Rotate(float input)
        {
            float yaw = input * _rotationSpeed * Time.fixedDeltaTime;
            _rigidbody.MoveRotation(_rigidbody.rotation * Quaternion.Euler(0f, yaw, 0f));
        }

        public void StartFlight(RideAbility rideAbility)
        {
            if (!_canFly) 
                return;
            
            _rideAbility = rideAbility;
            
            _ownerPlayer = rideAbility.GetComponent<PlayerController>();
            if (_ownerPlayer == null)
            {
                Debug.LogWarning("PlayerControllerが見つかりません");
                return;
            }
            
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
        
        // フライトを終了する
        private void EndFlight()
        {
            _isFlying = false;
            _rigidbody.useGravity = true;
            WaitForLanding().Forget();
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_Clash()
        {
            ClashRoutine().Forget();
        }

        private async UniTask ClashRoutine()
        {
            await Stop(_clashTime);
        }
        
        // フライト可能待ち時間
        private async UniTask Stop(float delay)
        {
            _material.color = Color.red;
            
            if (_rideAbility && !_rideAbility.gameObject.activeInHierarchy)
                _rideAbility.AppearPlayer(transform);
            
            await UniTask.Delay(TimeSpan.FromSeconds(delay));
            
            _canFly = true;
            _material.color = Color.white;
        }
        
        
        private void FireProjectile()
        {
            if (!Object.HasInputAuthority)
                return;

            Vector3 fireOrigin = _firePoint.position;
            Vector3 fireDirection = _virtualCamera.transform.forward;

            float sphereRadius = 5.0f;
            float sphereDistance = 300f;

            Vector3 direction = fireDirection;

            if (Physics.SphereCast(fireOrigin, sphereRadius, fireDirection, out RaycastHit hit, sphereDistance, _playerLayer))
            {
                direction = (hit.point - _firePoint.position).normalized;
            }

            RPC_FireProjectile(direction);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_FireProjectile(Vector3 direction)
        {
            var projectile = Runner.Spawn(_projectilePrefab,
                _firePoint.position,
                Quaternion.LookRotation(direction));

            projectile.NetworkRb.Rigidbody.linearVelocity = direction.normalized * _projectileSpeed;
            projectile.Owner = _ownerPlayer; 
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_isFlying && collision.gameObject.CompareTag("Ground"))
            {
                _hasLanded = true;
                _airborneTime = 0f;
            }
        }
    }
}