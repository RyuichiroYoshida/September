using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Common;
using September.Common;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.UI;

namespace InGame.Player.Sarutobi
{
    public class AbilityGrapplingHook : NetworkBehaviour, IAfterTick, IPlayerMovementOverride, IMimicCleanup
    {
        [Header("Ability")]
        [SerializeField] private GameObject _targetUIPrefab;
        [SerializeField] private float _cooldown;
        [Header("Grappling Hook")]
        [SerializeField] private MinMaxRange _distanceRange;
        [SerializeField] private float _maxAngle;
        [SerializeField] private float _distanceReflectionRate;
        [SerializeField] private float _angleReflectionRate;
        [SerializeField] private float _wireSpeed;
        [Header("Jump")]
        [SerializeField] private float _pullingSpeed;
        [SerializeField] private Vector3 _pullLastForce;
        [SerializeField] private float _landingDuration;
        [Header("AnimClip")]
        [SerializeField] private AnimationClip _animShot;
        [SerializeField] private AnimationClip _animShotWait;
        [SerializeField] private AnimationClip _animMoveStart;
        [SerializeField] private AnimationClip _animMoveLoop;
        [SerializeField] private AnimationClip _animLanding;
        [Header("WireDisplay")]
        [SerializeField] private Transform _handSocket;
        [SerializeField] private Material _wireMaterial;
        [SerializeField] private float _wireWidth;

        private PlayerManager _playerManager;
        private PlayerMovement _playerMovement;
        private AnimationClipPlayer _clipPlayer;
        private AnimationClipPlayerManager _clipPlayerManager;
        private SplineContainer _grappleableSpline;
        private Transform _targetUI;
        private Camera _mainCamera;
        private Transform _wireCyl;

        private GrappleStateType _grappleState = GrappleStateType.ShotWait;
        private float _jumpTimer;
        private float _wireTimer;
        private Vector3 _targetPosition;
        private Vector3 _startPosition;
        private float _distanceMag;
        private bool _isLandingAnimationStarted;

        [Networked] private bool IsGrappleMoving { get; set; }
        [Networked] private Vector3 GrappleStart { get; set; }
        [Networked] private Vector3 GrappleTarget { get; set; }
        [Networked] private int GrappleStartTick { get; set; }
        [Networked] private float GrappleDuration { get; set; }
        [Networked] private Vector3 GrappleReleaseVelocity { get; set; }
        [Networked] private TickTimer GrappleReleaseTimer { get; set; }
        public bool IsGrappleMotionActive => IsGrappleMoving || !GrappleReleaseTimer.ExpiredOrNotRunning(Runner);

        [Networked] private NetworkButtons PreviousButtons { get; set; }

        [Networked, HideInInspector] public AbilityStateType AbilityState { get; private set; } = AbilityStateType.Ready;
        public event Action OnAbilityStart;
        [Networked, HideInInspector] public TickTimer Cooldown { get; set; }

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                _playerMovement = GetComponent<PlayerMovement>();
                _playerManager = GetComponent<PlayerManager>();
                _clipPlayer = GetComponent<AnimationClipPlayer>();
                _clipPlayerManager = GetComponent<AnimationClipPlayerManager>();
            }

            if (HasInputAuthority)
            {
                _playerManager = GetComponent<PlayerManager>();
                _playerMovement = GetComponent<PlayerMovement>();
                _grappleableSpline = GrapplingSpline.I.GrapplingTargetSpline;
                _targetUI = Instantiate(_targetUIPrefab).GetComponentInChildren<Image>().transform;
                _mainCamera = Camera.main;
            }

