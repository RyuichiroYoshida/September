using System;
using Unity.Mathematics.Geometry;
using UnityEngine;
using UnityEngine.Profiling;

namespace September.InGame.Kraken.Animations
{
    /// <summary>
    /// Position-Based Dynamics (PBD) based collision solver for tentacles.
    /// Satisfies non-penetration, distance, and bending/rotation constraints
    /// to ensure natural-looking animations that snap to deck colliders.
    /// Supports landed/staying state to keep the tentacle on the deck for a given duration.
    /// </summary>
    public class TentacleConstraintSolver : MonoBehaviour
    {
        [Header("Collision Settings")]
        [SerializeField] private float _radius = 0.55f;
        [SerializeField] private LayerMask _layerMask;

        [Header("Constraint Settings")]
        [SerializeField] private float _maxBendingAngle = 25f; // Max angle (degrees) between adjacent segments
        [SerializeField] private float _maxRotationSpeed = 180f; // Max rotation change (degrees/second) for stability
        [SerializeField] private int _pbdIterations = 5;
        [SerializeField] private int _pbdSubsteps = 5;

        [Header("Tentacle Settings")]
        [SerializeField] private Kraken _kraken;
        [SerializeField] private int _tentacleIndex;

        public event Action<Vector3> OnCollided;

        private float[] _segmentLengths;
        private Collider[] _colliders;

        private IKFollower.Point[] _prevSolvedPoints;

        [NonSerialized] public bool EnablePhysicsConstraint;

        private bool[] _isCollidingThisFrame;
        private bool[] _wasCollided;
        private Vector3[] _hitPoints;

        private ArmSettings ArmSettings => _kraken.Tentacles.Arms[_tentacleIndex];

        /// <summary>
        /// Solves constraints for the given input points.
        /// </summary>
        public void Solve(ref Span<IKFollower.Point> inputPoints, float deltaTime)
        {
            Profiler.BeginSample("Initialize");
            if (inputPoints == null || inputPoints.Length == 0) return;

            int count = inputPoints.Length;

            // 1. Initialize segment lengths and cache buffers on first run or when bone count changes
            InitializeBuffers(inputPoints);
            InitializeCollisionBuffers(count);

            Profiler.EndSample();

            // 2. Prepare predicted positions
            Profiler.BeginSample("Prepare predictions");
            Span<Vector3> solvedPositions = stackalloc Vector3[count];
            Span<Quaternion> solvedRotations = stackalloc Quaternion[count];
            for (int i = 0; i < count; i++)
            {
                solvedPositions[i] = _prevSolvedPoints[i].Position;
                solvedRotations[i] = inputPoints[i].Rotation;
            }
            Profiler.EndSample();

            // 4. Run PBD Iterative Solver to satisfy constraints
            Profiler.BeginSample("Iterate");
            for (int iter = 0; iter < _pbdIterations; iter++)
            {
                for (int step = 0; step < _pbdSubsteps; step++)
                {
                    Profiler.BeginSample("Non-penetration Constraint");
                    // A. Non-penetration Constraint (非侵入拘束)
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 h = (inputPoints[i].Position - solvedPositions[i]) * (1f / _pbdSubsteps);
                        Vector3 p = solvedPositions[i] + h;

                        if (EnablePhysicsConstraint && i != 0)
                        {
                            Vector3 pos = ResolveCollisions(p, _radius, _layerMask, out bool collided, out Vector3 hitPoint);

                            if (collided)
                            {
                                // 接触情報を保存
                                _isCollidingThisFrame[i] = true;
                                _hitPoints[i] = hitPoint;
                            }

                            if (i > 3)
                            {
                                // 攻撃予測からズレないよう補正する
                                Vector3 delta = pos - p;
                                pos = p + Vector3.ProjectOnPlane(delta, ArmSettings.TargetDirectionPlaneNormal);
                            }

                            solvedPositions[i] = pos;
                        }
                        else
                        {
                            solvedPositions[i] = p;
                        }
                    }
                    Profiler.EndSample();

                    Profiler.BeginSample("Distance Constraint");
                    // B. Distance Constraint (距離拘束) - standard PBD with mass weights
                    for (int i = 1; i < count; i++)
                    {
                        float targetDist = _segmentLengths[i];
                        Vector3 diff = solvedPositions[i] - solvedPositions[i - 1];
                        float currentDist = diff.magnitude;

                        if (i == 1)
                        {
                            Vector3 dir = diff / currentDist;
                            float error = currentDist - targetDist;
                            Vector3 correction = dir * error;

                            solvedPositions[i] -= correction;
                            continue;
                        }

                        if (currentDist > 0.0001f)
                        {
                            Vector3 dir = diff / currentDist;
                            float error = currentDist - targetDist;
                            Vector3 correction = dir * error / 2;

                            solvedPositions[i - 1] += correction;
                            solvedPositions[i] -= correction;
                        }
                    }
                    Profiler.EndSample();

                    Profiler.BeginSample("Bending Constraint");
                    // C. Bending / Rotation Constraint (回転拘束)
                    // Restricts the angle between consecutive bone segments
                    for (int i = 2; i < count; i++)
                    {
                        Vector3 v1 = solvedPositions[i - 1] - solvedPositions[i - 2];
                        Vector3 v2 = solvedPositions[i] - solvedPositions[i - 1];

                        float angle = Vector3.Angle(v1, v2);
                        if (angle > _maxBendingAngle)
                        {
                            Vector3 axis = Vector3.Cross(v1, v2).normalized;
                            if (axis.sqrMagnitude < 0.001f)
                                axis = Vector3.up;

                            Quaternion limitRot = Quaternion.AngleAxis(_maxBendingAngle - angle, axis);
                            Vector3 constrainedV2 = limitRot * v2;

                            solvedPositions[i] = solvedPositions[i - 1] + constrainedV2.normalized * _segmentLengths[i];
                        }
                    }
                    Profiler.EndSample();
                }
            }
            Profiler.EndSample();

