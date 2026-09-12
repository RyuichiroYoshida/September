using System;
using System.Collections.Generic;
using Fusion;
using InGame.Common;
using InGame.Health;
using September.Common;
using September.InGame.Common;
using September.InGame.Common.Stats;
using September.InGame.Effect;
using UnityEngine;

namespace InGame.Player.Ability
{
    [Serializable]
    public class AbilityNormalAttack : AbilityBase
    {
        [Header("通常攻撃")]
        [SerializeField] protected int _attackDamage = 10;
        [Header("鬼状態の通常攻撃")]
        [SerializeField] protected int _ogreAttackDamage = 15;
        [Header("ヒットチェック開始フレーム")]
        [SerializeField] protected int _startHitCheckFrame = 17;
        [Header("ヒットチェック終了フレーム")]
        [SerializeField] protected int _endHitCheckFrame = 21;
        [Header("攻撃終了フレーム")]
        [SerializeField] private int _endAttackFrame = 22;
        [Header("ヒットエフェクト")]
        [SerializeField] protected EffectType _hitEffect = EffectType.HitNormal;

        [Header("参照")]
        [SerializeField] private AnimationClip _normalAttackAnimationClip;
        [SerializeField] private AnimationClipPlayer _animationClipPlayer;

        // 攻撃中は方向を固定するため、旧オートエイム設定は無効化する。
        /*
        [Header("自動エイム設定")]
        [SerializeField] private bool _enableAutoAim = true;
        */
        [SerializeField] protected float _moveForwardSpeed = 2f;
        /*
        [Header("どれくらいの距離までの敵を狙って攻撃するか")]
        [SerializeField] private float _searchRadius = 2f;
        */

        [Header("Hit Box 設定")]
        [SerializeField] private Vector3 _boxHalfExtents = new Vector3(0.45f, 0.85f, 0.45f);
        [SerializeField] private Vector3 _boxLocalOffset = new Vector3(0f, 0.9f, 0.6f);
        [SerializeField, Tooltip("前方へ掃引する距離")]
        private float _boxCastDistance = 1.0f;
        [SerializeField] private LayerMask _hitLayer = ~0;
        [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore;
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];
        protected readonly HashSet<Collider> _alreadyHit = new HashSet<Collider>();

        [Header("Debug Draw")]
        [SerializeField] private Color _debugHitBoxColor = new Color(0f, 0.6f, 1f, 1f);
        [SerializeField, Tooltip("Debug 線の表示秒数。0 なら 1 フレームだけ")]
        private float _debugDrawDuration = 1f; // 例: 0.05f

        [Header("ビルドシステム関連の参照")]
        [SerializeField] protected BuildGenerator _buildGenerator;
        [SerializeField] PlayerStatus _playerStatus;

        // 変換後のTickオフセット
        protected int _startHitTick, _endHitTick, _endAttackTick;

        // 攻撃開始Tick
        protected int _attackStartTick = -1;

        /*
        // 最も近い敵のTransform
        protected Transform _closestEnemyTransform;
        */
        protected PlayerMovement _playerMovement;
        protected EffectSpawner _effectSpawner;
        protected Vector3 _attackDirection;

