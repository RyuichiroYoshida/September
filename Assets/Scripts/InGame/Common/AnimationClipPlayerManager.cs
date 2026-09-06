using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Health;
using InGame.Player;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

namespace InGame.Common
{
    public class AnimationClipPlayerManager : NetworkBehaviour
    {
        [SerializeField] private AnimationClipPlayer _animationClipPlayer;
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private AnimationClip _jumpOver;
        [SerializeField] private float _jumpOverDuration = 0.2f;
        [SerializeField] private AnimationClip _fallDown;
        [SerializeField] private AnimationClip _faint;  // 追加: 気絶アニメーション
        [SerializeField] private AnimationClip _getUp;   // 追加: 起き上がりアニメーション
        [SerializeField] private PlayerManager _playerManager; // 追加: PlayerManager参照
        [SerializeField] private PlayerHealth _playerHealth;

        [Header("Roll Evasion")]
        [SerializeField] private AnimationClip _rollEvasion;
        [SerializeField, Range(0f, 1f)] private float _evasionInTime = 0.08f;
        [SerializeField] private AnimationCurve _evasionInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float _evasionOutTime = 0.10f;
        [SerializeField] private AnimationCurve _evasionOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private EvasionData _evasionData;

        // ▼ 追加: ブレンド設定
        [Header("Loco Blend")]
        [SerializeField] private float _locoBlendSpeed;
        [Header("Fall Blend")]
        [SerializeField, Range(0f, 1f)] private float _fallInTime = 0.20f;
        [SerializeField] private AnimationCurve _fallInCurve = null;   // null の場合は線形扱い
        [SerializeField, Range(0f, 1f)] private float _landOutTime = 0.12f;
        [SerializeField] private AnimationCurve _landOutCurve = null;
        [Header("Loco Playback Rate")]
        [SerializeField, Tooltip("歩き/走りクリップに焼かれたルート移動量 (averageSpeed) から基準速度を自動算出する。焼かれていないクリップは下の手入力値を使う")]
        private bool _useClipAverageSpeed = true;
        [SerializeField, Tooltip("歩きクリップが 1 倍速で進む速さ (m/s)。自動算出できない場合に使用")]
        private float _walkAnimSpeed = 1f;
        [SerializeField, Tooltip("走りクリップが 1 倍速で進む速さ (m/s)。自動算出できない場合に使用")]
        private float _runAnimSpeed = 2f;
        private float _walkBaseSpeed;
        private float _runBaseSpeed;

        [Header("被ダメ")]
        [SerializeField] private AnimationClip _hitReactionClip = null;
        [Header("気絶")]
        [SerializeField, Range(0f, 1f)] private float _overrideInTime = 0.08f;
        [SerializeField] private AnimationCurve _overrideInCurve = null;
        [SerializeField, Range(0f, 1f)] private float _overrideOutTime = 0.10f;
        [SerializeField] private AnimationCurve _overrideOutCurve = null;
        [SerializeField, Range(0f, 1f)] private float _getUpBlendTime = 0.12f;
        [SerializeField] private AnimationCurve _getUpBlendCurve = null;
        [SerializeField, Tooltip("DownとGetUpの向きを自動で揃える。OFFの場合は下の手動角度だけを使用する")]
        private bool _isGetUpAutoRotationEnabled = true;
        [SerializeField, Range(-180f, 180f), Tooltip("GetUp開始時に加える水平回転角度 (度)")]
        private float _getUpRotationOffsetY;
        private bool _hardOverride = false;

        private bool _isFadingOutFall = false;       // 着地フェード多重起動防止
        private CancellationTokenSource _overrideCts;
        private bool _isFainting = false;          // 気絶中フラグ（多重起動防止）
        private Transform _visualRoot;
        private Quaternion _visualRootBaseLocalRotation;
        private float _locoWeight;
        private CancellationTokenSource _jumpOverTokenSrc;
        private CancellationTokenSource _rollEvasionTokenSrc;

