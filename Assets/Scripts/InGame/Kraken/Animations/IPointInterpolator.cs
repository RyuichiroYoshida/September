using System;
using System.Collections.Generic;

namespace September.InGame.Kraken
{
    public interface IPointInterpolator
    {
        public IKFollower.Point Evaluate(float distance);
        public void Evaluate(IReadOnlyList<float> distances, ref Span<IKFollower.Point> points);
        public void Evaluate(int pointCount, ref Span<IKFollower.Point> points);
    }
}