        protected override void OnStart()
        {
            if (_animationClipPlayer && _normalAttackAnimationClip)
            {
                _animationClipPlayer.PlayClip(_normalAttackAnimationClip);
            }

            if (!_effectSpawner)
                _effectSpawner = StaticServiceLocator.Instance.Get<EffectSpawner>();

            _startHitTick = FrameToTick(_startHitCheckFrame);
            _endHitTick = FrameToTick(_endHitCheckFrame);
            _endAttackTick = FrameToTick(_endAttackFrame);

            _attackStartTick = Runner != null ? Runner.Tick : 0;

            // PlayerMovementコンポーネントを取得
            _playerMovement = Parameter.Owner.GetComponent<PlayerMovement>();
            _attackDirection = _playerInput.DesiredLookDirection;
            _attackDirection.y = 0f;
            if (_attackDirection.sqrMagnitude <= Mathf.Epsilon)
                _attackDirection = Parameter.Owner.transform.forward;

            _playerMovement.SetRotationImmediately(_attackDirection);

            /*
            // 自動エイムが有効な場合のみ最も近い敵を取得
            if (_enableAutoAim)
            {
                _closestEnemyTransform = GetClosestEnemy();
            }
            */

            _startHitTick = FrameToTick(_startHitCheckFrame);
            _playerMovement.IgnoreMoveInput = true;
            _playerMovement.IgnoreEvasionInput = true;

#if UNITY_EDITOR
            if (_buildGenerator & _playerStatus)
                Debug.Log("ビルドシステムが正常に動きます");
            else
                Debug.LogWarning("ビルドに関する参照がないためビルドシステムが正常に動作しません\nPlayerAbilityManager.csを確認してください");
#endif
        }

        public override void OnUpdateLocal(float deltaTime, GameObject owner)
        {
            if (HitboxDebugUtility.IsDebugModeEnabled)
            {
                var t = owner.transform;
                if (!t) return;

                // BoxCast の原点と向き
                var origin = t.position + t.TransformVector(_boxLocalOffset);
                var dir = t.forward;
                var rot = t.rotation;

                if (HitboxDebugUtility.IsDebugModeEnabled)
                {
                    // 表示したい位置を1つだけ選ぶ
                    // BoxCast なら「終了位置だけ」見せるのが直感的
                    var boxCenter = _boxCastDistance > 0f
                        ? origin + dir.normalized * _boxCastDistance   // 終了側
                        : origin;                                       // 距離0なら開始側

                    HitboxDebugUtility.DrawBoxOneFrame(
                        boxCenter,
                        _boxHalfExtents,
                        rot,
                        _debugHitBoxColor   // 好きな色に
                    );
                }

            }

        }

        public override void SetPlayerComponent(GameObject player)
        {
            _animationClipPlayer = player.GetComponentInChildren<AnimationClipPlayer>();
            _buildGenerator = player.GetComponentInChildren<BuildGenerator>();
            _playerStatus = player.GetComponentInChildren<PlayerStatus>();
        }

        protected virtual void OnHitEnemy(Collider hitInfo, Vector3 hitPosition)
        {
            if (hitInfo.GetComponentInParent<NetworkObject>() == Parameter.Owner) return;
            var damageable = hitInfo.GetComponentInParent<IDamageable>();
            if (damageable == null) return;

            // 鬼状態かどうかでダメージを変更
            int damage = GetAttackDamage();

            var hitData = new HitData(
                HitActionType.Damage,
                damage,
                Parameter.Owner.InputAuthority,
                damageable.OwnerPlayerRef);
            damageable.TakeHit(ref hitData);
            _buildGenerator?.UpdateBuild(BuildRouteType.AttackPower);

            //エフェクトの再生
            _effectSpawner.RequestPlayOneShotEffect(_hitEffect, hitInfo.ClosestPoint(hitInfo.bounds.ClosestPoint(hitPosition)), Quaternion.identity);
        }

        /// <summary>
        /// 鬼状態かどうかでダメージを決定する
        /// </summary>
        protected virtual int GetAttackDamage()
        {
            try
            {
                var playerDatabase = PlayerDatabase.Instance;
                if (playerDatabase == null) return _attackDamage;

                if (playerDatabase.PlayerDataDic.TryGet(Parameter.Owner.InputAuthority, out SessionPlayerData playerData))
                {
                    // ビルドの上昇分を加算して計算
                    return (playerData.IsOgre ? _ogreAttackDamage : _attackDamage) + (_playerStatus ? (int)_playerStatus.AttackDamage : 0);
                }
            }
            catch (System.Exception)
            {
                // エラーが発生した場合は通常ダメージを返す
            }

            return _attackDamage;
        }

