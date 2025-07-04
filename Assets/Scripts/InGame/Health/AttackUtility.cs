using System.Collections.Generic;
using UnityEngine;

namespace InGame.Health
{
    public static class AttackHitUtility
    {
        /// <summary>
        /// 指定位置・範囲・角度での当たり判定（IDamageable専用）
        /// </summary>
        public static int OverlapDamageables(
            Vector3 origin,
            float radius,
            Collider[] buffer,
            HashSet<IDamageable> cache,
            int selfPlayerId,
            LayerMask mask,
            float angle = 360f,
            Vector3? forward = null)
        {
            Vector3 direction = forward ?? Vector3.forward;
            int count = Physics.OverlapSphereNonAlloc(origin, radius, buffer, mask);
            for (int i = 0; i < count; i++)
            {
                var col = buffer[i];
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null) continue;

                if (target.OwnerPlayerRef.RawEncoded == selfPlayerId) continue;

                if (angle < 360f)
                {
                    var toTarget = (col.transform.position - origin).normalized;
                    float angleToTarget = Vector3.Angle(direction, toTarget);
                    if (angleToTarget > angle * 0.5f) continue;
                }

                cache.Add(target);
            }

            return cache.Count;
        }
        
        public static int OverlapComponents<T>(
            Vector3 origin,
            float radius,
            Collider[] buffer,
            HashSet<T> cache,
            LayerMask mask,
            float angle = 360f,
            Vector3? forward = null
        ) where T : Component
        {
            Vector3 dir = forward ?? Vector3.forward;
            int count = Physics.OverlapSphereNonAlloc(origin, radius, buffer, mask);

            for (int i = 0; i < count; i++)
            {
                var col = buffer[i];
                if (!col.TryGetComponent(out T comp)) continue;

                if (angle < 360f)
                {
                    Vector3 toTarget = (col.transform.position - origin).normalized;
                    float ang = Vector3.Angle(dir, toTarget);
                    if (ang > angle * 0.5f) continue;
                }

                cache.Add(comp);
            }

            return cache.Count;
        }
    }
}