using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Jewelry;
using September.Common;
using September.InGame.Common.Stats;
using UniRx;
using UnityEngine;

namespace InGame.Player
{
    /// <summary> プレイヤーの移動 </summary>
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("BasicMove")]
        [SerializeField] private CapsuleCollider _moveCapsuleCollider;
        [SerializeField] private float _moveSpeed;
        [SerializeField, Tooltip("地面と認識する最大角度")] private float _groundSlopeThreshold = 45f;
        [SerializeField, Tooltip("地面と認識する最大距離")] private float _groundDistanceThreshold = 0.25f;
        [SerializeField, Tooltip("接地していないときに足元へ吸着させる最大距離")] private float _groundSnapDistance = 0.4f;
        [SerializeField, Tooltip("接地が切れてから空中扱いになるまでの猶予 (コヨーテタイム)")] private float _coyoteTime = 0.1f;
        [SerializeField] private LayerMask _groundLayer = ~0;
        [Header("鬼状態時の移動速度")]
        [SerializeField] private float _ogreMoveSpeed;
        [SerializeField] private float _ogreDashSpeed;
        [Header("Dash")]
        [SerializeField] private float _dashSpeed;
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
        [Header("Hook")]
        [SerializeField] private float _hookPower = 10f;
        [Header("Bomb")]
        [SerializeField] private float _moveDumping = 0.5f;
        [SerializeField] private float _flyingDamping = 8f;
        [Header("Roll")]
        [SerializeField] private EvasionData _evasionData;
        [SerializeField] private PlayerHealth _playerHealth;
        [SerializeField] private PlayerJewelryRuntime _playerJewelryRuntime;
        [Header("Debug")]
        [SerializeField] private bool _printVaultFailedLog;
        [SerializeField] private bool _visibleGizmos;
        [SerializeField] private VaultGizmoDebugger _vaultGizmoDebugger;
        // ========== ビルドシステム ==========
        [Header("ビルドシステム関連の参照")]
        [SerializeField] BuildGenerator _buildGenerator;
        [SerializeField] PlayerStatus _playerStatus;
        Vector3 _prePos;
        bool _moveBuildEnabled;

        private Rigidbody _rb;
        private PlayerStatus _status;
        private Animator _animator;

        // base move
        private Vector3 _moveVelocity;
        public Vector3 MoveVelocity => _moveVelocity;
        private Vector3 _flyingVelocity;
        private Vector3 _fallVelocity;
        private Vector3 _flyingMoveVelocity;

        private Vector3 _rotationDirection;
        private bool _setDirection;
        private bool _isGround;
        private float _isGroundTimer;
        private float _prevGroundedTime;
        private Vector3 _groundNormal = Vector3.up;
        /// <summary> カプセルを接地面へ吸着させるための下方向移動量 </summary>
        private float _groundGap;
        /// <summary> 接地判定・吸着の探索開始オフセット。足裏からこの高さで探索を始める </summary>
        private const float GroundProbeOffset = 0.1f;
        /// <summary> この値以下の浮きは吸着しない。毎Tickの微小な上下でガタつかせないため </summary>
        private const float GroundSnapTolerance = 0.02f;
        private bool _isDashCoolTime;
        private bool CanDash => !_isDashCoolTime && _status.CurrentStamina > 0 && IsGround;
        private bool _isDash;
        // vault
        private bool _doingVault;
        // Roll
        private PlayerEvasion _playerEvasion;
        /// <summary> 回避の同期状態。Tick 基準なので入力権限側の予測でも決定的に再計算できる </summary>
        [Networked, HideInInspector] public EvasionState Evasion { get; private set; }
        [Networked, HideInInspector] public bool DoingVault { get; private set; }
        public event Action OnStartVault;
        [Networked, HideInInspector] public Vector3 NetworkVelocity { get; private set; }
        [Networked] public Vector2 MoveDirection { get; private set; }
        private bool _isHookFollow;
        private Transform _hookTarget;
        private float _vaultTimer;
        private Vector3 _vaultStartPos;
        private Vector3 _vaultTopPos;
        private Vector3 _vaultEndPos;
        // teleport
        private Vector3? _teleportTarget;
        // knock back
        private bool _knockBackActive = false;
        // Gizmo
        private List<CapsuleCastData> _capsuleCastData = new();

        public float WalkSpeed => GetCurrentMoveSpeed();
        public float DashMoveSpeed => GetCurrentDashSpeed();
        public bool IsGround => (_isGround || _isGroundTimer > 0) && !_knockBackActive;
        [Networked, HideInInspector]
        public NetworkBool IsGroundNet { get; private set; }
        public Vector3 GroundNormal => _groundNormal;
        public bool InfiniteStamina { get; set; } = false;
        public CapsuleCollider MoveCapsuleCollider => _moveCapsuleCollider;
        public LayerMask GroundLayer => _groundLayer;
        public bool IsEvading => Evasion.IsEvading;
        /// <summary> 回避を開始した Tick </summary>
        public int EvasionStartTick => Evasion.StartTick;
        /// <summary> 回避全体の所要時間 (秒、重量係数適用後) </summary>
        public float EvasionDuration => Evasion.RollDuration;
        [Networked] public bool IgnoreMoveInput { get; set; }
        [Networked] public bool IgnoreEvasionInput { get; set; }
        [Networked] public bool IsHookLocked { get; set; }

        public override void Spawned()
        {
            _prevGroundedTime = Runner.SimulationTime;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = true;
            _status = GetComponent<PlayerStatus>();
            _animator = GetComponentInChildren<Animator>();
            // ========== ビルドシステム ==========
            _moveBuildEnabled = _playerStatus & _buildGenerator;
#if UNITY_EDITOR
            if (_moveBuildEnabled)
                Debug.Log("ビルドシステムが正常に動きます");
            else
                Debug.LogWarning("ビルドに関する参照がないためビルドシステムが正常に動作しません\nPlayerMovement.csを確認してください", this);
#endif
            _prePos = transform.position;

            _playerEvasion = new(_evasionData);
        }


        public virtual void UpdateMovement(Vector2 moveInput, bool isDash, float cameraYaw, bool isJump, bool isEvasion, float deltaTime)
        {
            CheckGroundManual();

            MoveDirection = GetMoveDirection(moveInput, cameraYaw);

            if (_isHookFollow)
            {
                var followDirection = _hookTarget.transform.position - this.transform.position;
                followDirection.y = 0;
                _moveVelocity = followDirection * _hookPower;
            }

            if (IgnoreMoveInput || IsHookLocked || IsEvading) moveInput = Vector2.zero;

            Vector2 moveDirection = GetMoveDirection(moveInput, cameraYaw);

            // set velocity
            if (!IsEvading && isJump && HasStateAuthority) TryVault(moveDirection);

            //回避 状態が Networked なので入力権限のみのクライアントでも予測し、再シミュレーションで補正される
            if (!IgnoreEvasionInput && IsGround && !DoingVault && isEvasion)
                StartEvasion();

            if (DoingVault)
                return;

            if (IsEvading)
                _isDash = false;
            else
                Move(moveDirection, isDash, cameraYaw, deltaTime);

            AdsorptionOnGround();
        }

        private void StartEvasion()
        {
            var state = Evasion;
            int jewelryCount = _playerJewelryRuntime.CalculateJewelryScore();

            if (!_playerEvasion.TryStartEvasion(ref state, MoveDirection, transform.forward, Runner.Tick, Runner.DeltaTime, jewelryCount))
                return;

            Evasion = state;
            Stop();
        }

        /// <summary> 入力無関係のTick UpdateMovementとの呼び出し順序を確定させるためにManagerから呼ばれる </summary>
        public virtual void MoveTick(float deltaTime)
        {
            CheckGroundManual();

            //回避
            if (IsEvading) UpdateEvasion();

            if (DoingVault && HasStateAuthority) UpdateVault(deltaTime);

            if (_teleportTarget.HasValue)
            {
                transform.position = _teleportTarget.Value;
                _moveVelocity = Vector3.zero;
                _teleportTarget = null;
            }

            if (IsGround)
            {
                _fallVelocity = Vector3.zero;
                _prevGroundedTime = Runner.SimulationTime;
                _flyingMoveVelocity = _moveVelocity;
            }
            else
            {
                _fallVelocity += Physics.gravity * deltaTime;
            }

            ApplyVelocity(deltaTime);
            // Character の回転
            RotationByDirection(_rotationDirection, deltaTime);

            // スタミナの更新
            UpdateStamina(_isDash, deltaTime);

            if (HasStateAuthority)
            {
                IsGroundNet = IsGround;
            }

            // is ground の管理
            if (!_isGround && _isGroundTimer > 0)
                _isGroundTimer = Mathf.Max(0f, _isGroundTimer - deltaTime);
            if (!_isGround && _isGroundTimer <= 0f)
                _groundNormal = Vector3.up;

            if (_isGround) _moveVelocity = Vector3.zero;
            _isGround = false;
        }

        /// <summary> 回避中の 1 Tick 分の更新。Tick 基準なので予測・再シミュレーションでも同じ結果になる </summary>
        private void UpdateEvasion()
        {
            var state = Evasion;
            int tick = Runner.Tick;
            float dt = Runner.DeltaTime;

            if (_playerEvasion.HasEnded(in state, tick, dt))
            {
                state.IsEvading = false;
                state.LastEndTick = tick;
                Evasion = state;

                if (HasStateAuthority) _playerHealth.IsInvincible = false;
                return;
            }

            // 速度ベースで移動する。Rigidbody が壁との衝突を解決するので座標直書きによる壁登りが起きない
            Vector3 horizontalVelocity = _playerEvasion.CalcVelocity(in state, tick, dt);
            _moveVelocity = Quaternion.FromToRotation(Vector3.up, _groundNormal) * horizontalVelocity;

            Vector3 forward = _playerEvasion.CalcForward(in state, tick, dt);
            _rb.rotation = Quaternion.LookRotation(forward);
            // RotationByDirection が同じ向きへ RotateTowards するので実質 no-op
            SetRotationDirection(forward);

            //無敵状態更新
            if (HasStateAuthority)
            {
                bool isInvincible = _playerEvasion.IsInvincible(in state, tick, dt);

                if (isInvincible != _playerHealth.IsInvincible)
                    _playerHealth.IsInvincible = isInvincible;
            }
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
            if (_animator && _animator.applyRootMotion)
            {
                _moveVelocity = Vector3.zero;
                return;
            }
            // Dash処理
            isDash = isDash && CanDash;
            _isDash = isDash;

            // Dash中ならスタミナを消費させる
            if (isDash && moveDirection != Vector2.zero)
            {
                if (!InfiniteStamina) _status.AddBaseValue(StatType.Stamina, -_status.StaminaConsumption * deltaTime);

                // スタミナなくなったら
                if (_status.CurrentStamina <= 0)
                {
                    // クールタイムに入れて一定時間後に解除
                    _isDashCoolTime = true;
                    Observable.Timer(TimeSpan.FromSeconds(_dashCooldown))
                        .Subscribe(_ => _isDashCoolTime = false).AddTo(this);
                }
            }

            // ========== ビルドシステム ==========
            if (_moveBuildEnabled && _buildGenerator.TryGetBuildEnable(BuildRouteType.MoveSpeed))
            {
                var moveDistance = Vector3.Distance(transform.position, _prePos);
                _buildGenerator?.UpdateBuild(BuildRouteType.MoveSpeed, moveDistance);
                _prePos = transform.position;
            }

            CalcMoveVelocity(moveDirection, isDash, deltaTime);
        }

        public void AddForce(Vector3 force)
        {
            _moveVelocity += force;
            if (Vector3.Angle(_moveVelocity, _groundNormal) < 89)
                _moveVelocity += force;
            if (Vector3.Angle(_moveVelocity, _groundNormal) < 89)
            {
                _isGround = false;
                _isGroundTimer = 0.1f;
                _isGroundTimer = 0;
            }
        }

        /// <summary> 水平方向のMoveVelocityを計算する </summary>
        private void CalcMoveVelocity(Vector2 moveDir, bool isDash, float deltaTime)
        {
            // 入力がある場合
            if (moveDir != Vector2.zero && IsGround)
            {
                Vector3 moveDir3 = Quaternion.FromToRotation(Vector3.up, _groundNormal) * new Vector3(moveDir.x, 0, moveDir.y);
                _moveVelocity = moveDir3 * (isDash ? GetCurrentDashSpeed() : GetCurrentMoveSpeed());
            }
        }

        /// <summary>
        /// 現在のプレイヤーが鬼状態かどうかを判定する
        /// </summary>
        private bool IsOgreState()
        {
            try
            {
                var playerDatabase = PlayerDatabase.Instance;
                if (playerDatabase == null) return false;

                if (playerDatabase.PlayerDataDic.TryGet(Object.InputAuthority, out SessionPlayerData playerData))
                {
                    return playerData.IsOgre;
                }
            }
            catch (System.Exception)
            {
                // エラーが発生した場合は通常状態として扱う
            }

            return false;
        }

        /// <summary>
        /// 鬼状態に応じた現在の移動速度を取得する
        /// </summary>
        private float GetCurrentMoveSpeed()
        {
            // ========== ビルドシステム ==========
            // ビルドの上昇分を乗算
            return (IsOgreState() ? _ogreMoveSpeed : _moveSpeed) * GetMoveMagnification();
        }

        /// <summary>
        /// 鬼状態に応じた現在のダッシュ速度を取得する
        /// </summary>
        private float GetCurrentDashSpeed()
        {
            // ========== ビルドシステム ==========
            // ビルドの上昇分を乗算
            return (IsOgreState() ? _ogreDashSpeed : _dashSpeed) * GetMoveMagnification();
        }

        /// <summary>
        /// 移動速度倍率を計算するメソッド
        /// </summary>
        /// <returns>諸々を計算した最終的な移動速度倍率</returns>
        protected virtual float GetMoveMagnification()
        {
            var result = 1f;    // 計算結果

            result *= _moveBuildEnabled ? _playerStatus.Speed : 1;   // 現在のステータスによる倍率

            return result;
        }

        /// <summary>
        /// 足裏を地面へ吸着させる
        /// <para>
        /// 接地したまま足が浮いていると、ApplyVelocityがy速度を上書きして重力が効かず、
        /// コヨーテタイムが切れるまで空中に留まってから落下する。下り坂ではこれが毎Tick起きて
        /// 空中判定と着地を繰り返し、つっかえる。IsGroundで弾かず接地中も吸着させる
        /// </para>
        /// </summary>
        void AdsorptionOnGround()
        {
            // ノックバック中と上方向へ飛ばされている間は引き戻さない
            if (_knockBackActive || _flyingVelocity.y > 0f) return;

            if (_isGround)
            {
                // CheckGroundManualが測った浮き量へそのまま吸着する
                if (_groundGap <= GroundSnapTolerance) return;

                transform.position += Vector3.down * _groundGap;
                _groundGap = 0f;
                return;
            }

            // 実接地していない場合は、接地判定より広い範囲を探して足元へ引き戻す
            if (!TryProbeGround(_groundSnapDistance, out Vector3 normal, out float gap)) return;

            if (gap > GroundSnapTolerance)
                transform.position += Vector3.down * gap;
            _isGround = true;
            _isGroundTimer = _coyoteTime;
            _groundNormal = normal;
            _groundGap = 0f;
        }

        protected virtual void ApplyVelocity(float deltaTime)
        {
            if (!_knockBackActive)
            {
                if (_isGround)
                {
                    _rb.linearVelocity = _moveVelocity + _flyingVelocity;
                }
                else
                {
                    _flyingMoveVelocity = Vector3.Lerp(_flyingMoveVelocity, Vector3.zero, _moveDumping * deltaTime);

                    _rb.linearVelocity =
                        (_rb.useGravity ? _fallVelocity : Vector3.zero)
                        + _flyingMoveVelocity
                        + _flyingVelocity;
                }
            }

            NetworkVelocity = _rb.linearVelocity;

            // 減衰
            _flyingVelocity = Vector3.Lerp(_flyingVelocity, Vector3.zero, _flyingDamping * deltaTime);

            // 微小値になったら0にする
            if (_flyingVelocity.sqrMagnitude < 0.001f)
                _flyingVelocity = Vector3.zero;

            // 回転の向きを代入
            if (!_setDirection) _rotationDirection = _moveVelocity;
        }

        public void SetRotationDirection(Vector3 lookDirection)
        {
            _rotationDirection = lookDirection;
            _setDirection = true;
        }

        /// <summary> 指定方向に回転する </summary>
        private void RotationByDirection(Vector3 direction, float deltaTime)
        {
            direction.y = 0;
            _setDirection = false;

            if (direction == Vector3.zero) return;

            _rb.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), _rotationSpeed * deltaTime);
        }

        /// <summary> 条件付きでスタミナを回復させる </summary>
        private void UpdateStamina(bool dashInput, float deltaTime)
        {
            if (!dashInput || _isDashCoolTime) _status.AddBaseValue(StatType.Stamina, _status.StaminaRegen * deltaTime);
        }

        private void TryVault(Vector2 moveDirection)
        {
            if (!IsGround)
            {
                if (_printVaultFailedLog) Debug.Log("Vault Failed : Not IsGround");
                return;
            }

            var p = new VaultParameter
            {
                Position = transform.position,
                moveDirection = new Vector3(moveDirection.x, 0, moveDirection.y),

                capsuleRadius = _moveCapsuleCollider.radius,
                capsuleHeight = _moveCapsuleCollider.height,

                reachDistance = _reachDistance * GetSpeedOnPlane(),

                maxLedgeHeight = _maxLedgeHeight,
                minLedgeHeight = _minLedgeHeight,
                maxLedgeDepth = _maxLedgeDepth,

                groundSlopeThreshold = _groundSlopeThreshold,
                groundLayer = _groundLayer
            };

            bool isVault = VaultChecker.TryVault(p, out var result, out var reason);
            Debug.Log(reason);

            if (_vaultGizmoDebugger != null)
            {
                _vaultGizmoDebugger.parameter = p;
            }

            if (!isVault) return;
            _vaultStartPos = result.vaultStart;
            _vaultTopPos = result.vaultTop;
            _vaultEndPos = result.vaultEnd;
            StartVault();
        }

        /// <summary> 乗り越え開始 </summary>
        void StartVault()
        {
            _vaultTimer = 0;
            DoingVault = true;
            OnStartVault?.Invoke();
            Stop();
        }

        void UpdateVault(float deltaTime)
        {
            Vector3 prevPos = transform.position;

            _vaultTimer += deltaTime;
            float t = _vaultTimer / _timeToVault;
            Vector3 resPos = Vector3.Lerp(_vaultStartPos, _vaultEndPos, t);
            float curveValue = _vaultCurve.Evaluate(t >= 0.5f ? 1 - (t - 0.5f) * 2 : t * 2);
            resPos.y = Mathf.Lerp(t >= 0.5f ? _vaultEndPos.y : _vaultStartPos.y, _vaultTopPos.y, curveValue);

            transform.position = resPos;

            SetRotationDirection(_vaultEndPos - _vaultStartPos);

            if (_vaultTimer >= _timeToVault) EndVault((transform.position - prevPos) / deltaTime);
        }

        void EndVault(Vector3 endVelocity)
        {
            _moveVelocity = endVelocity;
            _flyingMoveVelocity = endVelocity;
            _rb.linearVelocity = _moveVelocity;
            DoingVault = false;
        }

        /// <summary> 速度ベクトルを0にする </summary>
        public void Stop()
        {
            _moveVelocity = Vector3.zero;
            _flyingMoveVelocity = Vector3.zero;
            _fallVelocity = Vector3.zero;
            _rb.linearVelocity = _moveVelocity;
        }

        public async UniTask KnockBack(Vector3 force, float duration = 0)
        {
            if (!HasStateAuthority) return;

            _rb.linearVelocity = force;
            _flyingVelocity = force;
            _knockBackActive = true;

            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: this.GetCancellationTokenOnDestroy());

            _knockBackActive = false;
        }

        public void Teleport(Vector3 position)
        {
            _teleportTarget = position;
        }

        public void TeleportImmediate(Vector3 position)
        {
            transform.position = position;
            _prevGroundedTime = Runner.SimulationTime;
            Stop();
        }

        public float GetSpeedOnPlane()
        {
            Quaternion normalRot = Quaternion.FromToRotation(_groundNormal, Vector3.up);
            Vector3 onPlaneVec = normalRot * _rb.linearVelocity;
            onPlaneVec.y = 0;
            return onPlaneVec.magnitude;
        }

        public void OnStartHook()
        {
            IsHookLocked = true;
            MoveDirection = Vector2.zero;
        }

        public void OnHookFollow(Transform targetHook)
        {
            _isHookFollow = true;
            _hookTarget = targetHook;
        }

        public void OnEndHook()
        {
            IsHookLocked = false;
            _isHookFollow = false;
            _hookTarget = null;
            MoveDirection = Vector2.zero;
        }

        public void AddFlyingVelocity(Vector3 force)
        {
            _flyingVelocity = force;
        }

        private void CheckGroundManual()
        {
            if (!TryProbeGround(out Vector3 normal, out float gap)) return;

            _isGround = true;
            _isGroundTimer = _coyoteTime;
            _groundNormal = normal;
            _groundGap = gap;
        }

        /// <summary> 立てる角度の面か </summary>
        private bool IsWalkable(Vector3 normal) => Vector3.Angle(Vector3.up, normal) <= _groundSlopeThreshold;

        /// <summary>
        /// 足裏から真下の地面を探索する
        /// <para>
        /// 足元中心のRaycastを優先し、現在立っている面の法線を取得する。
        /// 下り始めなどRaycastが地面を見失った場合だけSphereCastで補完し、
        /// 登り切りで前方の平地を先取りして移動方向が水平になることを防ぐ
        /// </para>
        /// </summary>
        /// <param name="normal">接地面の法線</param>
        /// <param name="gap">接地面までの下方向移動量</param>
        /// <returns>地面が見つかったか</returns>
        private bool TryProbeGround(out Vector3 normal, out float gap)
        {
            return TryProbeGround(_groundDistanceThreshold, out normal, out gap);
        }

        private bool TryProbeGround(float probeDistance, out Vector3 normal, out float gap)
        {
            if (TryRaycastGround(probeDistance, out normal, out gap)) return true;
            return TrySphereCastGround(probeDistance, out normal, out gap);
        }

        private bool TryRaycastGround(float probeDistance, out Vector3 normal, out float gap)
        {
            normal = Vector3.up;
            gap = 0f;

            Bounds bounds = _moveCapsuleCollider.bounds;
            Vector3 rayOrigin = new(bounds.center.x, bounds.min.y + GroundProbeOffset, bounds.center.z);
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit rayHit, probeDistance, _groundLayer)) return false;
            if (!IsWalkable(rayHit.normal)) return false;

            float radius = Mathf.Min(bounds.extents.x, bounds.extents.z);
            float normalY = Mathf.Max(rayHit.normal.y, Mathf.Epsilon);
            float capsuleSlopeClearance = radius * (1f / normalY - 1f);
            normal = rayHit.normal;
            gap = Mathf.Max(0f, bounds.min.y - rayHit.point.y - capsuleSlopeClearance);
            return true;
        }

        private bool TrySphereCastGround(float probeDistance, out Vector3 normal, out float gap)
        {
            normal = Vector3.up;
            gap = 0f;

            Bounds bounds = _moveCapsuleCollider.bounds;
            float radius = Mathf.Min(bounds.extents.x, bounds.extents.z);
            Vector3 sphereOrigin = new(bounds.center.x, bounds.min.y + radius + GroundProbeOffset, bounds.center.z);

            if (!Physics.SphereCast(sphereOrigin, radius, Vector3.down, out RaycastHit sphereHit, probeDistance, _groundLayer)) return false;
            if (sphereHit.distance <= 0f || !IsWalkable(sphereHit.normal)) return false;

            float expectedContactHeight = bounds.min.y + radius * (1f - sphereHit.normal.y);
            // 通常の斜面接触より高い位置へ当たった場合は、頂上の縁を先取りしたものとして除外する
            if (sphereHit.point.y > expectedContactHeight + GroundSnapTolerance) return false;

            normal = sphereHit.normal;
            gap = Mathf.Max(0f, sphereHit.distance - GroundProbeOffset);
            return true;
        }


#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_visibleGizmos) return;

            int index = 0;

            foreach (var castData in _capsuleCastData)
            {
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
