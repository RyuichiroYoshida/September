using System;
using System.Linq;
using RootMotion.FinalIK;
using September.Common.Extensions;

namespace September.InGame.Kraken.Animations
{
    public interface IPointProvider
    {
        public IKFollower.Point[] GetPoints();
        public int GetCount();
    }

    public struct IKSolverPointProvider : IPointProvider
    {
        private readonly IKSolver.Point[] _points;
        private IKFollower.Point[] _results;

        public IKSolverPointProvider(IKSolver solver)
        {
            _points = solver.GetPoints().DistinctBy(x => x.transform).ToArray();
            _results = new IKFollower.Point[_points.Length];
        }

        public IKFollower.Point[] GetPoints()
        {
            if (_results.Length != _points.Length)
            {
                Array.Resize(ref _results, _points.Length);
            }

            for (int i = 0; i < _results.Length; i++)
            {
                _results[i] = Convert(_points[i]);
            }

            return _results;
        }

        public int GetCount()
        {
            return _points.Length;
        }

        private static IKFollower.Point Convert(IKSolver.Point point)
        {
            return new IKFollower.Point(point.solverPosition, point.solverRotation);
        }
    }
}
