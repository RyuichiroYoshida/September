using System.Collections.Generic;
using InGame.Health;
using UnityEngine;
using Fusion;
using InGame.Common;
using NaughtyAttributes;
using September.Common;
using September.InGame.Common;

namespace InGame.Player.Ability
{
    [System.Serializable]
    public class AbilityNormalAttack : AbilityBase
    {
        [SerializeField] private float _attackRadius = 1.0f;
        [SerializeField, Label("攻撃力")] private int _attackDamage = 10;
        [SerializeField] private float _attackDuration = 1.0f;
        [SerializeField] private LayerMask _hitMask;
        [SerializeField] private AnimationClip _attackAnimationClip;

        private static InGameManager _inGameManager;

        private float _remainingTime;
        private MeleeHitboxExecutor _executor;
        private readonly HashSet<IDamageable> _alreadyHit = new();

        public override bool RunLocal => false;
        public override string DisplayName => "通常攻撃";

        public AbilityNormalAttack()
        {
        }

        public AbilityNormalAttack(AbilityNormalAttack original) : base(original)
        {
            _attackRadius = original._attackRadius;
            _attackDamage = original._attackDamage;
            _attackDuration = original._attackDuration;
            _hitMask = original._hitMask;
            _attackAnimationClip = original._attackAnimationClip;
        }

        public override AbilityBase Clone(AbilityBase abilityReference) => new AbilityNormalAttack(this);

        protected override void OnStart()
        {
            if (!_inGameManager && !StaticServiceLocator.Instance.TryGet(out _inGameManager))
            {
                Debug.LogError("InGameManagerが見つかりません。通常攻撃を実行できません。");
                ForceEnd();
                return;
            }

            if (!_inGameManager.PlayerDataDic.TryGetValue(PlayerRef.FromEncoded(Context.SourcePlayer),
                    out var playerData))
            {
                Debug.LogError("PlayerDataが見つかりません。通常攻撃を実行できません。");
                ForceEnd();
                return;
            }

            var ownerAnimator = playerData.GetComponent<AnimationClipPlayer>();
            if (ownerAnimator) ownerAnimator.PlayClip(_attackAnimationClip);
            var resolver = playerData.GetComponentInChildren<HitPointResolver>();
            var points = resolver?.GetPoints();
            var start = resolver?.GetStartFrame();
            var end = resolver?.GetEndFrame();
            _executor = new MeleeHitboxExecutor(points, _attackDuration, _attackRadius, _hitMask, start ?? 0, end ?? int.MaxValue)
            {
                OnHit = collider =>
                {
                    if (collider.TryGetComponent(out IDamageable damageable))
                    {
                        _alreadyHit.Add(damageable);
                    }
                }
            };

            _remainingTime = _attackDuration;
            _alreadyHit.Clear();
        }

        protected override void OnUpdate(float deltaTime)
        {
            _remainingTime -= deltaTime;
            if (_remainingTime <= 0f)
            {
                ForceEnd();
                return;
            }

            _executor?.Tick(deltaTime);
            _executor?.ExecuteHitCheck();
        }
    }
}