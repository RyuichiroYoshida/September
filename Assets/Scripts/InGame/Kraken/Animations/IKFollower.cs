using System;
using System.Collections.Generic;
using System.Linq;
using RootMotion.FinalIK;
using September.InGame.Kraken.Animations;
using UnityEngine;
using UnityEngine.Profiling;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace September.InGame.Kraken
{
    public class IKFollower : MonoBehaviour
    {
        public readonly struct Point
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;

            public Point(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }

            public static Vector3[] ConvertToVector(IReadOnlyList<Point> points)
            {
                return points.Select(x => x.Position).ToArray();
            }

            public static Quaternion GetInterpolatedRotation(Point p0, Point p1, Vector3 target)
            {
                Vector3 v0 = target - p0.Position;
                Vector3 v1 = target - p1.Position;

                float d0 = v0.magnitude;
                float d1 = v1.magnitude;

                float m = Mathf.Max(d0, d1);

                // d0 = 0 => 0
                // d1 = 0 => 1
                // d0 = d1 => .5
                float ratio = ((d0 - d1) / m + 1f) * 0.5f;

                return Quaternion.Slerp(p0.Rotation, p1.Rotation, ratio);
            }

            public static Point Slerp(Point p0, Point p1, float t)
            {
                return new Point(
                    Vector3.Slerp(p0.Position, p1.Position, t),
                    Quaternion.Slerp(p0.Rotation, p1.Rotation, t)
                    );
            }
        }

        [SerializeField] private IK _ik;
        [SerializeField] private Transform[] _followers;
        [SerializeField] private float _radius = .55f;
        [SerializeField] private TentacleConstraintSolver _constraintSolver;
        [SerializeField] private int _subPointCount = 3;

        [SerializeField] private ConstraintManager _constraints;

        [SerializeField, HideInInspector] private float[] _maxDistances;
        [SerializeField, HideInInspector] private Quaternion[] _defaultRotations;

        private float[] _followerMaxDistances;

        [Header("Debug Settings")]
        [SerializeField] private bool _debugFabrikPoints;
        [SerializeField] private bool _debugFabrikAxis;
        [SerializeField] private bool _debugSolvedPoints;
        [SerializeField] private bool _debugSolvedAxis;
        [SerializeField] private bool _debugSolvedResamplingPoints;
        [SerializeField] private bool _debugSolvedResamplingAxis;

        private IKSolverPointProvider _pointProvider;

        private void Start()
        {
            _pointProvider = new IKSolverPointProvider(_ik.GetIKSolver());

            // IKの長さをキャッシュ
            var points = _pointProvider.GetPoints();
            _maxDistances = new float[points.Length];
            for (int i = 1; i < points.Length; i++)
            {
                var p0 = points[i - 1].Position;
                var p1 = points[i].Position;

                _maxDistances[i] += (p1 - p0).magnitude + _maxDistances[i - 1];
            }

            // ボーンの長さをキャッシュ
            _followerMaxDistances = new float[_followers.Length];
            for (int i = 1; i < _followers.Length; i++)
            {
                var p0 = _followers[i - 1].position;
                var p1 = _followers[i].position;

                _followerMaxDistances[i] += (p1 - p0).magnitude + _followerMaxDistances[i - 1];
            }
            
            // ボーンの回転をキャッシュ
            _defaultRotations = _followers.Select(x => x.rotation).ToArray();

            Validate();
        }

        private void Validate()
        {
            Debug.Assert(_maxDistances.Length > 0, "Max distance list is empty");
            Debug.Assert(_maxDistances.Skip(1).All(x => x > float.Epsilon), "Max distance element is too small");
            Debug.Assert(_maxDistances.SequenceEqual(_maxDistances.OrderBy(x => x)), $"Max distance is not ordered\n{string.Join(",\n", _maxDistances)}");
            Debug.Assert(_defaultRotations.Length > 0, "Default rotation array is empty");
        }

        private void Update()
        {
            Profiler.BeginSample("GetPoints");
            // FABRIKで障害物がない時のボーン位置を取得
            Point[] points = _pointProvider.GetPoints();
            Profiler.EndSample();

            IKFollowerDebug.DebugDraw(points, _radius * .2f, Color.yellow, _debugFabrikPoints, _debugFabrikPoints, _debugFabrikAxis);

            Profiler.BeginSample("EvaluateSubPoints");
            SlerpInterpolator interpolator = new(points, _maxDistances);
            Span<Point> subdividedPoints = stackalloc Point[points.Length * _subPointCount];
            interpolator.Evaluate(points.Length * _subPointCount, ref subdividedPoints);
            Profiler.EndSample();

            Profiler.BeginSample("SolveConstraints");
            _constraintSolver.Solve(ref subdividedPoints, Time.deltaTime);
            Span<Point> solvedPoints = subdividedPoints;
            Profiler.EndSample();

            IKFollowerDebug.DebugDraw(solvedPoints, 6f, Color.red, _debugSolvedPoints, _debugSolvedPoints, _debugSolvedAxis);

            Profiler.BeginSample("Resampling");
            Span<float> solvedDistances = stackalloc float[solvedPoints.Length];
            for (int i = 1; i < solvedDistances.Length; i++)
            {
                solvedDistances[i] =
                    solvedDistances[i - 1] +
                    (solvedPoints[i].Position - solvedPoints[i - 1].Position).magnitude;
            }
            SlerpInterpolator solvedInterpolator = new(solvedPoints, solvedDistances);
            Span<Point> solvedResamplingPoints = stackalloc Point[_followerMaxDistances.Length];
            solvedInterpolator.Evaluate(_followerMaxDistances, ref solvedResamplingPoints);
            Profiler.EndSample();

            IKFollowerDebug.DebugDraw(solvedResamplingPoints, 6f, Color.magenta, _debugSolvedResamplingPoints, _debugSolvedResamplingPoints, _debugSolvedResamplingAxis);

            Profiler.BeginSample("UpdatePoints");
            UpdatePosition(solvedResamplingPoints);
            Profiler.EndSample();

            if (_constraints) _constraints.Solve();
        }

        private void UpdatePosition(Span<Point> points)
        {
            for (int i = 0; i < _followers.Length; i++)
            {
                if (_followers[i] == null) continue;

                _followers[i].transform.position = points[i].Position;
                _followers[i].transform.rotation = points[i].Rotation;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// エディタ用。設定項目の自動アサイン処理
        /// </summary>
        [ContextMenu("SetObjects")]
        private void SetObjects()
        {
            Undo.RecordObject(this, "SetObjects");

            _followers = GetChildFlat().ToArray();
            _constraintSolver = GetComponent<TentacleConstraintSolver>();

            var hitChecker = GetComponent<HitChecker>();
            hitChecker.HitPoint.Clear();
            hitChecker.HitPoint.Add(transform);
            hitChecker.HitPoint.AddRange(_followers);

            return;

            IEnumerable<Transform> GetChildFlat()
            {
                Transform parent = transform;
                while (parent.childCount > 0)
                {
                    parent = parent.GetChild(0);
                    yield return parent;
                }
            }
        }
#endif
    }
}
