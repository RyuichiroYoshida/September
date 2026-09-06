using Fusion;
using InGame.Common;
using InGame.Health;
using September.Common;
using September.InGame.Effect;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace InGame.Player.Ability
{
    /// <summary>擬態解除時の攻撃の処理を持つクラス</summary>
    public class AbilityRevealAttack : AbilityBase
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
        [SerializeField] private AnimationClip _revealAttackAnimationClip;
        [SerializeField] private AnimationClipPlayer _animationClipPlayer;

        [Header("Hit Sphere 設定")]
        [SerializeField] private LayerMask _hitLayer = ~0;
        [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore;
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];
        protected readonly HashSet<Collider> _alreadyHit = new HashSet<Collider>();

        [Header("Debug Draw")]
        [SerializeField] private Color _debugHitBoxColor = new Color(0f, 0.6f, 1f, 1f);
        [SerializeField, Tooltip("Debug 線の表示秒数。0 なら 1 フレームだけ")]
        private float _debugDrawDuration = 1f; // 例: 0.05f

        [Header("上方向にどれくらいの角度をつけて飛ばすか"), SerializeField] float _upDegree = 30;
        [Header("ノックバックの強さ"), SerializeField] float _knockbackPower = 10f;

        // 変換後のTickオフセット
        protected int _startHitTick, _endHitTick, _endAttackTick;

        // 攻撃開始Tick
        protected int _attackStartTick = -1;

        // 最も近い敵のTransform
        protected Transform _closestEnemyTransform;
        protected PlayerMovement _playerMovement;
        protected EffectSpawner _effectSpawner;

        protected override void OnStart()
        {
            if (_animationClipPlayer && _revealAttackAnimationClip)
            {
                _animationClipPlayer.PlayClip(_revealAttackAnimationClip);
            }

            if (!_effectSpawner)
                _effectSpawner = StaticServiceLocator.Instance.Get<EffectSpawner>();

            _startHitTick = FrameToTick(_startHitCheckFrame);
            _endHitTick = FrameToTick(_endHitCheckFrame);
            _endAttackTick = FrameToTick(_endAttackFrame);

            _attackStartTick = Runner != null ? Runner.Tick : 0;

            // PlayerMovementコンポーネントを取得
            _playerMovement = Parameter.Owner.GetComponent<PlayerMovement>();

            _startHitTick = FrameToTick(_startHitCheckFrame);
            _playerMovement.IgnoreMoveInput = true;

        }

        public override void OnUpdateLocal(float deltaTime, GameObject owner)
        {
            if (HitboxDebugUtility.IsDebugModeEnabled)
            {
                if (_playerMovement is not TakamuraMovement typed) return;

                HitboxDebugUtility.DrawWireSphere(
                    Parameter.Owner.transform.position,
                    typed.GetRevealAttackRadius(),
                    _debugHitBoxColor   // 好きな色に
                );
            }
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

            var playerMovement = hitInfo.GetComponentInParent<PlayerMovement>();
            if (playerMovement != null)
            {
                // 自分を始点、相手を終点としたときベクトル
                var knockbackVector = (hitInfo.transform.position - Parameter.Owner.transform.position);
                // 水平方向に整える
                knockbackVector.y = 0;
                // 正規化
                knockbackVector = knockbackVector.normalized;
                // 度数法を弧度法に直してtanを用いて上方向の力を加える
                knockbackVector.y = Mathf.Tan(Mathf.Deg2Rad * _upDegree);
                //もう一度正規化
                knockbackVector = knockbackVector.normalized;

                // ノックバックの力を相手にかける
                playerMovement.AddFlyingVelocity(knockbackVector * _knockbackPower);

#if UNITY_EDITOR
                Debug.Log("<color=green>Takamura</color> : Attack!");
#endif
            }

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
                    return playerData.IsOgre ? _ogreAttackDamage : _attackDamage;
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
            if (_playerMovement is not TakamuraMovement typed) return;

            var t = Parameter.Owner.transform;

            // SphereCast の原点と向き
            var origin = t.position;
            var dir = t.forward;
            var radius = typed.GetRevealAttackRadius();

            // 掃引（NonAlloc で GC しない）
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                radius,
                dir,
                _hitBuffer, // LayerMaskをPlayerのみくらいにするといいかも
                0,  // その場の判定にするために移動距離を0にする
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
                    hitPos = origin + dir * Mathf.Max(0.1f, radius * 0.5f);

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

            // 攻撃終了
            if (elapsed >= _endAttackTick)
            {
                _playerMovement.IgnoreMoveInput = false;
                RequestEndAbility();
            }
        }

        protected int FrameToTick(int f)
        {
            float fps = _revealAttackAnimationClip ? _revealAttackAnimationClip.frameRate : 60f;
            float dt = Runner != null ? Runner.DeltaTime : Time.fixedDeltaTime;
            return Mathf.RoundToInt((f / fps) / dt);
        }
    }
}
