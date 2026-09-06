using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Health;
using September.InGame.Kraken.Attack;
using UnityEngine;
using Object = UnityEngine.Object;

namespace September.InGame.Kraken.Animations
{
    public class TentacleController
    {
        private readonly ArmSettings _armSettings;
        private readonly KrakenSettings _krakenSettings;

        public bool IsAttacking => _armSettings.IsAttacking;
        public Transform ArmRoot => _armSettings.ArmRoot;

        public TentacleController(ArmSettings armSettings, KrakenSettings krakenSettings, Kraken kraken)
        {
            _armSettings = armSettings;
            _krakenSettings = krakenSettings;

            armSettings.ArmHitChecker.OnHit += col => OnHitAction(armSettings.AlreadyHits, col);

            armSettings.TentacleConstraintSolver.OnCollided += hitPos => OnPhysicalCollision(hitPos, _armSettings, _krakenSettings, kraken);

            armSettings.EnablePhysics = false;
        }

        public void StartAttack(NetworkRunner runner)
        {
            _armSettings.TentacleConstraintSolver.ResetState();
            _armSettings.IsAttacking = true;
            _armSettings.CollidedPoints.Clear();

            // ローカルテスト用。実際のゲーム中では処理されない想定
            if (runner == null)
            {
                _armSettings.EnablePhysics = true;
                return;
            }

            _armSettings.StartAttackTick = runner.Tick;
            _armSettings.EnablePhysics = false;
        }

        public void StartAreaAttack(NetworkRunner runner, PredictionParticle particle)
        {
            _armSettings.AreaHitCapsule =
                HitCapsule.Create(
                    runner,
                    new CapsuleShape(particle.ForwardEndPos, particle.BackEndPos, _krakenSettings.AreaHitRadius),
                    _krakenSettings.AreaHitDuration,
                    _krakenSettings.AreaAttackHitLayer,
                    col => OnHitAction(_armSettings.AlreadyHits, col)
                );
        }

        public void Tick(NetworkRunner runner)
        {
            UpdatePhysicsState(runner);
            UpdateArmAttackState(runner);
            UpdateAreaAttackState(runner);
        }

        private void UpdatePhysicsState(NetworkRunner runner)
        {
            int targetTick = (int)(_krakenSettings.TentacleEnablePhysicsTime * runner.TickRate);
            int elapsedTick = runner.Tick - _armSettings.StartAttackTick;
            if (_armSettings.IsAttacking && elapsedTick > targetTick)
            {
                _armSettings.EnablePhysics = true;
            }
        }

        private void UpdateArmAttackState(NetworkRunner runner)
        {
            int armStartTick = (int)(_krakenSettings.ArmStartTime * runner.TickRate);
            int armEndTick = (int)(_krakenSettings.ArmEndTime * runner.TickRate);
            int elapsedTick = runner.Tick - _armSettings.StartAttackTick;

            if (_armSettings.IsAttacking && armStartTick < elapsedTick && elapsedTick < armEndTick)
            {
                if (!_armSettings.ArmHitChecker.IsActive) _armSettings.ArmHitChecker.StartHitCheck();
            }
            else
            {
                if (_armSettings.ArmHitChecker.IsActive) _armSettings.ArmHitChecker.EndHitCheck();
            }
        }

        private void UpdateAreaAttackState(NetworkRunner runner)
        {
            if (!_armSettings.AreaHitCapsule.ExpiredOrNotRunning(runner))
            {
                _armSettings.AreaHitCapsule.Cast();
            }
        }

        private void OnHitAction(HashSet<Collider> alreadyHits, Collider hitCollider)
        {
            if (alreadyHits.Contains(hitCollider)) return;

            float rayWorldHeight = _krakenSettings.HitRayCastHeight + _krakenSettings.Root.position.y;
            Vector3 hitPos = hitCollider.ClosestPoint(_krakenSettings.Root.position);
            Vector3 rayOrigin = new(hitPos.x, rayWorldHeight, hitPos.z);
            float distance = rayWorldHeight - hitPos.y;

            if (Physics.Raycast(rayOrigin, Vector3.down, out _, distance, _krakenSettings.CeilingLayer))
            {
                Debug.DrawRay(rayOrigin, Vector3.down * distance, Color.red, 100f);
                return;
            }

            Debug.DrawRay(rayOrigin, Vector3.down * distance, Color.green, 100f);

            alreadyHits.Add(hitCollider);

            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                HitData hitData = new()
                {
                    HitActionType = HitActionType.RangedDamage, Amount = _krakenSettings.Damage,
                    ExecutorRef = _krakenSettings.OwnerPlayerRef, TargetRef = damageable.OwnerPlayerRef
                };

                damageable.TakeHit(ref hitData);
            }
        }

