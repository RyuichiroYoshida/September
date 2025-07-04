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

    private readonly Collider[] _hitBuffer = new Collider[10];
    private static InGameManager _inGameManager;

    private float _remainingTime;
    private Vector3 _attackOrigin;
    private readonly HashSet<IDamageable> _alreadyHit = new();

    public override bool RunLocal => false;
    public override string DisplayName => "通常攻撃";

    public AbilityNormalAttack() { }

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

        if (!_inGameManager.PlayerDataDic.TryGetValue(PlayerRef.FromEncoded(Context.SourcePlayer), out var playerData))
        {
            Debug.LogError("PlayerDataが見つかりません。通常攻撃を実行できません。");
            ForceEnd();
            return;
        }

        var ownerAnimator = playerData.GetComponent<AnimationClipPlayer>();
        if (ownerAnimator) ownerAnimator.PlayClip(_attackAnimationClip);
        _attackOrigin = playerData.transform.position;
        _remainingTime = _attackDuration;
        _alreadyHit.Clear();

#if UNITY_EDITOR
        DebugDrawHelper.RegisterAttackPosition(_attackOrigin, _attackRadius, Color.red, _attackDuration);
#endif
    }

    protected override void OnUpdate(float deltaTime)
    {
        _remainingTime -= deltaTime;
        if (_remainingTime <= 0f)
        {
            ApplyCachedHits();
            ForceEnd();
            return;
        }

        AttackHitUtility.OverlapDamageables(
            _attackOrigin,
            _attackRadius,
            _hitBuffer,
            _alreadyHit,
            OwnerPlayerId,
            _hitMask
        );
    }

    private void ApplyCachedHits()
    {
        foreach (var target in _alreadyHit)
        {
            var hitData = new HitData
            {
                HitActionType = HitActionType.Damage,
                Amount = _attackDamage,
                ExecutorRef = PlayerRef.FromEncoded(Context.SourcePlayer),
                TargetRef = target.OwnerPlayerRef,
                Target = target,
            };

            Debug.Log($"ApplyCachedHits: {hitData.ExecutorRef} -> {hitData.TargetRef}");
            target.TakeHit(ref hitData);
        }

        _alreadyHit.Clear();
    }
}

}