            var wireObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wireObj.name = "WireCylinder";
            _wireCyl = wireObj.transform;
            wireObj.GetComponent<Renderer>().sharedMaterial = _wireMaterial;
            if (wireObj.TryGetComponent(out Collider col)) Destroy(col);
            _wireCyl.gameObject.SetActive(false);
        }

        // IMimicCleanupの実装。
        // 擬態によってSarutobiプレハブが破棄される前に、進行中のフック移動とPrefab外へ生成した表示物を片付ける。
        public void CleanupBeforeMimicDespawn()
        {
            if (HasStateAuthority)
            {
                if (AbilityState == AbilityStateType.Active)
                    GrappleEnd();
                else
                    CancelGrappleMotion();
            }

            CleanupLocalObjects();
        }

        // 通常のDespawn経路でも、ローカルに生成したUIとワイヤーを残さない。
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            CleanupLocalObjects();
        }

        // NetworkObjectではないためRunner.DespawnではなくDestroyで破棄する。
        // CleanupBeforeMimicDespawnとDespawnedの両方から呼ばれるので、存在確認をして多重実行を許容する。
        private void CleanupLocalObjects()
        {
            if (_targetUI)
            {
                Destroy(_targetUI.gameObject);
                _targetUI = null;
            }

            if (_wireCyl)
            {
                Destroy(_wireCyl.gameObject);
                _wireCyl = null;
            }
        }

        public override void FixedUpdateNetwork()
        {
            GetInput<PlayerInput>(out var input);

            // input authority で判定
            if (HasInputAuthority && Runner.IsForward)
            {
                if (_targetUI)
                {
                    _targetUI.gameObject.SetActive(false);
                }
                else
                {
                    return;
                }

                // Abilityの状態と入力受付がされているときに判定に入る
                if (AbilityState == AbilityStateType.Ready && GameInput.I.Player.Ability1.enabled && !IsRidingExhibit())
                {
                    bool canUse = FindGrappleablePosition(out var position);
                    DisplayTargetUI(canUse, position);

                    if (canUse && input.Buttons.WasPressed(PreviousButtons, PlayerButtons.Ability1))
                    {
                        RPC_GrappleStart(position);
                        _targetUI.gameObject.SetActive(false);
                    }
                }
            }

            // state authority で移動とクールダウン
            if (HasStateAuthority)
            {
                if (AbilityState == AbilityStateType.Active)
                {
                    // 発動中Tick
                    if (_grappleState == GrappleStateType.ShotWait)
                    {
                        ShotWaitTick();
                    }
                    else if (_grappleState == GrappleStateType.Jumping)
                    {
                        JumpingTick();
                    }
                    else if (_grappleState == GrappleStateType.Landing)
                    {
                        LandingTick();
                    }

                    _playerMovement.SetRotationDirection(_targetPosition - _startPosition);
                }
                else if (AbilityState == AbilityStateType.Cooldown && Cooldown.ExpiredOrNotRunning(Runner))
                {
                    AbilityState = AbilityStateType.Ready;
                }
            }
        }

        bool IsRidingExhibit()
        {
            return _playerManager.CurrentPlayerControlState != PlayerManager.PlayerControlState.Normal;
        }

        public void AfterTick()
        {
            PreviousButtons = GetInput<PlayerInput>().GetValueOrDefault().Buttons;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        void RPC_GrappleStart(Vector3 targetPosition)
        {
            if (HasStateAuthority && (AbilityState != AbilityStateType.Ready || IsRidingExhibit())) return;
            OnAbilityStart?.Invoke();

            if (!HasStateAuthority)
            {
                _targetPosition = targetPosition + Vector3.up * 0.05f;
                _startPosition = transform.position;
                _distanceMag = Vector3.Distance(_startPosition, _targetPosition);
                return;
            }

            AbilityState = AbilityStateType.Active;
            _grappleState = GrappleStateType.Shot;
            _targetPosition = targetPosition + Vector3.up * 0.05f;
            _startPosition = transform.position;
            _distanceMag = Vector3.Distance(_startPosition, _targetPosition);
            _jumpTimer = 0;
            Shot().Forget();

            _playerManager.SetControlState(PlayerManager.PlayerControlState.ForcedControl);

            _clipPlayerManager.EnableFallMotion = false;

            Cooldown = TickTimer.CreateFromSeconds(Runner, _cooldown);

            // ボーナスカウントを更新する
            PlayerDatabase db = PlayerDatabase.Instance;
            if (!db.PlayerDataDic.TryGet(Object.InputAuthority, out SessionPlayerData playerData))
                return;
            if (playerData.CharacterType != CharacterType.Sarutobi)
                return;

            if (HasStateAuthority)
            {
                db.Server_AddGrapplingHook(Object.InputAuthority);
            }
        }

        async UniTask Shot()
        {
            var endType = await _clipPlayer.PlayClipAndWait(_animShot);

            if (endType != EndClipType.Complete)
            {
                GrappleEnd();
                return;
            }

            _grappleState = GrappleStateType.ShotWait;
            _clipPlayer.PlayClip(_animShotWait);
            RPC_DisplayWireStart();
        }

        void ShotWaitTick()
        {
            _jumpTimer += Runner.DeltaTime;

            if (_jumpTimer >= _distanceMag / _wireSpeed)
            {
                _grappleState = GrappleStateType.PreJump;
                _jumpTimer = 0;

                PreJump().Forget();
            }
        }

        async UniTask PreJump()
        {
            var endType = await _clipPlayer.PlayClipAndWait(_animMoveStart);

            if (endType != EndClipType.Complete)
            {
                GrappleEnd();
                return;
            }

            _grappleState = GrappleStateType.Jumping;
            Vector3 forward = _targetPosition - transform.position;
            forward.y = 0f;
            Quaternion rotation = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : transform.rotation;
            StartGrappleMotion(_targetPosition, _pullingSpeed, rotation * _pullLastForce);
            _clipPlayer.PlayClipLoop(_animMoveLoop);
        }

        void JumpingTick()
        {
            if (!IsGrappleMotionActive)
            {
                _grappleState = GrappleStateType.Landing;
                _jumpTimer = 0;
                _isLandingAnimationStarted = false;
                RPC_DisplayWireEnd();
                // 接地するまで移動ループを保ち、通常姿勢・落下姿勢を間に挟まない。
                LandingTick();
            }
        }

        void LandingTick()
        {
            if (!_isLandingAnimationStarted && _playerMovement.IsGroundNet)
            {
                _isLandingAnimationStarted = true;
                _clipPlayer.PlayClip(_animLanding);
                // 着地を先に再生する。同一レイヤーなら再生側が旧ループを置換する。
                _clipPlayer.StopClip(_animMoveLoop);
            }

            if (!_isLandingAnimationStarted) return;

            _jumpTimer += Runner.DeltaTime;
            if (_jumpTimer >= _landingDuration)
            {
                GrappleEnd();
            }
        }

        void GrappleEnd()
        {
            CancelGrappleMotion();
            _clipPlayer.StopClip(_animMoveLoop);
            AbilityState = AbilityStateType.Cooldown;
            _playerManager.SetControlState(PlayerManager.PlayerControlState.Normal);
            _jumpTimer = 0;
            RPC_DisplayWireEnd();
            _clipPlayerManager.EnableFallMotion = true;
        }

        private void StartGrappleMotion(Vector3 target, float speed, Vector3 releaseVelocity)
        {
            GrappleStart = _playerMovement.Rigidbody.position;
            GrappleTarget = target;
            GrappleStartTick = Runner.Tick + 1;
            GrappleDuration = Mathf.Max(Runner.DeltaTime, Vector3.Distance(GrappleStart, target) / Mathf.Max(speed, 0.01f));
            GrappleReleaseVelocity = releaseVelocity;
            GrappleReleaseTimer = default;
            IsGrappleMoving = true;
        }

        private void CancelGrappleMotion()
        {
            IsGrappleMoving = false;
            GrappleReleaseTimer = default;
        }

        public bool TryOverrideMovement(PlayerMovement movement, float deltaTime)
        {
            if (IsGrappleMoving)
            {
                float elapsed = (Runner.Tick - GrappleStartTick) * deltaTime;
                if (elapsed < 0f) return false;

                if (elapsed < GrappleDuration)
                {
                    float progress = Mathf.Clamp01((elapsed + deltaTime) / GrappleDuration);
                    Vector3 nextPosition = Vector3.Lerp(GrappleStart, GrappleTarget, progress);
                    Vector3 velocity = (nextPosition - movement.Rigidbody.position) / deltaTime;
                    if (movement.Rigidbody.useGravity) velocity -= Physics.gravity * deltaTime;
                    movement.Rigidbody.linearVelocity = velocity;

                    Vector3 direction = GrappleTarget - GrappleStart;
                    direction.y = 0f;
                    if (direction.sqrMagnitude > 0.0001f)
                        movement.Rigidbody.rotation = Quaternion.RotateTowards(
                            movement.Rigidbody.rotation, Quaternion.LookRotation(direction), movement.RotationSpeed * deltaTime);
                    movement.SetRotationDirection(direction);
                    movement.ApplyExternalMovementState(velocity, Vector3.zero, Vector3.zero);
                    return true;
                }

                IsGrappleMoving = false;
                movement.Rigidbody.linearVelocity = GrappleReleaseVelocity;
                movement.ResetFlyingVelocity();
                movement.ResetExternalGroundState();
                GrappleReleaseTimer = TickTimer.CreateFromSeconds(Runner, 0.2f);
            }

            if (GrappleReleaseTimer.IsRunning)
            {
                Vector3 velocity = movement.Rigidbody.linearVelocity;
                movement.ApplyExternalMovementState(
                    velocity,
                    Vector3.up * velocity.y,
                    Vector3.ProjectOnPlane(velocity, Vector3.up));
                if (!GrappleReleaseTimer.ExpiredOrNotRunning(Runner)) return true;
                GrappleReleaseTimer = default;
            }

            return false;
        }

        bool FindGrappleablePosition(out Vector3 position)
        {
            position = Vector3.zero;
            if (!HasInputAuthority || !_grappleableSpline || !_grappleableSpline.Splines.Any()) return false;

            var splines = _grappleableSpline.Splines;

            // 粗い間隔で最もポイントが低い点を見つける
            Spline minSpline = null;
            float minT = float.MaxValue;
            float minPoint = float.MaxValue;

            foreach (var t1 in splines)
            {
                if (!GetMinPoint(t1, new MinMaxRange(0, 1), out var t, out _, out var newPoint)) continue;

                if (minPoint > newPoint)
                {
                    minSpline = t1;
                    minT = t;
                    minPoint = newPoint;
                }
            }

            if (minSpline == null) return false;

            // そのポイント周辺で最もポイントが低い点を探す
            if (!GetMinPoint(minSpline, new MinMaxRange(minT - 0.05f, minT + 0.05f), out _, out var ansPosition,
                    out _)) return false;

            position = ansPosition;

            return true;
        }

        /// <summary> pointの評価値を取得 </summary>
        bool GetEvaluatePoint(Vector3 position, out float point)
        {
            point = float.MaxValue;
            Vector3 posDiff = position - transform.position;

            // 距離判定
            if (posDiff.sqrMagnitude < _distanceRange.Min * _distanceRange.Min || posDiff.sqrMagnitude > _distanceRange.Max * _distanceRange.Max)
            {
                return false;
            }

            // 角度判定
            float angle = Vector3.Angle(_mainCamera.transform.forward, position - _mainCamera.transform.position);

            if (angle > _maxAngle)
            {
                return false;
            }

            // 障害物判定　カプセルの中心から同じRadiusの球でTargetまでCast
            Vector3 halfHeight = (_playerMovement.MoveCapsuleCollider.height * 0.5f + _playerMovement.MoveCapsuleCollider.radius) * Vector3.up;

            if (Physics.CheckCapsule(transform.position + halfHeight, position + halfHeight, _playerMovement.MoveCapsuleCollider.radius, _playerMovement.GroundLayer))
            {
                return false;
            }

            point = posDiff.magnitude * _distanceReflectionRate + angle * _angleReflectionRate;

            return true;
        }

        bool GetMinPoint(Spline spline, MinMaxRange tRange, out float t, out Vector3 position, out float point, int resolution = 10, int iterations = 2)
        {
            t = -1;
            position = Vector3.zero;
            point = float.MaxValue;

            for (int i = 0; i < iterations; i++)
            {
                for (int j = 0; j < resolution; j++)
                {
                    float currentT = tRange.Min + (tRange.Max - tRange.Min) * (j / (float)resolution);
                    if (!spline.Evaluate(currentT, out var pos, out _, out _)) continue;
                    if (!GetEvaluatePoint(pos, out var newPoint)) continue;

                    if (point > newPoint)
                    {
                        t = currentT;
                        position = pos;
                        point = newPoint;
                    }
                }

                if (t < 0) return false;

                float tRangeIntervalHalf = (tRange.Max - tRange.Min) * 0.5f;
                tRange = new(t - tRangeIntervalHalf, t + tRangeIntervalHalf);
            }

            return point < float.MaxValue;
        }

        void DisplayTargetUI(bool display, Vector3 worldPos)
        {
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

            if (!display || screenPos.z < 0)
            {
                _targetUI.gameObject.SetActive(false);
                return;
            }

            _targetUI.gameObject.SetActive(true);
            _targetUI.position = screenPos;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        void RPC_DisplayWireStart()
        {
            // 擬態解除とRPCの到着が重なった場合、ワイヤーはすでに破棄されている。
            if (!_wireCyl) return;

            _wireTimer = 0;
            _wireCyl.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_wireCyl && _wireCyl.gameObject.activeSelf)
            {
                _wireTimer += Time.deltaTime;
                float t = Math.Clamp(_wireTimer * _wireSpeed / _distanceMag, 0, 1);
                SetWirePosition(Vector3.Lerp(_startPosition, _targetPosition, t));
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        void RPC_DisplayWireEnd()
        {
            // CleanupLocalObjectsによる破棄後でも安全に終了できるようにする。
            if (_wireCyl)
                _wireCyl.gameObject.SetActive(false);
        }

        void SetWirePosition(Vector3 otherPos)
        {
            // Despawn直前・直後は生成物や手の参照が先に無効になる場合がある。
            if (!_wireCyl || !_handSocket) return;

            var dir = otherPos - _handSocket.position;
            var len = dir.magnitude;

            if (len < 1e-5f)
            {
                _wireCyl.gameObject.SetActive(false);
                return;
            }

            // transform の変更
            _wireCyl.position = (otherPos + _handSocket.position) * 0.5f;
            _wireCyl.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            var sy = len * 0.5f;
            _wireCyl.localScale = new Vector3(_wireWidth, sy, _wireWidth);
        }

        public enum AbilityStateType
        {
            Ready,
            Active,
            Cooldown
        }

        private enum GrappleStateType
        {
            Shot,
            ShotWait,
            PreJump,
            Jumping,
            Landing
        }
    }
}
