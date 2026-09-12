using System;
using System.Collections.Generic;

namespace September.InGame.Kraken.Animations
{
    public readonly ref struct SlerpInterpolator
    {
        private readonly Span<IKFollower.Point> _points;
        private readonly Span<float> _sumDistances; // 累積和（原点から同じインデックスのポイントまでの距離。1つ目は必ず0）

        public SlerpInterpolator(Span<IKFollower.Point> points, Span<float> sumDistances)
        {
            _points = points;
            _sumDistances = sumDistances;
        }

        public IKFollower.Point Evaluate(float distance)
        {
            for (int i = 0; i < _sumDistances.Length - 1; i++)
            {
                if (distance > _sumDistances[i + 1]) continue;

                float t = (distance - _sumDistances[i]) / (_sumDistances[i + 1] - _sumDistances[i]);
                return IKFollower.Point.Slerp(_points[i], _points[i + 1], t);
            }

            return _points[^1];
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
            float length = _sumDistances[^1];
            for (int i = 0; i < pointCount; i++)
            {
                points[i] = Evaluate(i * (length / pointCount));
            }
        }
    }
}
