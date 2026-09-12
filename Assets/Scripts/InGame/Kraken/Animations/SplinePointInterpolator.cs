using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Splines;

namespace September.InGame.Kraken
{
    public readonly struct SplinePointInterpolator : IPointInterpolator
    {
        private readonly Spline _spline;
        private const float Tension = 1f;

        public SplinePointInterpolator(IKFollower.Point[] points)
        {
            _spline = CreateSpline(points, Tension);
        }

        public SplinePointInterpolator(Span<IKFollower.Point> points)
        {
            _spline = CreateSpline(points, Tension);
        }

        private static Spline CreateSpline(Span<IKFollower.Point> points, float tension)
        {
            var spline = new Spline();
            foreach (var point in points)
            {
                // tangentの向きはクラーケン用に合わせている
                spline.Add(new BezierKnot(point.Position, Vector3.right * tension, -Vector3.right * tension, point.Rotation));
            }
            return spline;
        }

        public void Evaluate(IReadOnlyList<float> distances, ref Span<IKFollower.Point> points)
        {
            for (int i = 0; i < distances.Count; i++)
            {
                points[i] = Evaluate(distances[i]);
            }
        }

        public void Evaluate(int pointCount, ref Span<IKFollower.Point> points)
        {
            float length = _spline.GetLength();
            for (int i = 0; i < pointCount; i++)
            {
                points[i] = Evaluate(i * (length / pointCount));
            }
        }

        public IKFollower.Point Evaluate(float distance)
        {
            float curveLength = _spline.GetLength();
            float t = distance / curveLength;
            Profiler.BeginSample("Evaluate Spline");
            _spline.Evaluate(t, out var position, out var tangent, out var upVector);
            Profiler.EndSample();

            // Splineのちょうど両端の法線は必ずゼロベクトルになるっぽい
            if (Vector3.SqrMagnitude(tangent) == 0f)
            {
                _spline.Evaluate(t + .01f, out _, out tangent, out _);
                if (Vector3.SqrMagnitude(tangent) == 0f)
                {
                    _spline.Evaluate(t - .01f, out _, out tangent, out _);
                }
            }

            // TODO: tangent情報が元の姿勢を表していないためアーティファクトが発生する。
            var rotation = Quaternion.LookRotation(tangent, upVector) * Quaternion.LookRotation(Vector3.right);
            return new IKFollower.Point(position, rotation);
        }
    }
}