        protected void CastAndApplyHits()
        {
            var t = Parameter.Owner.transform;

            // BoxCast の原点と向き
            var origin = t.position + t.TransformVector(_boxLocalOffset);
            var dir = t.forward;
            var rot = t.rotation;

            // 掃引（NonAlloc で GC しない）
            int hitCount = Physics.BoxCastNonAlloc(
                origin,
                _boxHalfExtents,
                dir,
                _hitBuffer,
                rot,
                _boxCastDistance,
                _hitLayer,
                _triggerInteraction
            );

            for (int i = 0; i < hitCount; i++)
            {
                var hit = _hitBuffer[i];
                var col = hit.collider;
                if (col == null) continue;

                // 自分自身除外
                if (col.GetComponentInParent<NetworkObject>() == Parameter.Owner) continue;

                // 二度当たり防止
                if (_alreadyHit.Contains(col)) continue;
                _alreadyHit.Add(col);

                // ヒット位置が 0 のことがあるのでフォールバック
                var hitPos = hit.point;
                if (hitPos == Vector3.zero)
                    hitPos = origin + dir * Mathf.Max(0.1f, _boxCastDistance * 0.5f);

                OnHitEnemy(col, hitPos);
            }
            // バッファ初期化（念のため）
            Array.Clear(_hitBuffer, 0, hitCount);
        }

        protected override void OnUpdate(float deltaTime)
        {
            //if (_isStopWhenAttack && _playerMovement) _playerMovement.Stop();
            int now = Runner.Tick;
            int elapsed = now - _attackStartTick;

            _playerMovement.SetRotationDirection(_attackDirection);

            /*
            // 最も近い敵の方向を向く
            if (_closestEnemyTransform != null && _playerMovement != null)
            {
                Vector3 directionToEnemy = (_closestEnemyTransform.position - Parameter.Owner.transform.position).normalized;
                directionToEnemy.y = 0; // Y軸は無視して水平方向のみ

                if (directionToEnemy.magnitude > 0.1f)
                {
                    _playerMovement.SetRotationDirection(directionToEnemy);
                }
            }
            */

            // ヒット窓
            bool inWindow = elapsed >= _startHitTick && elapsed < _endHitTick;

            if (inWindow)
            {
                CastAndApplyHits(); // ← ここで BoxCast 実行
            }
            else
            {
                // 窓を抜けたら次の攻撃に備えてクリア
                if (elapsed >= _endHitTick && _alreadyHit.Count > 0)
                    _alreadyHit.Clear();
            }

            _playerMovement.AddForce(Parameter.Owner.transform.forward * _moveForwardSpeed);

            // 攻撃終了
            if (elapsed >= _endAttackTick)
            {
                _playerMovement.IgnoreMoveInput = false;
                _playerMovement.IgnoreEvasionInput = false;
                RequestEndAbility();
            }
        }

        protected int FrameToTick(int f)
        {
            float fps = _normalAttackAnimationClip ? _normalAttackAnimationClip.frameRate : 60f;
            float dt = Runner != null ? Runner.DeltaTime : Time.fixedDeltaTime;
            return Mathf.RoundToInt((f / fps) / dt);
        }

        /*
        private Transform GetClosestEnemy()
        {
            try
            {
                var inGameManager = StaticServiceLocator.Instance.Get<InGameManager>();
                if (inGameManager?.PlayerDataDic == null || Parameter.Owner == null) return null;

                Transform closestEnemy = null;
                float closestDistance = _searchRadius;
                Vector3 ownerPosition = Parameter.Owner.transform.position;

                foreach (var playerData in inGameManager.PlayerDataDic.Values)
                {
                    if (playerData == null || playerData == Parameter.Owner) continue;

                    var playerManager = playerData.GetComponent<PlayerManager>();
                    if (playerManager == null || playerManager.IsStun) continue;

                    float distance = Vector3.Distance(ownerPosition, playerData.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestEnemy = playerData.transform;
                    }
                }

                return closestEnemy;
            }
            catch (System.Exception)
            {
                return null;
            }
        }
        */
    }
}

