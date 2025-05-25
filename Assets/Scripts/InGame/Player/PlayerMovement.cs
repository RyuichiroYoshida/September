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
        [SerializeField] float _moveSpeed = 5f;
        [SerializeField] float _dashSpeed = 10f;
        // スタミナ切れた時のダッシュできない時間(仮)
        [SerializeField] private float _dashCooldown = 3f;

        // スタミナ消費量
        private float _staminaConsumption;
        // スタミナ回復量
        float _staminaRegen;
        private Rigidbody _rb;
        private bool CanDash => !_isDashCoolTime && Stamina > 0;
        private bool _isDashCoolTime;

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
            velocity.y = _rb.linearVelocity.y;
            _rb.linearVelocity = velocity;
        
            // 移動による回転までとりあえずここに書く
            _rb.angularVelocity = Vector3.zero;
            transform.LookAt(transform.position + rotated, Vector3.up);
        }
    }
}