        private void Start()
        {
            ResolveLocoBaseSpeeds();

            _playerManager.ObserveEveryValueChanged(x => x.IsStun)
                .Subscribe(isStun =>
                {
                    if (isStun && !_isFainting)
                    {
                        _isFainting = true;
                        TriggerFaint();
                    }
                    else
                    {
                        _isFainting = false;
                    }
                }).AddTo(this);

            _playerHealth.OnHitTaken += (hitData) =>
            {
                if (!_playerManager.IsStun && hitData.HitActionType.IsDamage())
                {
                    //被ダメのアニメーション再生
                    _animationClipPlayer.PlayClip(_hitReactionClip);
                }
            };

            _playerMovement.OnStartVault += () =>
            {
                if (!_hardOverride)
                {
                    RPC_TriggerVault();
                }
            };

            _playerMovement.UpdateAsObservable()
                .Select(_ => _playerMovement.IsGroundNet || !EnableFallMotion) // EnableFallMotionが偽なら落下モーションを即時解除
                .DistinctUntilChanged().Subscribe(x => SetFallAnim(x)).AddTo(this);

            // 回避開始 Tick の変化で発火する。Networked 状態由来なので、ホスト・予測中のクライアント・リモート表示の全てが同じ経路で再生される
            // 回避中でなければ 0 に落とす。終了後も StartTick は残るため、途中参加時に過去の回避を再生してしまうのを防ぐ
            _playerMovement.UpdateAsObservable()
                .Select(_ => _playerMovement.IsEvading ? _playerMovement.EvasionStartTick : 0)
                .DistinctUntilChanged()
                .Where(startTick => startTick > 0)
                .Subscribe(_ =>
                {
                    if (!_hardOverride) TriggerEvasion(_playerMovement.EvasionDuration).Forget();
                })
                .AddTo(this);
        }

        private void SetFallAnim(bool isGround)
        {
            if (!isGround
                && EnableFallMotion
                && !_animationClipPlayer.IsPlayingTargetClip(_jumpOver)
                && !_animationClipPlayer.IsPlayingTargetClip(_fallDown, includeIsEnded: true))
            {
                _animationClipPlayer.PlayOnLayer(_fallDown, loop: true);
                _animationClipPlayer.SetLayerWeight(LayerInfo.LayerType.TopLayer, 0f);
                _animationClipPlayer.BlendLayerWeight(
                    LayerInfo.LayerType.TopLayer,
                    1f,
                    new LayerInfo.Blend
                    {
                        BlendTime = _fallInTime,
                        BlendCurve = _fallInCurve
                    }
                ).Forget();

                _isFadingOutFall = false;
            }

            // ===== 着地の終了（1→0へブレンドしてからTopLayer解除） =====
            if (!isGround || !_animationClipPlayer.IsPlayingTargetClip(_fallDown, includeIsEnded: true)) return;
            if (_isFadingOutFall) return;
            _isFadingOutFall = true;
            //topレイヤーにFallアニメーションがある場合は除外する
            FadeOutAndClearFall().Forget();
        }

        /// <summary>
        /// TopLayerで何かアクティブなクリップが再生されているかをチェック
        /// </summary>
        private bool HasActiveTopLayerClip()
        {
            // 管理対象のアニメーションクリップをチェック
            return _animationClipPlayer.IsPlayingTargetClip(_jumpOver) ||
                   _animationClipPlayer.IsPlayingTargetClip(_fallDown, includeIsEnded: true) ||
                   _animationClipPlayer.IsPlayingTargetClip(_faint) ||
                   _animationClipPlayer.IsPlayingTargetClip(_getUp) ||
                   _animationClipPlayer.IsPlayingTargetClip(_rollEvasion);
        }

        [Networked] public bool EnableFallMotion { get; set; } = true;

        /// <summary>
        /// 歩き/走りクリップの基準速度を決める。クリップに焼かれたルート移動量を優先し、
        /// 無い場合や自動算出を切っている場合は Inspector の手入力値を使う。
        /// </summary>
        private void ResolveLocoBaseSpeeds()
        {
            if (!_useClipAverageSpeed || _animationClipPlayer == null)
            {
                _walkBaseSpeed = _walkAnimSpeed;
                _runBaseSpeed = _runAnimSpeed;
                return;
            }

            _walkBaseSpeed = LocoAnimSpeedSource.Resolve(_animationClipPlayer.WalkClip, _walkAnimSpeed, nameof(_walkAnimSpeed), this);
            _runBaseSpeed = LocoAnimSpeedSource.Resolve(_animationClipPlayer.RunClip, _runAnimSpeed, nameof(_runAnimSpeed), this);
        }

