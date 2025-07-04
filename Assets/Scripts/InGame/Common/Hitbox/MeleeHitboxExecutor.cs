using System;
using System.Collections.Generic;
using UnityEngine;

public class MeleeHitboxExecutor : IHitboxExecutor
{
    private readonly List<Transform> _points;
    private readonly float _duration;
    private float _elapsed;

    private readonly float _hitboxRadius;
    private readonly LayerMask _hitMask = default;
    private readonly HashSet<Collider> _alreadyHit = new();
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[32]; // バッファサイズは必要に応じて調整

    private int _currentFrame;
    private readonly int _startFrame;
    private readonly int _endFrame;

    public bool IsFinished => _elapsed >= _duration;
    public Action<Collider> OnHit;

    public MeleeHitboxExecutor(
        List<Transform> points,
        float duration,
        float hitboxRadius = 0.2f,
        LayerMask hitMask = default,
        int startFrame = 0,
        int endFrame = int.MaxValue)
    {
        _points = points;
        _duration = duration;
        _hitboxRadius = hitboxRadius;
        _hitMask = hitMask;
        _startFrame = startFrame;
        _endFrame = endFrame;
        _currentFrame = 0;
    }

    public void Tick(float deltaTime)
    {
        _elapsed += deltaTime;
        _currentFrame++;

        if (_currentFrame >= _startFrame && _currentFrame <= _endFrame)
        {
            ExecuteHitCheck();
        }
    }

    public void ExecuteHitCheck(Action<Collider> externalOnHit = null)
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
                    externalOnHit?.Invoke(collider);
                }
            }

#if UNITY_EDITOR
            DebugGizmo(start, end);
#endif
        }
    }

#if UNITY_EDITOR
    public void DebugGizmo(Vector3 start, Vector3 end)
    {
        if (HitboxDebugUtility.IsDebugModeEnabled)
        {
            DebugDrawUtility.DrawWireCapsule(start, end, _hitboxRadius, Color.red);
        }
    }
#endif
}
