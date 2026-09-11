using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace September.InGame.Kraken
{
    public static class IKFollowerDebug
    {
        private static void DebugDrawLine(IReadOnlyList<Vector3> points, Color color)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 p0 = points[i];
                Vector3 p1 = points[i + 1];
                Debug.DrawLine(p0, p1, color);
            }
        }

        private static void DebugDrawLine(IReadOnlyList<IKFollower.Point> points, Color color)
        {
            DebugDrawLine(IKFollower.Point.ConvertToVector(points), color);
        }

        private static void DebugDrawSpheres(IReadOnlyList<Vector3> points, float radius, Color color)
        {
            foreach (var p in points)
            {
                DebugDrawUtility.DrawWireSphere(p, radius, color);
            }
        }

        private static void DebugDrawSpheres(IReadOnlyList<IKFollower.Point> points, float radius, Color color)
        {
            DebugDrawSpheres(IKFollower.Point.ConvertToVector(points), radius, color);
        }


        private static void DebugDrawAxis(IReadOnlyList<IKFollower.Point> points)
        {
            foreach (var p in points)
            {
                Debug.DrawRay(p.Position, p.Rotation * Vector3.up, Color.green);
                Debug.DrawRay(p.Position, p.Rotation * Vector3.forward, Color.blue);
                Debug.DrawRay(p.Position, p.Rotation * Vector3.right, Color.red);
            }
        }

        [Conditional("UNITY_EDITOR")]
        public static void DebugDraw(Span<IKFollower.Point> points, float radius, Color color,
            bool drawLine, bool drawSpheres, bool drawAxis)
        {
            if (!(drawLine || drawSpheres || drawAxis)) return;

            IKFollower.Point[] p = points.ToArray();

            if (drawLine) DebugDrawSpheres(p, radius, color);
            if (drawSpheres) DebugDrawLine(p, color);
            if (drawAxis) DebugDrawAxis(p);
        }
    }
}