        private void LateUpdate()
        {
            if (!_animationClipPlayer) return;

            var maxSpeed = _playerMovement.DashMoveSpeed;
            var walkSpeed = _playerMovement.WalkSpeed;
            var moveSpeed = _playerMovement.NetworkVelocity.magnitude;
            var wishWeight = (moveSpeed <= walkSpeed)
                ? Mathf.InverseLerp(0f, walkSpeed, moveSpeed)         // 0..1
                : Mathf.InverseLerp(walkSpeed, maxSpeed, moveSpeed) + 1f; // 1..2
            if (Mathf.Abs(wishWeight) < 1e-3f) wishWeight = 0f;
            // weight を遷移させる
            float deltaWeight = _locoBlendSpeed * Time.deltaTime;
            _locoWeight = Mathf.Abs(_locoWeight - wishWeight) <= deltaWeight ? wishWeight : _locoWeight < wishWeight ? _locoWeight + deltaWeight : _locoWeight - deltaWeight;
            _animationClipPlayer.SetLocoWeight(Mathf.Clamp(_locoWeight, 0f, 2f));

            var velocity = _playerMovement.NetworkVelocity;
            velocity.y = 0;
            var speed = velocity.magnitude;
            // 速度を歩き〜走りの割合に変換
            var speedRate = Mathf.InverseLerp(walkSpeed, maxSpeed, speed);

            // その割合でアニメーション基準速度 (1 倍速で進む m/s) を補間
            var baseSpeed = Mathf.Lerp(
                _walkBaseSpeed,
                _runBaseSpeed,
                speedRate);

            var playbackRate = baseSpeed > 0f ? speed / baseSpeed : 0f;

            _animationClipPlayer.SetLocoPlaybackRate(playbackRate);
            // 強制上書き中は、非ループクリップが終端に到達しても倒れた姿勢を保持する。
            if (!_hardOverride && !HasActiveTopLayerClip())
            {
                _animationClipPlayer.SetLayerWeight(LayerInfo.LayerType.TopLayer, 0f);
            }
            //
            // Debug.LogError($"IsGroundNet:{_playerMovement.IsGroundNet}, IsPlaying:{_animationClipPlayer.IsPlayingTargetClip(_fallDown)}");
            // if (_playerMovement.IsGroundNet && _animationClipPlayer.IsPlayingTargetClip(_fallDown))
            // {
            //     SetFallAnim(true);
            // }
        }