        public void LookAt(Vector3 target)
        {
            var root = _armSettings.ArmRoot;

            _armSettings.StartRotation = root.rotation;

            var forward = -root.transform.right;
            forward.y = 0;
            var dir = Vector3.ProjectOnPlane(target - root.position, Vector3.up).normalized;
            Debug.DrawRay(root.position, dir * 100f, Color.red, 3f);

            var rot = Quaternion.FromToRotation(forward, dir);
            Debug.Log($"{target} {forward} {dir} {rot.eulerAngles}", _armSettings.ArmRoot);

            root.rotation = rot * root.rotation;

            _armSettings.TargetPosition = target;
            _armSettings.TargetDirection = dir;
            _armSettings.TargetDirectionPlaneNormal = root.transform.forward;
        }

        public async UniTask PlayAnimation(CancellationToken token = default)
        {
            await _armSettings.Animator.PlayAsync(_krakenSettings.AnimationName, 0, 0f, cancellationToken: token);
            await _armSettings.Animator.WaitState(_krakenSettings.EndStateName, cancellationToken: token);
            _armSettings.ArmRoot.rotation = _armSettings.StartRotation;
            Debug.Log($"{_krakenSettings.AnimationName} {_armSettings.StartRotation.eulerAngles}", _armSettings.ArmRoot);
        }

        public void ResetState()
        {
            _armSettings.AlreadyHits.Clear();
            _armSettings.IsAttacking = false;
            _armSettings.EnablePhysics = false;
        }

        private static void OnPhysicalCollision(Vector3 hitPos, ArmSettings armSettings, KrakenSettings krakenSettings, Kraken kraken)
        {
            if (krakenSettings.SlamEffect == null) return;

            bool tooNear = false;
            foreach (Vector3 p in armSettings.CollidedPoints)
            {
                if ((hitPos - p).sqrMagnitude < krakenSettings.EffectDistance.Sqr())
                {
                    tooNear = true;
                    break;
                }
            }

            if (tooNear) return;

            armSettings.CollidedPoints.Add(hitPos);
            ParticleSystem particle = kraken.SlamParticlePool.Get();
            particle.transform.position = hitPos;
            Release().Forget();

            return;

            async UniTaskVoid Release()
            {
                await UniTask.WaitForSeconds(particle.main.duration);
                kraken.SlamParticlePool.Release(particle);
            }
        }
    }

    public static class NumberExtensions
    {
        public static float Sqr(this float num) => num * num;
    }

    /// <summary>
    /// 触手ごとに持たせるデータ
    /// </summary>
    [Serializable]
    public class ArmSettings
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _armRoot;
        [SerializeField] private HitChecker _armHitChecker;
        [SerializeField] private TentacleConstraintSolver _tentacleConstraintSolver;

        public Animator Animator => _animator;
        public Transform ArmRoot => _armRoot;
        public HitChecker ArmHitChecker => _armHitChecker;
        public TentacleConstraintSolver TentacleConstraintSolver => _tentacleConstraintSolver;
        public HashSet<Collider> AlreadyHits { get; } = new();

        [NonSerialized] public Quaternion StartRotation;
        [NonSerialized] public HitCapsule AreaHitCapsule;
        [NonSerialized] public int StartAttackTick;
        [NonSerialized] public bool IsAttacking;
        [NonSerialized] public List<Vector3> CollidedPoints = new();
        [NonSerialized] public Vector3 TargetPosition;
        [NonSerialized] public Vector3 TargetDirection;
        [NonSerialized] public Vector3 TargetDirectionPlaneNormal;

        public bool EnablePhysics
        {
            get => _tentacleConstraintSolver.EnablePhysicsConstraint;
            set => _tentacleConstraintSolver.EnablePhysicsConstraint = value;
        }
    }

    [Serializable]
    public class KrakenSettings
    {
        public Transform Root;
        public float TentacleEnablePhysicsTime;

        [Header("攻撃設定")]
        public float HitRayCastHeight;
        public float AreaHitDuration;
        public float AreaHitRadius;
        public int Damage;
        public LayerMask CeilingLayer;
        public LayerMask AttackPointRayHitLayer;
        public LayerMask AreaAttackHitLayer;
        public float ArmStartTime;
        public float ArmEndTime;

        [Header("アニメーション設定")]
        public string AnimationName;
        public string EndStateName;

        [Header("エフェクト設定")]
        public ParticleSystem SlamEffect;
        public float EffectDistance = 5f;
        public int DefaultParticlePoolCapacity = 20;

        [NonSerialized] public PlayerRef OwnerPlayerRef;
    }

    [Serializable]
    public class KrakenTentacles
    {
        [SerializeField] private ArmSettings[] _arms;
        public IReadOnlyList<ArmSettings> Arms => _arms;
    }
}
