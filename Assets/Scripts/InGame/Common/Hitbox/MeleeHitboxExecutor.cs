using System;
using System.Collections.Generic;
using UnityEngine;

public class MeleeHitboxExecutor : IHitboxExecutor
{
    private readonly List<Transform> _points;
    private readonly float _hitboxRadius;
    private readonly LayerMask _hitMask = default;
    private readonly HashSet<Collider> _alreadyHit = new();
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[32]; // バッファサイズは必要に応じて調整

    private int _currentFrame;
    private readonly int _startFrame;
    private readonly int _endFrame;

    public bool IsFinished => _currentFrame > _endFrame;
    public Action<Collider> OnHit;

    public MeleeHitboxExecutor(
        List<Transform> points,
        float hitboxRadius = 0.2f,
        LayerMask hitMask = default,
        int startFrame = 0,
        int endFrame = int.MaxValue)
    {
        _points = points ?? new List<Transform>();
        _hitboxRadius = hitboxRadius;
        _hitMask = hitMask == default ? ~0 : hitMask;
        _startFrame = startFrame;
        _endFrame = endFrame;
        _currentFrame = 0;
    }

    public void Tick(float deltaTime)
    {
        _currentFrame++;

        if (_currentFrame >= _startFrame && _currentFrame <= _endFrame)
        {
            ExecuteHitCheck();
        }
    }

    private void ExecuteHitCheck()
    {
        if (_points.Count == 1)
        {
            TrySphereCastSingle(_points[0]);
        }
        else if (_points.Count >= 2)
        {
            for (int i = 0; i < _points.Count - 1; i++)
            {
                var p0 = _points[i];
                var p1 = _points[i + 1];
                if (p0 == null || p1 == null) continue;

                var start = p0.position;
                var end = p1.position;
                var direction = end - start;
                var distance = direction.magnitude;

                if (distance <= 0.001f) continue;

                int hitCount = Physics.CapsuleCastNonAlloc(
                    start, end, _hitboxRadius,
                    direction.normalized, _hitBuffer, distance,
                    _hitMask);

                for (int j = 0; j < hitCount; j++)
                {
                    var collider = _hitBuffer[j].collider;
                    if (_alreadyHit.Add(collider))
                    {
                        OnHit?.Invoke(collider);
                    }
                }

#if UNITY_EDITOR
                DebugCapsuleGizmo(start, end);
#endif
            }

            // 奇数の場合、最後の点を SphereCast で補完
            if (_points.Count % 2 == 1)
            {
                TrySphereCastSingle(_points[^1]);
            }
        }
    }

    private void TrySphereCastSingle(Transform point)
    {
        if (point == null) return;

        var origin = point.position;
        var direction = point.forward;
        if (direction == Vector3.zero) direction = Vector3.forward;

        const float fallbackDistance = 0.01f;

        int hitCount = Physics.SphereCastNonAlloc(
            origin, _hitboxRadius, direction,
            _hitBuffer, fallbackDistance, _hitMask);

        for (int j = 0; j < hitCount; j++)
        {
            var collider = _hitBuffer[j].collider;
            if (_alreadyHit.Add(collider))
            {
                OnHit?.Invoke(collider);
            }
        }

#if UNITY_EDITOR
        DebugSphereGizmo(origin);
#endif
    }


#if UNITY_EDITOR
    public void DebugCapsuleGizmo(Vector3 start, Vector3 end)
    {
        if (HitboxDebugUtility.IsDebugModeEnabled)
        {
            DebugDrawUtility.DrawWireCapsule(start, end, _hitboxRadius, Color.red);
        }
    }
    
    public void DebugSphereGizmo(Vector3 start)
    {
        if (HitboxDebugUtility.IsDebugModeEnabled)
        {
            DebugDrawUtility.DrawWireSphere(start, _hitboxRadius, Color.red);
        }
    }
#endif
}
