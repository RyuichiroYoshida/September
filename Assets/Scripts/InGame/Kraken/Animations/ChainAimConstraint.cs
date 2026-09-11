using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace September.InGame.Kraken.Animations
{
    public class ChainAimConstraint : ConstraintBase
    {
        public Transform Root;
        public Transform Tip;
        public Transform LookAtTarget;
        public Vector3 RotateAxis = Vector3.forward;
        public Vector3 AimAxis = Vector3.up;
        public UpdateMode UpdateMode = UpdateMode.Update;

        private Transform[] _chain;

        private IKFollower.Point[] _originalPoints;

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (UpdateMode != UpdateMode.Update) return;

            ManualUpdate();
        }

        public void Initialize()
        {
            _chain = ConstraintsUtils.ExtractChain(Root, Tip);
            _originalPoints = new IKFollower.Point[_chain.Length];
        }

        public override void PreSolve()
        {
            int count = _chain.Length;

            for (int i = 0; i < count; i++)
            {
                _originalPoints[i] = new IKFollower.Point(_chain[i].position, _chain[i].rotation);
            }
        }

        public override void PostSolve()
        {

        }

        public override void ManualUpdate()
        {
            int count = _chain.Length;

            Span<IKFollower.Point> results = stackalloc IKFollower.Point[count];
            Solve(_originalPoints, ref results);

            for (int i = 0; i < count; i++)
            {
                _chain[i].position = results[i].Position;
                _chain[i].rotation = results[i].Rotation;
            }
        }

        public void Solve(ReadOnlySpan<IKFollower.Point> points, ref Span<IKFollower.Point> resolvedPoints)
        {
            int count = points.Length;
            Span<Vector3> positions = stackalloc Vector3[count];
            Span<Vector3> forwards = stackalloc Vector3[count];

            for (int i = 0; i < count - 1; i++)
            {
                positions[i] = points[i].Position;
                forwards[i] = (points[i + 1].Position - points[i].Position).normalized;

                var targetUp = LookAtTarget.position - positions[i];
                var projUp = Vector3.ProjectOnPlane(targetUp, forwards[i]).normalized;

                var rot = BoneRotationUtility.CalculateRotation(RotateAxis, AimAxis, forwards[i], projUp);

                resolvedPoints[i] = new IKFollower.Point(positions[i], rot);
            }
            resolvedPoints[^1] = points[^1];
        }
    }

    public enum UpdateMode
    {
        Update,
        Manual,
    }

    public static class BoneRotationUtility
    {
        public static Quaternion CalculateRotation(
            Vector3 localForward,
            Vector3 localUp,
            Vector3 worldForward,
            Vector3 worldUp)
        {
            // -------------------------
            // ローカル側の座標系を作る
            // -------------------------

            localUp.Normalize();

            localForward = Vector3.ProjectOnPlane(
                localForward,
                localUp
            ).normalized;

            if (localForward.sqrMagnitude < 0.000001f)
                return Quaternion.identity;

            Quaternion localBasis =
                Quaternion.LookRotation(
                    localForward,
                    localUp
                );

            // -------------------------
            // ワールド側の座標系を作る
            // -------------------------

            worldUp.Normalize();

            worldForward = Vector3.ProjectOnPlane(
                worldForward,
                worldUp
            ).normalized;

            if (worldForward.sqrMagnitude < 0.000001f)
                return Quaternion.identity;

            Quaternion worldBasis =
                Quaternion.LookRotation(
                    worldForward,
                    worldUp
                );

            // -------------------------
            // ローカル軸 → 目標ワールド軸
            // -------------------------

            return worldBasis * Quaternion.Inverse(localBasis);
        }
    }
}
