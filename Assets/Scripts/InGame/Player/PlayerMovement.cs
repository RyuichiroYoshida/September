using System;
using Fusion;
using UniRx;
using UnityEngine;

namespace InGame.Player
{
    /// <summary> プレイヤーの移動 </summary>
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("BasicMove")]
        [SerializeField, Tooltip("重力")] private float _gravity = 9.81f;
        [SerializeField, Tooltip("加速")] private float _acceleration;
        [SerializeField, Tooltip("最大速度")] private float _maxMoveSpeed;
        [SerializeField, Tooltip("空中加速")] private float _airAcceleration;
        [SerializeField, Tooltip("摩擦")] private float _friction;
        [SerializeField, Tooltip("摩擦")] private float _airFriction;
        [SerializeField, Tooltip("地面と認識する最大角度")] private float _groundSlopeThreshold = 45f;
        [SerializeField] private float _jumpPower = 10f;
        [Header("Dash")]
        [SerializeField] float _dashAcceleration;
        [SerializeField] private float _maxDashSpeed;
        [SerializeField] private float _dashCooldown = 3f;
        [Header("Rotation")]
        [SerializeField, Tooltip("degree/s")] private float _rotationSpeed = 5f;

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
        public bool IsGround => _isGround;
        public Vector3 MoveVelocity => _moveVelocity;
        
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

        public void UpdateMovement(Vector2 moveInput, bool isDash, float cameraYaw, bool isJump, float deltaTime)
        {
            // set velocity
            Move(moveInput, isDash, cameraYaw, deltaTime);
            if (isJump) Jump();
            ApplyVelocity(deltaTime);
            
            // Character の回転
            RotationByDirection(_moveVelocity, deltaTime);
            
            // スタミナの更新
            UpdateStamina(isDash, deltaTime);
            
            // is ground のリセット
            _isGround = false;
        }

        /// <summary> move velocity の計算 </summary>
        private void Move(Vector2 moveInput, bool isDash, float cameraYaw, float deltaTime)
        {
            // CameraYaw から入力を回転させる
            float radYaw = -cameraYaw * Mathf.Deg2Rad;
            Vector3 moveDirection = new Vector3(
                moveInput.x * Mathf.Cos(radYaw) - moveInput.y * Mathf.Sin(radYaw),
                0f,
                moveInput.x * Mathf.Sin(radYaw) + moveInput.y * Mathf.Cos(radYaw)
            );
            
            // Dash処理
            isDash = isDash && CanDash;
            
            // Dash中ならスタミナを消費させる
            if (isDash)
            {
                Stamina = Mathf.Max(0, Stamina - _staminaConsumption * deltaTime);

                // スタミナなくなったら
                if (Stamina <= 0)
                {
                    // クールタイムに入れて一定時間後に解除
                    _isDashCoolTime = true;
                    Observable.Timer(TimeSpan.FromSeconds(_dashCooldown))
                        .Subscribe(_ => _isDashCoolTime = false).AddTo(this);
                }
            }
            
            Vector2 moveDirection2 = new Vector3(
                moveInput.x * Mathf.Cos(radYaw) - moveInput.y * Mathf.Sin(radYaw),
                moveInput.x * Mathf.Sin(radYaw) + moveInput.y * Mathf.Cos(radYaw)
            );
            
            CalcMoveVelocity(moveDirection2, isDash, deltaTime);
        }

        private void CalcMoveVelocity(Vector2 moveDir, bool isDash, float deltaTime)
        {
            Vector2 moveVelocity2 = new Vector2(_moveVelocity.x, _moveVelocity.z);
            float lastMoveMag = moveVelocity2.magnitude;
            // is ground で摩擦量が変わる
            float friction = (_isGround ? _friction : _airFriction) * deltaTime;
            
            // 入力がある場合
            if (moveDir != Vector2.zero)
            {
                // 加速
                float acceleration = (_isGround ? isDash ? _dashAcceleration : _acceleration : _airAcceleration) * deltaTime;
                Vector2 targetVelocity = moveVelocity2 + moveDir * acceleration;
            
                float maxSpeed = isDash ? _maxDashSpeed : _maxMoveSpeed;
                float moveMag = targetVelocity.magnitude;

                if (!_isGround)
                {
                    // 空中は加速も摩擦もかける
                    moveVelocity2 = (moveMag - friction) / moveMag * targetVelocity;
                }
                else if (moveMag > maxSpeed) // todo:加速後のVectorから計算したいけど摩擦の計算時に加速を入れたくない
                {
                    // 入力があってMaxSpeedを超えた場合、摩擦をかけるがMaxSpeedを下回らない
                    friction = Mathf.Min(friction, lastMoveMag - maxSpeed);
                    moveVelocity2 = (lastMoveMag - friction) / moveMag * targetVelocity;
                }
                else // max speed を超えない場合
                {
                    // 加速する
                    moveVelocity2 = targetVelocity;
                }

                _moveVelocity.x = moveVelocity2.x;
                _moveVelocity.z = moveVelocity2.y;
            }
            else // 入力がなかった場合
            {
                // 摩擦をかけるだけ
                Vector3 frictionVec = friction * -_moveVelocity.normalized;
                frictionVec = Vector3.ClampMagnitude(frictionVec, _moveVelocity.magnitude);
                frictionVec.y = 0;
                _moveVelocity += frictionVec;
            }
        }

        /// <summary> 指定方向に回転する </summary>
        private void RotationByDirection(Vector3 direction, float deltaTime)
        {
            direction.y = 0;
            
            if (direction == Vector3.zero) return;

            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), _rotationSpeed * deltaTime);
        }

        private void ApplyVelocity(float deltaTime)
        {
            // 重力
            if (_isGround) _moveVelocity.y = 0;
            else _moveVelocity.y -= _gravity * deltaTime;
            // 速度の代入
            _rb.linearVelocity = _moveVelocity;
        }

        /// <summary> 条件付きでスタミナを回復させる </summary>
        private void UpdateStamina(bool dashInput, float deltaTime)
        {
            if (!dashInput || _isDashCoolTime) Stamina = Mathf.Min(MaxStamina, Stamina + _staminaRegen * deltaTime);
        }

        private void Jump()
        {
            if (!_isGround) return;
            
            _moveVelocity.y = _jumpPower;
            _isGround = false;
        }

        public void AddForce(Vector3 force)
        {
            _moveVelocity += force;
            if (_moveVelocity.y > 0) _isGround = false;
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
