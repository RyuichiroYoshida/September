using Fusion;
using System.Collections.Generic;
using InGame.Exhibit;
using InGame.Health;
using InGame.Interact;
using September.Common;
using September.InGame.Effect;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace InGame.Player.Ability.Effect
{
    [Serializable]
    public class AbilityHulkUlt : AbilityUltBase
    {
        [Header("必殺技効果設定")]
        [SerializeField] private float _radius = 20f;
        [SerializeField] private float _exhibitsCooldownTime = 5f;
        [SerializeField] private int _damage = 10;
        [SerializeField] private float _attackInterval = 1;
        [SerializeField] private float _attackDuration = 4.5f;
        [SerializeField] private LayerMask _hitLayerMask;

        [Header("視覚効果設定")]
        [SerializeField] private EffectType _effectType;
        [SerializeField] private Vector3 _effectOffset;

        private EffectSpawner _effectSpawner;
        private readonly Collider[] _hitBuffer = new Collider[10];
        private float _attackTimer;
        private Vector3 _effectCenter;
        private bool _isAttacking;

        private IReadOnlyList<InteractableBase> _exhibits;

        protected override void OnStartEffect()
        {
            var player = Parameter.Owner;

            _exhibits ??= GetExhibits();

            foreach (var item in _exhibits)
            {
                if (!item) continue;

                var sqDistance = (item.transform.position - player.transform.position).sqrMagnitude;
                if (sqDistance > _radius * _radius) continue;

                item.SetCooldown(_exhibitsCooldownTime);
            }
            _effectCenter = player.transform.position + player.transform.rotation * _effectOffset;
            _effectSpawner = StaticServiceLocator.Instance.Get<EffectSpawner>();
            _effectSpawner?.RequestPlayOneShotEffect(_effectType, _effectCenter, Quaternion.identity);

            //攻撃処理の開始
            _isAttacking = true;
            _attackTimer = _attackInterval;
        }

        protected override void OnCutInUpdate(float deltaTime)
        {
            UpdateAttackTimer(deltaTime);
        }

        protected override void OnUpdateUlt(float deltaTime)
        {
            UpdateAttackTimer(deltaTime);

            // 持続時間を超えたらアビリティ終了
            if (TimeSinceCutInEnd > _attackDuration)
            {
                _isAttacking = false;
                RequestEndAbility();
            }
        }

        protected override void OnCutInEnd()
        {
            Debug.Log("[AbilityHulkUlt] End");
        }

        protected override void OnEndUlt()
        {
            //アビリティ終了時処理
            _isAttacking = false;
            _attackTimer = 0f;
            _effectCenter = Vector3.zero;
        }

        /// <summary>
        /// 範囲内の敵にダメージを適用する
        /// </summary>
        private void ApplyAreaDamage()
        {
            var player = Parameter.Owner;
            if (player == null) return;
            int count = Physics.OverlapSphereNonAlloc(_effectCenter, _radius, _hitBuffer, _hitLayerMask);
            HitboxDebugUtility.DrawWireSphere(_effectCenter, _radius, Color.blue, 0.1f);
            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = _hitBuffer[i];
                if (hitCollider == null) continue;

                // 自分自身にはダメージを与えない
                if (hitCollider.GetComponentInParent<NetworkObject>() == player) continue;

                //ダメージコンポーネントの取得
                var damageable = hitCollider.GetComponentInParent<IDamageable>();
                if (damageable == null) continue;

                HitData hitData = new(
                    HitActionType.Damage,
                    _damage,
                    player.InputAuthority,
                    damageable.OwnerPlayerRef
                );

                damageable.TakeHit(ref hitData);
            }
        }

        /// <summary>
        /// 攻撃タイマーを更新し、間隔に達したら範囲ダメージを実行する
        /// </summary>
        private void UpdateAttackTimer(float deltaTime)
        {
            if (!_isAttacking) return;
            _attackTimer += deltaTime;
            if (_attackTimer >= _attackInterval)
            {
                ApplyAreaDamage();
                _attackTimer = 0f;
            }
        }

        private IReadOnlyList<InteractableBase> GetExhibits()
        {
            if (ExhibitRegistry.I != null)
            {
                return ExhibitRegistry.I.Items;
            }
            else
            {
                Debug.LogWarning("[AbilityHulkUlt] ExhibitRegistry が存在しないため、全探索を行います");
                return Object.FindObjectsByType<InteractableBase>(FindObjectsSortMode.None);
            }
        }
    }
}
