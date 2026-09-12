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

        public static void DrawWireCapsule(Vector3 p0, Vector3 p1, float radius)
        {
            var capsuleDirection = (p0 - p1).normalized;
            var rot = Quaternion.FromToRotation(Vector3.up, capsuleDirection);

            // 中心位置を使って、球体を描画
            Gizmos.DrawWireSphere(p0, radius);
            Gizmos.DrawWireSphere(p1, radius);

            // 4方向をつなぐ
            Vector3[] directions =
            {
                Vector3.right, Vector3.back, Vector3.forward, Vector3.left
            };

            foreach (var dir in directions)
            {
                var offset = rot * dir * radius;
                Gizmos.DrawLine(p0 + offset, p1 + offset);
            }
        }

        /// <summary>
        /// コライダー形状に合わせて描画を行う
        /// MeshColliderは未対応（何も描画しない）
        /// </summary>
        public static void DrawCollider(Collider collider)
        {
            var originMatrix = Gizmos.matrix;
            var transform = collider.transform;

            switch (collider)
            {
                case BoxCollider box:
                {
                    Gizmos.matrix = transform.localToWorldMatrix;
                    var boxSize = box.size;
                    Gizmos.DrawWireCube(box.center, boxSize);
                    break;
                }
                case SphereCollider sphere:
                {
                    Gizmos.matrix = transform.localToWorldMatrix;
                    Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                    break;
                }
                case CapsuleCollider capsule:
                {
                    Gizmos.matrix = transform.localToWorldMatrix;

                    var direction = capsule.direction switch
                    {
                        0 => Vector3.right,
                        1 => Vector3.up,
                        2 => Vector3.forward,
                    };

                    var height = Mathf.Max(capsule.height - capsule.radius * 2, 0);
                    var center = capsule.center;
                    var offset = height * 0.5f * direction;

                    var p0 = center + offset;
                    var p1 = center - offset;

                    DrawWireCapsule(p0, p1, capsule.radius);
                    break;
                }
            }

            Gizmos.matrix = originMatrix;
        }
    }
}
