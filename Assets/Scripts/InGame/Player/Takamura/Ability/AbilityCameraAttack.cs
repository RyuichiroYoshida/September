using Fusion;
using InGame.Health;
using September.Common;
using UnityEngine;

namespace InGame.Player.Ability
{
    /// <summary>通常攻撃の処理を持つクラス</summary>
    public class AbilityCameraAttack : AbilityNormalAttack
    {
        [Header("上方向にどれくらいの角度をつけて飛ばすか"), SerializeField] float _upDegree = 30;
        [Header("ノックバックの強さ"), SerializeField] float _knockbackPower = 10f;

        protected override void OnHitEnemy(Collider hitInfo, Vector3 hitPosition)
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
    }
}