        public void TriggerFaint()
        {
            // すでに実行中なら差し替え（再発動）
            _overrideCts?.Cancel();
            ClearGetUpVisualCorrection();
            _ = PlayFaintSequenceAsync();
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_TriggerVault()
        {
            if (!_hardOverride)
            {
                _ = TriggerVault();
            }
        }

        public async UniTask TriggerVault()
        {

            if (_jumpOverTokenSrc != null)
            {
                _jumpOverTokenSrc.Cancel();
                _jumpOverTokenSrc.Dispose();
            }
            _animationClipPlayer.PlayOnLayer(_jumpOver);

            _jumpOverTokenSrc = new CancellationTokenSource();
            // ジャンプオーバークリップの長さだけ待機（速度変更を考慮しない場合は length をそのまま使用）
            if (_jumpOver && _jumpOver.length > 0f)
            {
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_jumpOverDuration), cancellationToken: _jumpOverTokenSrc.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
            _animationClipPlayer.PlayOnLayer(null);
            if (!_playerMovement.IsGroundNet) SetFallAnim(false);
        }

        /// <param name="rollDuration"> 回避全体の所要時間 (秒)。クリップ長を割ってこの秒数に収まる再生速度にする </param>
        public async UniTask TriggerEvasion(float rollDuration)
        {
            if (rollDuration <= 0f || !_rollEvasion || _rollEvasion.length <= 0f)
                return;

            if (_rollEvasionTokenSrc != null)
            {
                _rollEvasionTokenSrc.Cancel();
                _rollEvasionTokenSrc.Dispose();
            }
            // クリップ長基準の再生速度にすることで、移動時間とモーションの長さが一致する
            _animationClipPlayer.PlayOnLayer(_rollEvasion, LayerInfo.LayerType.FullBody, _rollEvasion.length / rollDuration);

            _rollEvasionTokenSrc = new CancellationTokenSource();

            try
            {
                await _animationClipPlayer.BlendLayerWeight(
                    LayerInfo.LayerType.FullBody,
                    1f,
                    new LayerInfo.Blend { BlendTime = _evasionInTime, BlendCurve = _evasionInCurve },
                    _rollEvasionTokenSrc.Token
                );

                // ブレンドアウトが移動終了と同時に完了するよう、残り時間だけ待つ
                float waitTime = Mathf.Max(0f, rollDuration - (_evasionInTime + _evasionOutTime));
                await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: _rollEvasionTokenSrc.Token);

                await _animationClipPlayer.BlendLayerWeight(
                    LayerInfo.LayerType.FullBody,
                    0f,
                    new LayerInfo.Blend { BlendTime = _evasionOutTime, BlendCurve = _evasionOutCurve },
                    _rollEvasionTokenSrc.Token
                );
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _animationClipPlayer.PlayOnLayer(null, LayerInfo.LayerType.FullBody);
        }

        /// <summary>強制解除（リスポーン等）</summary>
        public void ForceClearOverride()
        {
            _overrideCts?.Cancel();
            _hardOverride = false;
            _animationClipPlayer.PlayOnLayer(null);
            _animationClipPlayer.SetLayerWeight(LayerInfo.LayerType.TopLayer, 0f);
            ClearGetUpVisualCorrection();
            _overrideCts = null;
        }

        private async UniTaskVoid PlayFaintSequenceAsync()
        {
            _hardOverride = true;
            CaptureVisualRootBasePose();

            var cts = new CancellationTokenSource();
            _overrideCts = cts;
            try
            {
                // 気絶へフェードイン（TopLayer=1）
                if (_faint != null)
                {
                    _animationClipPlayer.PlayOnLayer(_faint);
                }
                _animationClipPlayer.SetLayerWeight(LayerInfo.LayerType.TopLayer, 0f);

                await _animationClipPlayer.BlendLayerWeight(
                    LayerInfo.LayerType.TopLayer,
                    1f,
                    new LayerInfo.Blend { BlendTime = _overrideInTime, BlendCurve = _overrideInCurve },
                    cts.Token
                );

                // 倒れた姿勢を維持し、気絶終了時刻から起き上がり時間を逆算する。
                float getUpDuration = _getUp ? _getUp.length : 0f;
                await WaitUntilRemainingStunTimeAsync(getUpDuration, cts.Token);

                // 気絶時間が短く、起き上がり開始時点ですでに終了間際の場合は、終了時刻に収まる速度にする。
                if (_playerManager.IsStun && _getUp)
                {
                    bool hasDownForward = TryGetHumanoidHorizontalForward(out var downForward);
                    float remainingStunTime = _playerManager.GetRemainingStunTime;
                    float getUpSpeed = remainingStunTime > 0f && _getUp.length > 0f
                        ? _getUp.length / remainingStunTime
                        : 1f;
                    var getUpBlendTask = _animationClipPlayer.CrossFadeOnTopLayerAsync(
                        _getUp,
                        getUpSpeed,
                        _getUpBlendTime,
                        _getUpBlendCurve,
                        cts.Token);

                    await ApplyGetUpVisualCorrectionAsync(downForward, hasDownForward, cts.Token);

                    await getUpBlendTask;
                    _animationClipPlayer.PlayOnLayer(_getUp);
                    if (_getUp.length > 0f)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(_getUp.length), cancellationToken: cts.Token);
                    }
                }

                await WaitUntilStunEndedAsync(cts.Token);

                // フェードアウトして解除
                await _animationClipPlayer.BlendLayerWeight(
                    LayerInfo.LayerType.TopLayer,
                    0f,
                    new LayerInfo.Blend { BlendTime = _overrideOutTime, BlendCurve = _overrideOutCurve },
                    cts.Token
                );

                if (!ReferenceEquals(_overrideCts, cts)) return;
                ClearGetUpVisualCorrection();
                _animationClipPlayer.PlayOnLayer(null);
                _hardOverride = false;
                _overrideCts = null;
            }
            catch (OperationCanceledException)
            {
                // 再発動や強制解除でキャンセルされた
            }
            finally
            {
                // 途中キャンセル時も確実に状態を畳む
                if (cts.IsCancellationRequested && ReferenceEquals(_overrideCts, cts))
                {
                    _animationClipPlayer.PlayOnLayer(null);
                    _animationClipPlayer.SetLayerWeight(LayerInfo.LayerType.TopLayer, 0f);
                    ClearGetUpVisualCorrection();
                    _hardOverride = false;
                    if (ReferenceEquals(_overrideCts, cts)) _overrideCts = null;
                }
                cts.Dispose();
            }
        }

