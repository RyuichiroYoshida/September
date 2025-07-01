using System;
using System.Collections.Generic;
using UnityEngine;

public class MeleeHitboxExecutor : IHitboxExecutor
{
    private readonly List<Transform> _points;
    private readonly float _duration;
    private float _elapsed;

    private readonly float _hitboxRadius;
    private readonly LayerMask _hitMask;
    private readonly HashSet<Collider> _alreadyHit = new();

    public bool IsFinished => _elapsed >= _duration;
    public Action<Collider> OnHit;

    public MeleeHitboxExecutor(
        List<Transform> points,
        float duration,
        float hitboxRadius = 0.2f,
        LayerMask hitMask = default)
    {
        _points = points;
        _duration = duration;
        _hitboxRadius = hitboxRadius;
        _hitMask = hitMask;
    }

    public void Tick(float deltaTime)
    {
        _elapsed += deltaTime;
    }

    public void ExecuteHitCheck(Action<Collider> externalOnHit = null)
    {
        for (int i = 0; i < _points.Count - 1; i++)
        {
            var p0 = _points[i];
            var p1 = _points[i + 1];

            if (p0 == null || p1 == null) continue;

            Vector3 start = p0.position;
            Vector3 end = p1.position;
            Vector3 direction = end - start;
            float distance = direction.magnitude;

            if (distance <= 0.001f) continue;

            var hits = Physics.CapsuleCastAll(
                start, end, _hitboxRadius,
                direction.normalized, distance,
                _hitMask, QueryTriggerInteraction.Ignore);

            foreach (var hit in hits)
            {
                var collider = hit.collider;
                if (_alreadyHit.Add(collider))
                {
                    OnHit?.Invoke(collider);
                    externalOnHit?.Invoke(collider);
                }
            }
        }
    }

#if UNITY_EDITOR
    public void DrawDebugGizmos()
    {
        if (!HitboxDebugUtility.IsDebugModeEnabled) return;

        Gizmos.color = Color.red;

        for (int i = 0; i < _points.Count - 1; i++)
        {
            var p0 = _points[i];
            var p1 = _points[i + 1];

            if (p0 == null || p1 == null) continue;

            Gizmos.DrawWireSphere(p0.position, _hitboxRadius);
            Gizmos.DrawWireSphere(p1.position, _hitboxRadius);
            Gizmos.DrawLine(p0.position, p1.position);
        }
    }
#endif
}