            Profiler.BeginSample("Compute Orientations");
            // 5. Compute Orientations (Rotations)
            // Align bone rotations with the solved bone segment directions
            for (int i = 0; i < count; i++)
            {
                if (i < count - 1)
                {
                    Vector3 origDir = inputPoints[i + 1].Position - inputPoints[i].Position;
                    Vector3 solvedDir = solvedPositions[i + 1] - solvedPositions[i];

                    if (origDir.sqrMagnitude > 0.0001f && solvedDir.sqrMagnitude > 0.0001f)
                    {
                        Quaternion deltaRot = Quaternion.FromToRotation(origDir.normalized, solvedDir.normalized);
                        Quaternion targetRot = deltaRot * inputPoints[i].Rotation;

                        // Apply temporal damping to satisfy the rotational velocity constraint
                        if (_prevSolvedPoints != null && _prevSolvedPoints.Length == count)
                        {
                            float maxAngleChange = _maxRotationSpeed * deltaTime;
                            solvedRotations[i] = Quaternion.RotateTowards(_prevSolvedPoints[i].Rotation, targetRot, maxAngleChange);
                        }
                        else
                        {
                            solvedRotations[i] = targetRot;
                        }
                    }
                    else
                    {
                        solvedRotations[i] = inputPoints[i].Rotation;
                    }
                }
                else
                {
                    // For the tip, match the previous segment's solved rotation or fallback to original
                    if (count >= 2)
                    {
                        solvedRotations[i] = solvedRotations[i - 1];
                    }
                    else
                    {
                        solvedRotations[i] = inputPoints[i].Rotation;
                    }
                }
            }

            Profiler.EndSample();

            Profiler.BeginSample("Build Resolved Points");
            // 6. Build the final resolved Points
            for (int i = 0; i < count; i++)
            {
                inputPoints[i] = new IKFollower.Point(solvedPositions[i], solvedRotations[i]);
                if (_prevSolvedPoints != null) _prevSolvedPoints[i] = inputPoints[i];
            }
            Profiler.EndSample();

            Profiler.BeginSample("Check Collisions Trigger");
            // 7. Check for new collisions and trigger events
            for (int i = 0; i < count; i++)
            {
                if (_isCollidingThisFrame[i] && !_wasCollided[i])
                {
                    OnCollided?.Invoke(_hitPoints[i]);
                }
                _wasCollided[i] = _isCollidingThisFrame[i];
            }
            Profiler.EndSample();
        }

        public void ResetState()
        {
            Array.Clear(_wasCollided, 0, _wasCollided.Length);
        }

        private void InitializeBuffers(Span<IKFollower.Point> inputPoints)
        {
            int count = inputPoints.Length;
            if (_segmentLengths == null || _segmentLengths.Length != count)
            {
                _segmentLengths = new float[count];
                _segmentLengths[0] = 0f;
                for (int i = 1; i < count; i++)
                {
                    _segmentLengths[i] = Vector3.Distance(inputPoints[i - 1].Position, inputPoints[i].Position);
                }
            }

            if (_prevSolvedPoints == null || _prevSolvedPoints.Length != count)
            {
                _prevSolvedPoints = inputPoints.ToArray();
            }

            _colliders ??= new Collider[10];
        }