        private void CaptureVisualRootBasePose()
        {
            var animator = _animationClipPlayer != null ? _animationClipPlayer.Animator : null;
            _visualRoot = animator != null ? animator.transform : null;
            if (_visualRoot == null || _visualRoot == transform)
            {
                _visualRoot = null;
                return;
            }

            _visualRootBaseLocalRotation = _visualRoot.localRotation;
        }

        /// <summary>
        /// Humanoidの身体の水平方向を取得する。DownとGetUpの向きを揃える基準にする。
        /// </summary>
        private bool TryGetHumanoidHorizontalForward(out Vector3 horizontalForward)
        {
            horizontalForward = default;
            var animator = _animationClipPlayer != null ? _animationClipPlayer.Animator : null;
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                return false;
            }

            horizontalForward = Vector3.ProjectOnPlane(animator.bodyRotation * Vector3.forward, Vector3.up);
            if (horizontalForward.sqrMagnitude < 0.0001f)
            {
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null)
                {
                    horizontalForward = Vector3.ProjectOnPlane(hips.forward, Vector3.up);
                }
            }

            if (horizontalForward.sqrMagnitude < 0.0001f)
            {
                horizontalForward = default;
                return false;
            }

            horizontalForward.Normalize();
            return true;
        }

        /// <summary>
        /// GetUpを1フレーム評価した後、モデル全体の水平向きを補正する。
        /// Foot IKやプレイヤー本体のTransformは変更しない。
        /// </summary>
        private async UniTask ApplyGetUpVisualCorrectionAsync(
            Vector3 downForward,
            bool hasDownForward,
            CancellationToken token)
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);

            if (_visualRoot == null) return;

            float rotationOffsetY = _getUpRotationOffsetY;
            if (_isGetUpAutoRotationEnabled && hasDownForward && TryGetHumanoidHorizontalForward(out var getUpForward))
            {
                rotationOffsetY += Vector3.SignedAngle(getUpForward, downForward, Vector3.up);
            }

            if (Mathf.Abs(rotationOffsetY) > Mathf.Epsilon)
            {
                var rotationOffset = Quaternion.AngleAxis(rotationOffsetY, Vector3.up);
                _visualRoot.rotation = rotationOffset * _visualRoot.rotation;
            }
        }

        private void ClearGetUpVisualCorrection()
        {
            if (_visualRoot != null)
            {
                _visualRoot.localRotation = _visualRootBaseLocalRotation;
            }

            _visualRoot = null;
        }

        /// <summary>
        /// Fusionで同期された気絶タイマーが、指定した残り時間になるまで待機する。
        /// </summary>
        private async UniTask WaitUntilRemainingStunTimeAsync(float targetRemainingTime, CancellationToken token)
        {
            while (_playerManager.IsStun && _playerManager.GetRemainingStunTime > targetRemainingTime)
            {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
            }
        }

        /// <summary>
        /// ネットワーク上の気絶状態が解除されるまで待機する。
        /// </summary>
        private async UniTask WaitUntilStunEndedAsync(CancellationToken token)
        {
            while (_playerManager.IsStun)
            {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
            }
        }


        // 1→0へブレンド完了後にTopLayerを外す
        private async UniTaskVoid FadeOutAndClearFall()
        {
            await _animationClipPlayer.BlendLayerWeight(
                LayerInfo.LayerType.TopLayer,
                0f,
                new LayerInfo.Blend
                {
                    BlendTime = _landOutTime,
                    BlendCurve = _landOutCurve
                }
            );

            if (_animationClipPlayer.IsPlayingTargetClip(_fallDown, includeIsEnded: true, includeZeroWeight: true) &&
                _animationClipPlayer.GetTargetLayerWeight(LayerInfo.LayerType.TopLayer) < 0.001f)
                _animationClipPlayer.PlayOnLayer(null);
        }
    }
}
