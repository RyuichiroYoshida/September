using System;
using System.Collections.Generic;
using Fusion;
using UniRx;
using UnityEngine;

namespace InGame.Player
{
    /// <summary> プレイヤーの移動 </summary>
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("BasicMove")]
        [SerializeField] private CapsuleCollider _moveCapsuleCollider;
        [SerializeField, Tooltip("重力")] private float _gravity = 9.81f;
        [SerializeField, Tooltip("加速")] private float _acceleration;
        [SerializeField, Tooltip("最大速度")] private float _maxMoveSpeed;
        [SerializeField, Tooltip("空中加速")] private float _airAcceleration;
        [SerializeField, Tooltip("摩擦")] private float _friction;
        [SerializeField, Tooltip("摩擦")] private float _airFriction;
        [SerializeField, Tooltip("地面と認識する最大角度")] private float _groundSlopeThreshold = 45f;
        [SerializeField] private LayerMask _groundLayer = ~0;
        [Header("Dash")]
        [SerializeField] float _dashAcceleration;
        [SerializeField] private float _maxDashSpeed;
        [SerializeField] private float _dashCooldown = 3f;
        [SerializeField] private float _staminaConsumption;
        [Header("Rotation")]
        [SerializeField, Tooltip("degree/s")] private float _rotationSpeed = 5f;
        [Header("Vault")]
        [SerializeField, Tooltip("最大高さ")] private float _maxLedgeHeight;
        [SerializeField, Tooltip("最小高さ")] private float _minLedgeHeight;
        [SerializeField, Tooltip("最大奥行")] private float _maxLedgeDepth;
        [SerializeField] private float _reachDistance;
        [SerializeField] private float _timeToVault;
        [SerializeField] private AnimationCurve _vaultCurve;
        [Header("Gizmos")]
        [SerializeField] private float _gizmoDisplayDuration;
        [SerializeField] private int _visibleBit;

        private Rigidbody _rb;
        private PlayerStatus _status;
        
        // base move
        private Vector3 _moveVelocity;
        private Vector3 _rotationDirection;
        private bool _isGround;
        private float _isGroundTimer;
        private Vector3 _groundNormal = Vector3.up;
        private bool _isDashCoolTime;
        private bool CanDash => !_isDashCoolTime && _status.CurrentStamina > 0 && IsGround;
        // vault
        private bool _doingVault;
        private float _vaultTimer;
        private Vector3 _vaultStartPos;
        private Vector3 _vaultTopPos;
        private Vector3 _vaultEndPos;
        // Gizmo
        private float _gizmoTimer;
        private List<CapsuleCastData> _capsuleCastData = new();

        public Vector3 MoveVelocity => _moveVelocity;
        public bool IsGround => _isGround || _isGroundTimer > 0;
        public Vector3 GroundNormal => _groundNormal;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _status = GetComponent<PlayerStatus>();
        }

        public void UpdateMovement(Vector2 moveInput, bool isDash, float cameraYaw, bool isJump, float deltaTime)
        {
            Vector2 moveDirection = GetMoveDirection(moveInput, cameraYaw);
            
            // set velocity
            if (isJump) TryVault(moveDirection);
            if (_doingVault) UpdateVault(deltaTime);
            else
            {
                ApplyGrav(deltaTime);
                Move(moveDirection, isDash, cameraYaw, deltaTime);
                AdsorptionOnGround();
                ApplyVelocity(deltaTime);
            }
            
            // Character の回転
            RotationByDirection(_rotationDirection, deltaTime);
            
            // スタミナの更新
            UpdateStamina(isDash, deltaTime);
            
            // is ground の管理
            if (!_isGround && _isGroundTimer > 0) _isGroundTimer -= deltaTime; 
            _isGround = false;
            _groundNormal = Vector3.up;
        }

        /// <summary> カメラ視点の移動入力を取得 </summary>
        Vector2 GetMoveDirection(Vector2 moveInput, float cameraYaw)
        {
            float radYaw = -cameraYaw * Mathf.Deg2Rad;
            return new Vector2(
                moveInput.x * Mathf.Cos(radYaw) - moveInput.y * Mathf.Sin(radYaw),
                moveInput.x * Mathf.Sin(radYaw) + moveInput.y * Mathf.Cos(radYaw)
                );
        }

        /// <summary> move velocity の計算 </summary>
        private void Move(Vector2 moveDirection, bool isDash, float cameraYaw, float deltaTime)
        {
            // Dash処理
            isDash = isDash && CanDash;
            
            // Dash中ならスタミナを消費させる
            if (isDash && moveDirection != Vector2.zero)
            {
                _status.CurrentStamina = Mathf.Max(0, _status.CurrentStamina - _staminaConsumption * deltaTime);

                // スタミナなくなったら
                if (_status.CurrentStamina <= 0)
                {
                    // クールタイムに入れて一定時間後に解除
                    _isDashCoolTime = true;
                    Observable.Timer(TimeSpan.FromSeconds(_dashCooldown))
                        .Subscribe(_ => _isDashCoolTime = false).AddTo(this);
                }
            }
            
            CalcMoveVelocity(moveDirection, isDash, deltaTime);
        }

        /// <summary> 水平方向のMoveVelocityを計算する </summary>
        private void CalcMoveVelocity(Vector2 moveDir, bool isDash, float deltaTime)
        {
            float lastMoveMag = _moveVelocity.magnitude;
            // is ground で摩擦量が変わる
            float friction = (IsGround ? _friction : _airFriction) * deltaTime;
            
            // 入力がある場合
            if (moveDir != Vector2.zero)
            {
                Vector3 moveDir3 = Quaternion.FromToRotation(Vector3.up, _groundNormal) * new Vector3(moveDir.x, 0, moveDir.y);
                // 加速
                float acceleration = (IsGround ? isDash ? _dashAcceleration : _acceleration : _airAcceleration) * deltaTime;
                Vector3 targetVelocity = _moveVelocity + moveDir3 * acceleration;
            
                float maxSpeed = (isDash ? _maxDashSpeed : _maxMoveSpeed) * _status.MaxSpeedRate;
                float moveMag = targetVelocity.magnitude;

                if (!IsGround)
                {
                    // 空中は加速も摩擦もかける
                    _moveVelocity = (moveMag - friction) / moveMag * targetVelocity;
                }
                else if (moveMag > maxSpeed) // todo:加速後のVectorから計算したいけど摩擦の計算時に加速を入れたくない
                {
                    // 入力があってMaxSpeedを超えた場合、摩擦をかけるがMaxSpeedを下回らない
                    friction = Mathf.Min(friction, lastMoveMag - maxSpeed);
                    _moveVelocity = (lastMoveMag - friction) / moveMag * targetVelocity;
                }
                else // max speed を超えない場合
                {
                    // 加速する
                    _moveVelocity = targetVelocity;
                }

                //_moveVelocity = Quaternion.FromToRotation(Vector3.up, _groundNormal) * new Vector3(moveVelocity2.x, 0, moveVelocity2.y);
                // _moveVelocity = localVelocity + Vector3.up * yMag;
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

        void ApplyGrav(float deltaTime)
        {
            if (IsGround)
            {
                Vector3 normalUp = Quaternion.FromToRotation(_groundNormal, Vector3.up) * _moveVelocity;
                normalUp.y = 0;
                _moveVelocity = Quaternion.FromToRotation(Vector3.up, _groundNormal) * normalUp;
            }
            else _moveVelocity.y -= _gravity * deltaTime;
        }

        void AdsorptionOnGround()
        {
            if (_isGround) return;
            
            Vector3 origin = _moveCapsuleCollider.transform.position + Vector3.up * _moveCapsuleCollider.radius;
            var hit = Physics.SphereCast(origin + Vector3.up * 0.1f, _moveCapsuleCollider.radius, Vector3.down, out var hitInfo, 0.4f, _groundLayer);

            if (hit && hitInfo.distance > 0)
            {
                transform.position += Vector3.down * hitInfo.distance;
            }
        }
        
        private void ApplyVelocity(float deltaTime)
        {
            // 速度の代入
            _rb.linearVelocity = _moveVelocity;
            // 回転の向きを代入
            _rotationDirection = _moveVelocity;
        }

        /// <summary> 指定方向に回転する </summary>
        private void RotationByDirection(Vector3 direction, float deltaTime)
        {
            direction.y = 0;
            
            if (direction == Vector3.zero) return;

            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), _rotationSpeed * deltaTime);
        }

        /// <summary> 条件付きでスタミナを回復させる </summary>
        private void UpdateStamina(bool dashInput, float deltaTime)
        {
            if (!dashInput || _isDashCoolTime) _status.CurrentStamina = Mathf.Min(_status.MaxStamina, _status.CurrentStamina + _status.StaminaRegen * deltaTime);
        }

        private void TryVault(Vector2 moveDirection)
        {
            if (!IsGround) return;

            _gizmoTimer = _gizmoDisplayDuration; // 1 秒 gizmo 表示
            _capsuleCastData.Clear();
            
            // ステップ1：キャラクターが歩くことができない壁やオブジェクトを見つけるために前方にCastします。
            float capsuleRadius = _moveCapsuleCollider.radius;
            Vector3 moveDir3 = new Vector3(moveDirection.x, 0, moveDirection.y);
            Vector3 point1 = transform.position + Vector3.up * (_maxLedgeHeight - capsuleRadius) - moveDir3 * 0.01f;
            Vector3 point2 = transform.position + Vector3.up * (_minLedgeHeight + capsuleRadius) - moveDir3 * 0.01f;
            bool hit = Physics.CapsuleCast(point1, point2, capsuleRadius, moveDir3, out var frontHitInfo, _reachDistance, _groundLayer);
            // hit point が歩けるかどうか
            bool walkablePoint = Vector3.Angle(Vector3.up, frontHitInfo.normal) <= _groundSlopeThreshold;
            
            _capsuleCastData.Add(new CapsuleCastData()
            {
                P1 = point1,
                P2 = point2,
                Direction = moveDir3,
                Distance = moveDir3 * _reachDistance,
                Radius = capsuleRadius,
                IsHit = hit,
                HitInfo = frontHitInfo
            });

            if (!(hit && !walkablePoint)) return;
            
            // ステップ2：乗り越えることのできる高さであるかの判定
            Vector3 origin = frontHitInfo.point + _maxLedgeDepth * 0.5f * -frontHitInfo.normal;
            origin.y = transform.position.y + _maxLedgeHeight + capsuleRadius * 2;
            hit = Physics.SphereCast(origin, capsuleRadius, Vector3.down, out var heightHitInfo, _maxLedgeHeight, _groundLayer);
            float ledgeHeight = heightHitInfo.point.y - transform.position.y;
            
            _capsuleCastData.Add(new CapsuleCastData()
            {
                P1 = origin,
                P2 = origin,
                Direction = Vector3.down,
                Distance = Vector3.down * _maxLedgeHeight,
                Radius = capsuleRadius,
                IsHit = hit,
                HitInfo = heightHitInfo
            });
            
            // min height 以下はステップ1ではじかれる
            if (!(hit && ledgeHeight <= _maxLedgeHeight)) return;
            
            // ステップ3：乗り越えられる障害物の奥行とスペースがあるかの判定
            float underPoint = transform.position.y + 0.01f; // ほんの少し高くする ここの高さは要検討
            float halfHeight = _moveCapsuleCollider.height * 0.5f;
            point1 = frontHitInfo.point - frontHitInfo.normal * 0.01f;
            point1.y = underPoint + capsuleRadius + _moveCapsuleCollider.height;
            point2 = frontHitInfo.point - frontHitInfo.normal * 0.01f;
            point2.y = underPoint + capsuleRadius;
            // 二つ目の障害物を見つける Cast
            hit = Physics.CapsuleCast(point1, point2, capsuleRadius, -frontHitInfo.normal, out var secondHitInfo, _maxLedgeDepth + capsuleRadius, _groundLayer);
            
            _capsuleCastData.Add(new CapsuleCastData()
            {
                P1 = point1,
                P2 = point2,
                Direction = -frontHitInfo.normal,
                Distance = -frontHitInfo.normal * (_maxLedgeDepth + capsuleRadius),
                Radius = capsuleRadius,
                IsHit = hit,
                HitInfo = secondHitInfo
            });
            
            origin = point2 + halfHeight * Vector3.up;
            Vector3 reverseCastOrigin = origin - frontHitInfo.normal * (hit ? secondHitInfo.distance : _maxLedgeDepth + capsuleRadius);
            // 最も近い乗り越えられる地点を探す Cast
            hit = Physics.CapsuleCast(reverseCastOrigin + Vector3.up * halfHeight, reverseCastOrigin + Vector3.down * halfHeight, capsuleRadius, 
                frontHitInfo.normal, out var canVaultHitInfo, _maxLedgeDepth + 0.1f, _groundLayer);
            
            _capsuleCastData.Add(new CapsuleCastData()
            {
                P1 = reverseCastOrigin + Vector3.up * halfHeight,
                P2 = reverseCastOrigin + Vector3.down * halfHeight,
                Direction = frontHitInfo.normal,
                Distance = frontHitInfo.normal * (_maxLedgeDepth + 0.1f),
                Radius = capsuleRadius,
                IsHit = hit,
                HitInfo = canVaultHitInfo
            });

            if (!hit) return;

            Vector3 distance = frontHitInfo.normal * (canVaultHitInfo.distance - 0.01f); // ほんの少し手前
            bool checkPosition = Physics.CheckCapsule(reverseCastOrigin + Vector3.up * halfHeight + distance,
                reverseCastOrigin + Vector3.down * halfHeight + distance, capsuleRadius);

            if (checkPosition) return;
            
            _vaultStartPos = transform.position;
            _vaultTopPos = (frontHitInfo.point + canVaultHitInfo.point) * 0.5f;
            _vaultTopPos.y = heightHitInfo.point.y;
            _vaultEndPos = reverseCastOrigin + Vector3.down * (halfHeight + capsuleRadius) + distance;
            StartVault();
        }

        /// <summary> 乗り越え開始 </summary>
        void StartVault()
        {
            _vaultTimer = 0;
            _doingVault = true;
            Stop();
            
            Vector3 dir = _vaultEndPos - _vaultStartPos;
            dir.y = 0;
            _rotationDirection = dir;
        }

        void UpdateVault(float deltaTime)
        {
            _vaultTimer += deltaTime;
            float t = _vaultTimer / _timeToVault;
            Vector3 resPos = Vector3.Lerp(_vaultStartPos, _vaultEndPos, t);
            float curveValue = _vaultCurve.Evaluate(t >= 0.5f ? 1 - (t - 0.5f) * 2 : t * 2);
            resPos.y = Mathf.Lerp(t >= 0.5f ? _vaultEndPos.y : _vaultStartPos.y, _vaultTopPos.y, curveValue);
            
            transform.position = resPos;
            
            if (_vaultTimer >= _timeToVault) EndVault();
        }

        void EndVault()
        {
            _doingVault = false;
        }

        public void AddForce(Vector3 force)
        {
            _moveVelocity += force;
            if (Vector3.Angle(_moveVelocity, _groundNormal) < 89)
            {
                _isGround = false;
                _isGroundTimer = 0.1f;
            }
        }

        /// <summary> 速度ベクトルを0にする </summary>
        public void Stop()
        {
            _moveVelocity = Vector3.zero;
        }

        public float GetSpeedOnPlane()
        {
            Quaternion normalRot = Quaternion.FromToRotation(_groundNormal, Vector3.up);
            Vector3 onPlaneVec = normalRot * _moveVelocity;
            onPlaneVec.y = 0;
            return onPlaneVec.magnitude;
        }

        private void CheckGround(Collision collision)
        {
            // 接触面が地面か
            foreach (var contact in collision.contacts)
            {
                // 接地できる角度か & 接地面に対して離れる velocity で無いか
                if (Vector3.Angle(Vector3.up, contact.normal) <= _groundSlopeThreshold && (Vector3.Angle(_moveVelocity, contact.normal) >= 89 || _moveVelocity == Vector3.zero))
                {
                    _isGround = true;
                    _isGroundTimer = 0.1f;
                    _groundNormal = contact.normal;
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

        private void Update()
        {
            if (_gizmoTimer > 0)
            {
                _gizmoTimer -= Time.deltaTime;
            }
        }

        struct CapsuleCastData
        {
            public Vector3 P1;
            public Vector3 P2;
            public float Radius;
            public Vector3 Direction;
            public Vector3 Distance;
            public bool IsHit;
            public RaycastHit HitInfo;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_gizmoTimer <= 0) return;

            int index = 0;

            foreach (var castData in _capsuleCastData)
            {
                if (((_visibleBit >> index) & 1) == 0)
                {
                    index++;
                    continue;
                }
                
                if (index == 0) Gizmos.color = Color.green;
                else if (index == 1) Gizmos.color = Color.cyan;
                else if (index == 2) Gizmos.color = Color.yellow;
                else if (index == 3) Gizmos.color = Color.magenta;
                
                DrawCapsuleGizmo(castData.P1, castData.P2, castData.Radius);
                DrawCapsuleGizmo(castData.P1 + castData.Distance, castData.P2 + castData.Distance, castData.Radius);
                Gizmos.DrawLine(castData.P1, castData.P1 + castData.Distance);
                Gizmos.DrawLine(castData.P2, castData.P2 + castData.Distance);

                if (castData.IsHit)
                {
                    Gizmos.color = Color.red;
                    Vector3 origin = (castData.P1 + castData.P2) * 0.5f;
                    origin += castData.Direction * castData.HitInfo.distance;
                    float halfHeight = (castData.P1 - castData.P2).magnitude * 0.5f;
                    DrawCapsuleGizmo(origin + Vector3.up * halfHeight, origin + Vector3.down * halfHeight, castData.Radius);
                }
                
                index++;
            }
            
            if (((_visibleBit >> 4) & 1) == 1)
            {
                Gizmos.color = Color.blue;
                Vector3 radUp = Vector3.up * _moveCapsuleCollider.radius;
                Vector3 radHeightUp = Vector3.up * (_moveCapsuleCollider.radius + _moveCapsuleCollider.height);
                DrawCapsuleGizmo(_vaultStartPos + radUp, _vaultStartPos + radHeightUp, _moveCapsuleCollider.radius);
                DrawCapsuleGizmo(_vaultTopPos + radUp, _vaultTopPos + radHeightUp, _moveCapsuleCollider.radius);
                DrawCapsuleGizmo(_vaultEndPos + radUp, _vaultEndPos + radHeightUp, _moveCapsuleCollider.radius);

                int vertexCount = 20;
                Vector3 frontPrevPos = _vaultStartPos;
                Vector3 backPrevPos = _vaultEndPos;

                for (int i = 1; i <= vertexCount; i++)
                {
                    float t = i / (float)vertexCount;
                    float curveValue = _vaultCurve.Evaluate(t);

                    Vector3 pos = Vector3.Lerp(_vaultStartPos, _vaultTopPos, t);
                    pos.y = Mathf.Lerp(_vaultStartPos.y, _vaultTopPos.y, curveValue);
                    Gizmos.DrawLine(frontPrevPos, pos);
                    frontPrevPos = pos;
                    
                    pos = Vector3.Lerp(_vaultEndPos, _vaultTopPos, t);
                    pos.y = Mathf.Lerp(_vaultEndPos.y, _vaultTopPos.y, curveValue);
                    Gizmos.DrawLine(backPrevPos, pos);
                    backPrevPos = pos;
                }
            }
        }
        
        void DrawCapsuleGizmo(Vector3 p1, Vector3 p2, float r)
        {
            Gizmos.DrawWireSphere(p1, r);
            Gizmos.DrawWireSphere(p2, r);
            Gizmos.DrawLine(p1 + transform.right * r, p2 + transform.right * r);
            Gizmos.DrawLine(p1 - transform.right * r, p2 - transform.right * r);
            Gizmos.DrawLine(p1 + transform.forward * r, p2 + transform.forward * r);
            Gizmos.DrawLine(p1 - transform.forward * r, p2 - transform.forward * r);
        }
#endif
    }
}
