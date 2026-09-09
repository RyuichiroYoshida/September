using System.Collections.Generic;
using UnityEngine;

namespace September.Common
{
    /// <summary>アニメーション評価後、追加ボーンだけを重力・バネ・減衰で揺らす。</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class AccessorySpringBones : MonoBehaviour
    {
        [SerializeField] private AccessorySpringProfile _profile;
        [SerializeField, Tooltip("胴体へのめり込みを抑える球・カプセル。Triggerも指定可能。")]
        private Collider[] _bodyColliders = new Collider[0];
        private readonly List<Bone> _bones = new List<Bone>();
        private readonly List<Collider> _resolvedBodyColliders = new List<Collider>();
        private Vector3 _previousPosition;
        private bool _prepared;
        private bool _suppressExternalMotion;

        /// <summary>実際に揺れの計算に使われるボーン数。</summary>
        public int ActiveBoneCount => _bones.Count;

        private sealed class Bone
        {
            public Transform Transform;
            public Quaternion BaseRotation;
            public Vector3 LocalTip;
            public Vector3 Position;
            public Vector3 Velocity;
            public Vector3 TargetPosition;
            public Vector3 FrameOrigin;
            public Vector3 FrameAnimatedTip;
            public Quaternion FrameBaseWorldRotation;
            public Vector3 PreviousOrigin;
            public Vector3 PreviousAnimatedTip;
            public float Length;
            public float GravityWeight;
            public float GravityInfluence;
            public bool Initialized;
        }

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnDisable() => RestorePose();

        private void Update() => RestorePose();

        private void LateUpdate() => Simulate(Time.deltaTime);

        /// <summary>ジップラインなど、外部移動を揺れの慣性として扱わない状態を切り替える。</summary>
        public void SetExternalMotionSuppressed(bool suppressed)
        {
            _suppressExternalMotion = suppressed;
            _previousPosition = transform.position;
            foreach (var bone in _bones)
            {
                bone.Velocity = Vector3.zero;
                bone.GravityWeight = 1f;
                bone.Initialized = false;
            }
        }

        /// <summary>プロファイルを指定し、現在の姿勢からシミュレーションを作り直す。</summary>
        public void Configure(AccessorySpringProfile profile)
        {
            RestorePose();
            _profile = profile;
            Rebuild();
        }

        /// <summary>現在のTransform階層から追加ボーンを収集する。</summary>
        public void Rebuild()
        {
            RestorePose();
            _bones.Clear();
            _resolvedBodyColliders.Clear();
            if (!_profile) return;
            var seen = new HashSet<Transform>();
            var humanoidBones = new HashSet<Transform>();
            var animator = GetComponentInChildren<Animator>(true);
            if (animator && animator.isHuman)
                for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
                {
                    var bone = animator.GetBoneTransform((HumanBodyBones)i);
                    if (bone) humanoidBones.Add(bone);
                }
            foreach (string path in _profile.RootPaths ?? new string[0])
            {
                if (string.IsNullOrEmpty(path)) continue;
                Transform root = transform.Find(path) ?? FindByName(transform, path.Substring(path.LastIndexOf('/') + 1));
                if (!root)
                {
                    Debug.LogWarning($"AccessorySpringBones: ボーンが見つかりません: {path}", this);
                    continue;
                }
                var descendants = root.GetComponentsInChildren<Transform>(true);
                // 人体ボーンを含む枝は一括で除外し、走りそのものを変形させない。
                bool unsafeRoot = false;
                foreach (var child in descendants)
                    if (humanoidBones.Contains(child)) unsafeRoot = true;
                if (unsafeRoot) continue;
                foreach (var child in descendants)
                {
                    if (!seen.Add(child)) continue;
                    // 分岐点は動かさず、各枝を独立に処理する。
                    if (child.childCount > 1) continue;
                    Vector3 tip = child.childCount == 1 ? child.GetChild(0).localPosition : Vector3.up * _profile.TipLength;
                    if (child.childCount == 0 && child.parent)
                        tip = child.InverseTransformDirection(child.position - child.parent.position).normalized * _profile.TipLength;
                    if (tip.sqrMagnitude < 0.000001f) continue;
                    Vector3 tipPosition = child.TransformPoint(tip);
                    _bones.Add(new Bone
                    {
                        Transform = child,
                        BaseRotation = child.localRotation,
                        LocalTip = tip,
                        Length = Vector3.Distance(child.position, tipPosition),
                        Position = tipPosition,
                        TargetPosition = tipPosition,
                        PreviousOrigin = child.position,
                        PreviousAnimatedTip = tipPosition,
                        GravityWeight = 1f,
                        GravityInfluence = _profile.GetGravityInfluence(path),
                    });
                }
            }
            ResolveBodyColliders();
            _previousPosition = transform.position;
            if (_bones.Count == 0)
                Debug.LogWarning("AccessorySpringBones: 揺れ対象のボーンが0本です。ProfileのパスとPrefab階層を確認してください。", this);
        }

        private void ResolveBodyColliders()
        {
            bool useAutomaticColliders = _bodyColliders == null || _bodyColliders.Length == 0;
            if (useAutomaticColliders)
            {
                foreach (var collider in GetComponentsInParent<Collider>(true))
                {
                    if (!IsSupportedCollider(collider) || IsAccessoryCollider(collider) || _resolvedBodyColliders.Contains(collider)) continue;
                    _resolvedBodyColliders.Add(collider);
                }
                foreach (var collider in GetComponentsInChildren<Collider>(true))
                {
                    if (!IsSupportedCollider(collider) || IsAccessoryCollider(collider) || _resolvedBodyColliders.Contains(collider)) continue;
                    _resolvedBodyColliders.Add(collider);
                }
                return;
            }

            foreach (var collider in _bodyColliders)
                if (IsSupportedCollider(collider)) _resolvedBodyColliders.Add(collider);
        }

        private bool IsAccessoryCollider(Collider collider)
        {
            foreach (var bone in _bones)
                if (collider.transform == bone.Transform || collider.transform.IsChildOf(bone.Transform)) return true;
            return false;
        }

        private static bool IsSupportedCollider(Collider collider)
        {
            return collider && (collider is SphereCollider || collider is CapsuleCollider);
        }

        private static Transform FindByName(Transform parent, string name)
        {
            foreach (var child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        /// <summary>前フレームの揺れを取り除き、アニメーション評価前の姿勢に戻す。</summary>
        public void RestorePose()
        {
            if (!_prepared) return;
            foreach (var bone in _bones)
                if (bone.Transform) bone.Transform.localRotation = bone.BaseRotation;
            _prepared = false;
        }

        /// <summary>実行時・Editorプレビュー共通の揺れ計算。アニメーション評価後に呼ぶ。</summary>
        public void Simulate(float deltaTime)
        {
            if (!_profile || deltaTime <= 0f) return;
            bool reset = Vector3.Distance(transform.position, _previousPosition) > _profile.TeleportDistance;
            _previousPosition = transform.position;
            float duration = Mathf.Min(deltaTime * Mathf.Max(0.1f, _profile.SimulationSpeed), 0.1f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(duration / (1f / 120f)));
            float dt = duration / steps;

            // 親ボーンを動かす前にアニメーション姿勢を全対象分保存する。
            // これをしないと、親を揺らした後の子が「揺らされた親」をアニメーション姿勢として参照してしまう。
            foreach (var bone in _bones)
            {
                if (!bone.Transform) continue;
                bone.BaseRotation = bone.Transform.localRotation;
                bone.FrameOrigin = bone.Transform.position;
                bone.FrameAnimatedTip = bone.Transform.TransformPoint(bone.LocalTip);
                bone.FrameBaseWorldRotation = bone.Transform.rotation;
            }

            foreach (var bone in _bones)
            {
                var joint = bone.Transform;
                if (!joint) continue;
                Vector3 origin = joint.position;
                Vector3 animatedTarget = bone.FrameAnimatedTip;
                Vector3 animatedDirection = SafeDirection(animatedTarget - bone.FrameOrigin, joint.up);
                float length = Mathf.Max(0.001f, bone.Length);
                Vector3 originDelta = origin - bone.PreviousOrigin;
                Vector3 animatedDelta = animatedTarget - bone.PreviousAnimatedTip;
                float motionSpeed = (_suppressExternalMotion ? 0f : Mathf.Max(originDelta.magnitude, animatedDelta.magnitude)) /
                                    Mathf.Max(deltaTime, 0.0001f);
                float threshold = Mathf.Max(0.001f, _profile.DefaultPoseMotionThreshold);
                float gravityTarget = 1f - Mathf.InverseLerp(threshold, threshold * 4f, motionSpeed);
                float gravityBlendSpeed = Mathf.Max(0.1f, _profile.DefaultPoseSpeed);
                float gravityBlend = 1f - Mathf.Exp(-gravityBlendSpeed * duration);
                bone.GravityWeight = Mathf.Lerp(bone.GravityWeight, gravityTarget, gravityBlend);

                Vector3 gravityDirection = SafeDirection(_profile.Gravity, Vector3.down);
                Vector3 desiredDirection = Vector3.Slerp(
                    animatedDirection, gravityDirection, bone.GravityWeight * Mathf.Clamp01(bone.GravityInfluence)).normalized;
                Vector3 desiredTarget = origin + desiredDirection * length;
                if (reset || !bone.Initialized)
                {
                    bone.Position = animatedTarget;
                    bone.TargetPosition = animatedTarget;
                    bone.Velocity = Vector3.zero;
                    bone.Initialized = true;
                }
                else
                {
                    // 取付点の移動分だけ先端も運び、そこへ本体移動の慣性を加える。
                    bone.Position += originDelta;
                    if (_suppressExternalMotion) bone.Velocity = Vector3.zero;
                    else bone.Velocity -= originDelta / Mathf.Max(deltaTime, 0.0001f) * Mathf.Clamp01(_profile.Inertia);
                }

                // 動きが止まっている間は積分器を回さない。
                // 衝突押し戻しを速度として再利用すると、停止中でも高周波の振動が発生する。
                if (motionSpeed <= threshold)
                {
                    float alignment = 1f - Mathf.Exp(-Mathf.Max(0.1f, _profile.DefaultPoseSpeed) * duration);
                    Vector3 currentDirection = SafeDirection(bone.Position - origin, desiredDirection);
                    Vector3 stableDirection = Vector3.Slerp(currentDirection, desiredDirection, alignment).normalized;
                    bone.Position = origin + stableDirection * length;
                    ResolveCollisions(ref bone.Position, origin);
                    bone.TargetPosition = desiredTarget;
                    bone.Velocity = Vector3.zero;
                }
                else
                {
                    float maxTargetDistance = Mathf.Max(0.01f, _profile.MaxTargetSpeed) * duration;
                    bone.TargetPosition = Vector3.MoveTowards(bone.TargetPosition, desiredTarget, maxTargetDistance);
                    for (int i = 0; i < steps; i++)
                    {
                        Vector3 before = bone.Position;
                        bone.Velocity += (bone.TargetPosition - before) * Mathf.Max(0f, _profile.Stiffness) * dt;
                        bone.Velocity *= Mathf.Exp(-Mathf.Max(0f, _profile.Damping) * dt);
                        float maxVelocity = Mathf.Max(0.01f, _profile.MaxVelocity);
                        if (bone.Velocity.sqrMagnitude > maxVelocity * maxVelocity)
                            bone.Velocity = bone.Velocity.normalized * maxVelocity;
                        bone.Position += bone.Velocity * dt;
                        Vector3 direction = bone.Position - origin;
                        if (direction.sqrMagnitude < 0.000001f) direction = desiredDirection;
                        bone.Position = origin + direction.normalized * length;
                        ResolveCollisions(ref bone.Position, origin);
                        bone.Velocity = (bone.Position - before) / dt;
                    }
                }
                Vector3 simulatedDirection = SafeDirection(bone.Position - origin, animatedDirection);
                Quaternion worldCorrection = Quaternion.FromToRotation(animatedDirection, simulatedDirection);
                Quaternion parentRotation = joint.parent ? joint.parent.rotation : Quaternion.identity;
                Quaternion desiredWorldRotation = worldCorrection * bone.FrameBaseWorldRotation;
                Quaternion targetRotation = Quaternion.Inverse(parentRotation) * desiredWorldRotation;
                joint.localRotation = Quaternion.Slerp(bone.BaseRotation, targetRotation, Mathf.Clamp01(_profile.Weight));
                bone.PreviousOrigin = joint.position;
                bone.PreviousAnimatedTip = animatedTarget;
            }
            _prepared = true;
        }

        private void ResolveCollisions(ref Vector3 position, Vector3 origin)
        {
            float radius = Mathf.Max(0f, _profile.CollisionRadius);
            if (radius <= 0f) return;
            foreach (var collider in _resolvedBodyColliders)
            {
                if (!collider || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                Vector3 closest;
                float bodyRadius;
                Vector3 capsuleStart;
                Vector3 capsuleEnd;
                if (collider is SphereCollider sphere)
                {
                    closest = sphere.transform.TransformPoint(sphere.center);
                    bodyRadius = sphere.radius * MaxAbsComponent(sphere.transform.lossyScale);
                    capsuleStart = capsuleEnd = closest;
                }
                else if (collider is CapsuleCollider capsule)
                {
                    Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
                    Vector3 axisScale = capsule.transform.TransformDirection(axis);
                    float axisLength = axisScale.magnitude;
                    axisScale /= Mathf.Max(axisLength, 0.0001f);
                    bodyRadius = capsule.radius * MaxAbsComponent(capsule.transform.lossyScale);
                    float halfSegment = Mathf.Max(0f, capsule.height * axisLength * 0.5f - bodyRadius);
                    closest = capsule.transform.TransformPoint(capsule.center);
                    capsuleStart = closest - axisScale * halfSegment;
                    capsuleEnd = closest + axisScale * halfSegment;
                }
                else continue;

                Vector3 nearest = ClosestPointOnSegment(capsuleStart, capsuleEnd, position);
                Vector3 originNearest = ClosestPointOnSegment(capsuleStart, capsuleEnd, origin);
                // 付け根が胴体の中にある節は、外へ押し出すと鎖全体が丸まる。
                // その節は衝突判定の起点にできないため、先端だけが胴体外に出てから補正する。
                if (Vector3.Distance(origin, originNearest) < bodyRadius + radius) continue;
                Vector3 away = position - nearest;
                float distance = away.magnitude;
                float requiredDistance = bodyRadius + radius;
                if (distance >= requiredDistance) continue;
                if (distance < 0.0001f)
                {
                    away = position - closest;
                    away = Vector3.ProjectOnPlane(away, capsuleEnd - capsuleStart);
                    if (away.sqrMagnitude < 0.000001f) away = Vector3.ProjectOnPlane(position - origin, capsuleEnd - capsuleStart);
                    if (away.sqrMagnitude < 0.000001f) away = Vector3.right;
                    away.Normalize();
                }
                else away /= distance;
                position = nearest + away * requiredDistance;
            }
        }

        private static Vector3 ClosestPointOnSegment(Vector3 start, Vector3 end, Vector3 point)
        {
            Vector3 segment = end - start;
            float t = segment.sqrMagnitude > 0.000001f ? Vector3.Dot(point - start, segment) / segment.sqrMagnitude : 0f;
            return Vector3.Lerp(start, end, Mathf.Clamp01(t));
        }

        private static Vector3 SafeDirection(Vector3 direction, Vector3 fallback)
        {
            return direction.sqrMagnitude > 0.000001f ? direction.normalized : fallback.normalized;
        }

        private static float MaxAbsComponent(Vector3 value)
        {
            return Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
