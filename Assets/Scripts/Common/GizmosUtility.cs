using System;
using UnityEngine;

namespace September.Common
{
    public static class GizmosUtility
    {
        public static void DrawHorizontalCross(Vector3 center, float size)
        {
            float halfSize = size * 0.5f;
            Gizmos.DrawLine(center - Vector3.right * halfSize, center + Vector3.right * halfSize);
            Gizmos.DrawLine(center - Vector3.forward * halfSize, center + Vector3.forward * halfSize);
        }

        public static void DrawCircle(Vector3 center, Vector3 forward, float radius)
        {
            const int segments = 32;
            Quaternion rot = Quaternion.LookRotation(forward, Vector3.up);
            Span<Vector3> points = stackalloc Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2;
                points[i] = center + rot * new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
            }
            Gizmos.DrawLineStrip(points, true);
        }

        public static void DrawMultiRay(MultiRay multiRay, Vector3 position, Quaternion rotation)
        {
            Vector3 forwardRayOrigin = position + rotation * multiRay.StartOrigin;
            Vector3 backRayOrigin = position + rotation * multiRay.EndOrigin;

            // 地面判定
            for (int i = 0; i < multiRay.DivideCount + 2; i++)
            {
                Vector3 rayOrigin = Vector3.Lerp(forwardRayOrigin, backRayOrigin, i / (multiRay.DivideCount + 1f));
                Gizmos.DrawRay(rayOrigin, rotation * multiRay.Direction * multiRay.Distance);
            }
        }
    }
}