        private void InitializeCollisionBuffers(int count)
        {
            if (_isCollidingThisFrame == null || _isCollidingThisFrame.Length != count)
            {
                _isCollidingThisFrame = new bool[count];
            }

            if (_wasCollided == null || _wasCollided.Length != count)
            {
                _wasCollided = new bool[count];
            }

            if (_hitPoints == null || _hitPoints.Length != count)
            {
                _hitPoints = new Vector3[count];
            }

            Array.Clear(_isCollidingThisFrame, 0, _isCollidingThisFrame.Length);
        }

        /// <summary>
        /// Precision sphere-to-box collision solver.
        /// Transforms sphere coordinates to local BoxCollider space to handle deep penetrations
        /// and compute accurate contact surface normals and points.
        /// </summary>
        private Vector3 ResolveCollisions(Vector3 position, float radius, LayerMask layerMask, out bool collided, out Vector3 hitPoint)
        {
            collided = false;
            hitPoint = Vector3.zero;

            if (_colliders == null || _colliders.Length == 0)
                return position;

            int size = Physics.OverlapSphereNonAlloc(position, radius, _colliders, layerMask);

            for (int i = 0; i < size; i++)
            {
                Collider col = _colliders[i];
                if (col is MeshCollider) continue;

                collided = true;
                if (col is BoxCollider box)
                {
                    position = ResolveSphereBoxCollision(position, radius, box);

                    hitPoint = box.ClosestPoint(position);
                }
                else
                {
                    // Standard fallback the closest point resolution for other types of colliders
                    Vector3 closest = col.ClosestPoint(position);
                    float dist = Vector3.Distance(position, closest);
                    if (dist < radius)
                    {
                        Vector3 correct = (position - closest) * (radius / dist);
                        position = closest + correct;
                        hitPoint = closest;
                    }
                }
            }

            return position;
        }

        private Vector3 ResolveSphereBoxCollision(Vector3 sphereCenter, float radius, BoxCollider box)
        {
            // Transform sphere center to box local space
            Vector3 localCenter = box.transform.InverseTransformPoint(sphereCenter);

            MinMaxAABB aabb = MinMaxAABB.CreateFromCenterAndExtents(box.center, box.size);

            // Clamped coordinates to find closest point inside or on box in local space
            Vector3 closestLocal = new(
                Mathf.Clamp(localCenter.x, aabb.Min.x, aabb.Max.x),
                Mathf.Clamp(localCenter.y, aabb.Min.y, aabb.Max.y),
                Mathf.Clamp(localCenter.z, aabb.Min.z, aabb.Max.z)
            );

            // Distance in local space
            if (closestLocal == localCenter)
            {
                // Sphere center is INSIDE the box. Push it out to the nearest face.
                float dx1 = localCenter.x - aabb.Min.x;
                float dx2 = aabb.Max.x - localCenter.x;
                float dy1 = localCenter.y - aabb.Min.y;
                float dy2 = aabb.Max.y - localCenter.y;
                float dz1 = localCenter.z - aabb.Min.z;
                float dz2 = aabb.Max.z - localCenter.z;

                float minDist = Mathf.Min(dx1, Mathf.Min(dx2, Mathf.Min(dy1, Mathf.Min(dy2, Mathf.Min(dz1, dz2)))));

                if (minDist == dx1) closestLocal.x = aabb.Min.x;
                else if (minDist == dx2) closestLocal.x = aabb.Max.x;
                else if (minDist == dy1) closestLocal.y = aabb.Min.y;
                else if (minDist == dy2) closestLocal.y = aabb.Max.y;
                else if (minDist == dz1) closestLocal.z = aabb.Min.z;
                else closestLocal.z = aabb.Max.z;

                Vector3 closestWorld = box.transform.TransformPoint(closestLocal);
                Vector3 localNormal;

                // Better normal calculation from the face we projected onto
                if (minDist == dx1) localNormal = Vector3.left;
                else if (minDist == dx2) localNormal = Vector3.right;
                else if (minDist == dy1) localNormal = Vector3.down;
                else if (minDist == dy2) localNormal = Vector3.up;
                else if (minDist == dz1) localNormal = Vector3.back;
                else localNormal = Vector3.forward;

                Vector3 normal = box.transform.TransformDirection(localNormal);

                return closestWorld + normal * radius;
            }
            else
            {
                // Sphere center is OUTSIDE the box. Push out if overlapping.
                Vector3 closestWorld = box.transform.TransformPoint(closestLocal);
                float distToSurface = Vector3.Distance(sphereCenter, closestWorld);

                if (distToSurface < radius)
                {
                    Vector3 normal = (sphereCenter - closestWorld).normalized;
                    if (normal == Vector3.zero)
                        normal = box.transform.up;
                    return closestWorld + normal * radius;
                }
            }

            return sphereCenter;
        }
    }
}
