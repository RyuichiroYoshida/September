using System;
using System.Collections.Generic;
using UnityEngine;

namespace InGame.Combat
{
    public class HitboxManager : MonoBehaviour
    {
        [SerializeField] private bool _debugDrawGizmos = true;
        private readonly List<HitboxTimeline> _timelines = new();
        private readonly RaycastHit[] _capsuleHitBuffer = new RaycastHit[32];
        private readonly Collider[] _sphereHitBuffer = new Collider[32];

        public void RegisterTimeline(HitboxTimeline timeline)
        {
            if (!_timelines.Contains(timeline))
            {
                _timelines.Add(timeline);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_debugDrawGizmos) return;

            Gizmos.color = Color.green;
            float radius = 0.2f;

            foreach (var timeline in _timelines)
            {
                foreach (var action in timeline.GetAllActions())
                {
                    if (!action.IsActive) continue;

                    int count = action.points.Count;
                    for (int i = 0; i < count - 1; i++)
                    {
                        var p0 = action.points[i];
                        var p1 = action.points[i + 1];
                        if (p0 != null && p1 != null)
                        {
                            Gizmos.DrawLine(p0.position, p1.position);
                            Gizmos.DrawWireSphere(p0.position, radius);
                            Gizmos.DrawWireSphere(p1.position, radius);
                        }
                    }

                    if (count % 2 != 0 && action.points[count - 1] != null)
                    {
                        Gizmos.color = Color.red;
                        Gizmos.DrawWireSphere(action.points[count - 1].position, radius);
                        Gizmos.color = Color.green;
                    }
                }
            }
        }
#endif

        public void ProcessHitboxAction(HitboxAction action)
        {
            int count = action.points.Count;

            for (int i = 0; i < count - 1; i++)
            {
                var p0 = action.points[i];
                var p1 = action.points[i + 1];

                if (p0 != null && p1 != null)
                {
                    var direction = p1.position - p0.position;
                    float radius = 0.2f;

                    int hitCount = Physics.CapsuleCastNonAlloc(
                        p0.position, p1.position, radius, direction.normalized,
                        _capsuleHitBuffer, 0f);

                    for (int h = 0; h < hitCount; h++)
                    {
                        var hit = _capsuleHitBuffer[h];
                        var collider = hit.collider;

                        // 🔽 既にヒットしていたらスキップ
                        if (action.AlreadyHitColliders.Contains(collider)) continue;

                        action.AlreadyHitColliders.Add(collider);
                        Debug.Log($"Hit {collider.name} with capsule from {p0.name} to {p1.name}");
                    }
                }
            }

            if (count % 2 != 0 && action.points[count - 1] != null)
            {
                var last = action.points[count - 1];
                float radius = 0.2f;

                int hitCount = Physics.OverlapSphereNonAlloc(last.position, radius, _sphereHitBuffer);
                for (int i = 0; i < hitCount; i++)
                {
                    var collider = _sphereHitBuffer[i];

                    // 🔽 既にヒットしていたらスキップ
                    if (action.AlreadyHitColliders.Contains(collider)) continue;

                    action.AlreadyHitColliders.Add(collider);
                    Debug.Log($"Hit {collider.name} with sphere at {last.name}");
                }
            }
        }
    }
}