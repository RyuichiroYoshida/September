using System;
using Fusion;
using UniRx;
using Unity.Mathematics;
using UnityEngine;

namespace InGame.Player
{
    /// <summary> プレイヤーの移動 </summary>
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] float _gravity = 9.81f;
        [SerializeField] float _moveSpeed = 5f;
        [SerializeField] private float _acceleration;
        [SerializeField] private float _groundSlopeThreshold = 45f;
        [Header("Dash")]
        [SerializeField] float _dashSpeed = 10f;
        // スタミナ切れた時のダッシュできない時間(仮)
        [SerializeField] private float _dashCooldown = 3f;
        [Header("Rotation")]
        [SerializeField, Tooltip("degree/s")] private float _rotationSpeed = 5f;
        [Header("Debug")]
        [SerializeField] private float _jumpPower = 10f;

        private Rigidbody _rb;
        private Vector3 _moveVelocity;
        private bool _isGround;
        // スタミナ消費量
        private float _staminaConsumption;
        // スタミナ回復量
        float _staminaRegen;
        private bool _isDashCoolTime;
        private bool CanDash => !_isDashCoolTime && Stamina > 0 && _isGround;

        public readonly BehaviorSubject<float> OnStaminaChanged = new(0);
        
        [Networked, OnChangedRender(nameof(OnChangedStamina))] private float Stamina { get; set; }
        private void OnChangedStamina() => OnStaminaChanged.OnNext(Stamina);
        [Networked, HideInInspector] public float MaxStamina { get; private set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public void Init(float stamina, float staminaConsumption, float staminaRegen)
        {
            Stamina = stamina;
            MaxStamina = stamina;
            _staminaConsumption = staminaConsumption;
            _staminaRegen = staminaRegen;
            
            // なんかFixedUpdateNetworkで値を更新しないと変更が同期されなくてOnChangedRenderが反応しない見たい
            // だから自分で呼ぶ必要がある
            OnStaminaChanged.OnNext(Stamina);
        }

        public void Move(Vector2 moveInput, bool isDash, float cameraYaw, float deltaTime)
        {
            // スタミナ回復
            if (!isDash || _isDashCoolTime) Stamina = math.min(MaxStamina, Stamina + _staminaRegen * deltaTime);
            
            // CameraYawから入力を回転させる
            float radYaw = -cameraYaw * Mathf.Deg2Rad;
            Vector3 rotated = new Vector3(
                moveInput.x * Mathf.Cos(radYaw) - moveInput.y * Mathf.Sin(radYaw),
                0f,
                moveInput.x * Mathf.Sin(radYaw) + moveInput.y * Mathf.Cos(radYaw)
            );
            
            // Dash処理
            isDash = isDash && CanDash;
            
            if (isDash)
            {
                Stamina = math.max(0, Stamina - _staminaConsumption * deltaTime);

                // スタミナなくなったら
                if (Stamina <= 0)
                {
                    // クールタイムに入れて一定時間後に解除
                    _isDashCoolTime = true;
                    Observable.Timer(TimeSpan.FromSeconds(_dashCooldown))
                        .Subscribe(_ => _isDashCoolTime = false).AddTo(this);
                }
            }
            
            // velocityに代入
            Vector3 velocity = rotated * (isDash ? _dashSpeed : _moveSpeed);
            velocity.y = _moveVelocity.y;
            _moveVelocity = velocity;
            
            // 移動による回転
            RotationByDirection(velocity, deltaTime);
        }

        /// <summary> 指定方向に回転する </summary>
        void RotationByDirection(Vector3 direction, float deltaTime)
        {
            if (direction == Vector3.zero) return;
            
            // eulerでLerpする
            Vector3 playerEuler = transform.eulerAngles;
            float startYaw = playerEuler.y;
            float targetYaw = Quaternion.LookRotation(direction).eulerAngles.y;
            // このフレームでの回転量と角度の差からLerpする
            playerEuler.y = Mathf.LerpAngle(startYaw, targetYaw, _rotationSpeed * deltaTime / math.abs(Mathf.DeltaAngle(startYaw, targetYaw)));
            // rotationに代入
            transform.rotation = Quaternion.Euler(playerEuler);
        }

        /// <summary> 接地判定確認のためのテストジャンプ </summary>
        public void TestJump()
        {
            if (!_isGround) return;
            
            _moveVelocity.y = _jumpPower;
            _isGround = false;
        }

        private void FixedUpdate()
        {
            if (HasStateAuthority)
            {
                // 速度の代入はFixedUpdateのタイミングで行う
                ApplyVelocity(Time.fixedDeltaTime);
            }
        }

        private void ApplyVelocity(float deltaTime)
        {
            // 重力
            if (_isGround) _moveVelocity.y = 0;
            else _moveVelocity.y -= _gravity * deltaTime;
            // 速度の代入
            _rb.linearVelocity = _moveVelocity;
            _isGround = false;
        }

        private void CheckGround(Collision collision)
        {
            // 上昇中なら終了
            if (_moveVelocity.y > 0) return;
                
            // 接触面が地面か
            foreach (var contact in collision.contacts)
            {
                if (Vector3.Angle(Vector3.up, contact.normal) <= _groundSlopeThreshold)
                {
                    _isGround = true;
                    return;
                }
            }
        }

        private void OnCollisionStay(Collision other)
        {
            if (HasStateAuthority)
            {
                // 接地判定
                CheckGround(other);
            }
        }
    }
}
