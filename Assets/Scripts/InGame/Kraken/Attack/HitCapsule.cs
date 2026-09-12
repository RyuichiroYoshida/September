using System;
using Fusion;
using UnityEngine;

namespace September.InGame.Kraken.Attack
{
    public struct CapsuleShape
    {
        public Vector3 Start;
        public Vector3 End;
        public float Radius;

        public CapsuleShape(Vector3 start, Vector3 end, float radius)
        {
            Start = start;
            End = end;
            Radius = radius;
        }
    }

    public struct HitCapsule
    {
        private static readonly Collider[] Hits = new Collider[10];

        private TickTimer _endTimer;

        public CapsuleShape Shape;
        public LayerMask LayerMask;

        public Action<Collider> OnHit;

        public static HitCapsule Create(NetworkRunner runner, CapsuleShape shape, float duration, LayerMask layerMask, Action<Collider> onHit)
        {
            return new HitCapsule
            {
                _endTimer = TickTimer.CreateFromSeconds(runner, duration),
                Shape = shape,
                LayerMask = layerMask,
                OnHit = onHit
            };
        }

        public void Cast()
        {
            HitboxDebugUtility.DrawWireCapsule(Shape.Start, Shape.End, Shape.Radius, Color.red);

            int hitCount = Physics.OverlapCapsuleNonAlloc(Shape.Start, Shape.End, Shape.Radius, Hits, LayerMask);

            if (hitCount == Hits.Length) Debug.LogWarning("[HitCapsule] ヒットバッファのサイズが不足しています");

            for (int i = 0; i < hitCount; i++)
            {
                OnHit(Hits[i]);
            }
        }

        public bool ExpiredOrNotRunning(NetworkRunner runner)
        {
            return _endTimer.ExpiredOrNotRunning(runner);
        }
    }
}
