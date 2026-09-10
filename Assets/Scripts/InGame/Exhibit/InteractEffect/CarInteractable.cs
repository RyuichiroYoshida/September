using System;
using System.Collections.Generic;
using CRISound;
using Fusion;
using InGame.Health;
using September.Common;
using September.InGame;
using September.InGame.Common;
using September.InGame.Effect;
using UnityEngine;
using UnityEngine.Splines;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace InGame.Exhibit.InteractEffect
{
    public class CarInteractable : NetworkBehaviour
    {
        [Header("Spline")] [SerializeField] private SplineContainer _spline;

        [Header("Move")] [SerializeField] private float _speed = 6f;
        [SerializeField] private Transform _target;
        [SerializeField] private bool _loop = false;

        [Header("EffectSettings")] [SerializeField]
        private float _hitEffectYOffset = 0.1f;

        [Header("配置位置")] private Vector3 _initialPos;
        private Quaternion _initialRot;

        [Header("減速設定")] [SerializeField] private float _delayTime = 0.3f;
        [SerializeField] private float _slowdownRadius = 3f;
        [SerializeField] private float _minSpeedFactor = 0.25f;
        [SerializeField] private AnimationCurve _slowdownCurve;

        private float _delayRemaining;
        private int _lastDelayKnotIndex = -1;
        private EffectSpawner _effectSpawner;

        [Networked] private bool IsMoving { get; set; }
        [Networked] private float Progress { get; set; }
        private float _approxCount;

        private readonly List<Vector3> _knotWorldPositions = new();

        [SerializeField] private int _damage;
        private PlayerRef _ownerRef;
        [SerializeField] private List<Transform> _effectPositions = new();
        private readonly List<EffectID> _ids = new();

        AudioBroadcaster _audioBroadcaster;

        public override void Render()
        {
            if (_spline == null || _target == null)
                return;

            if (IsMoving)
                ApplyPose(Progress);
            else
                _target.SetPositionAndRotation(_initialPos, _initialRot);
        }

        public override void Spawned()
        {
            _initialPos = _target.position;
            _initialRot = _target.rotation;
            if (_spline != null)
                _approxCount = ApproxLength(_spline, 200);

            CacheKnotWorldPositions();
            if (HasStateAuthority)
            {
                _effectSpawner = StaticServiceLocator.Instance.Get<EffectSpawner>();
            }
            _audioBroadcaster = GetComponent<AudioBroadcaster>();
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || !IsMoving)
                return;

            Move();
        }

        [Rpc]
        public void RPC_OnInteractStart(PlayerRef playerRef)
        {
            OnInteractStart(playerRef);
        }

        private void OnInteractStart(PlayerRef playerRef)
        {
            _ownerRef = playerRef;
            IsMoving = true;
            Progress = 0f;
            _delayRemaining = 0;
            _lastDelayKnotIndex = -1;
            //_audioBroadcaster.PlaySoundAtPosition(SoundCues.SE.Car_Interact.Name, this.transform.position);
            _audioBroadcaster.PlaySoundAtTransformFollow(SoundCues.SE.Car_Interact.Name, Object);
        }

        public void EffectSpawn()
        {
            foreach (var trm in _effectPositions)
            {
                if (_effectSpawner == null) continue;

                EffectID id =
                    _effectSpawner.RequestPlayLoopEffect(
                    EffectType.CarDash,
                    trm.position,
                    transform.rotation,
                    transform);

                _ids.Add(id);
            }
        }

        private void OnInteractEnd()
        {
            _ownerRef = PlayerRef.None;
            foreach (var id in _ids)
            {
                _effectSpawner?.StopEffect(id);
            }
            CRIAudio.Stop3DSEFromCueName(SoundCues.SE.Car_Interact.Name);
        }

        private void Move()
        {
            if (_approxCount <= 0f)
                return;
            Spline spline = _spline.Spline;
            // Spline上の現在位置と姿勢を算出する
            spline.Evaluate(Progress, out var localPos, out _, out _);
            Vector3 worldPos = _spline.transform.TransformPoint(localPos);

            if (_delayRemaining > 0f)
            {
                _delayRemaining -= Runner.DeltaTime;
                if (_delayRemaining < 0f)
                    _delayRemaining = 0f;

                return;
            }

            float slowFactor = 1f;
            int nearestIndex = -1;
            float nearestDist = float.MaxValue;

            if (_knotWorldPositions.Count > 0 && _slowdownRadius > 0f)
            {
                // 最も近いノットを調べる
                for (int i = 0; i < _knotWorldPositions.Count; i++)
                {
                    float d = Vector3.Distance(worldPos, _knotWorldPositions[i]);
                    if (d < nearestDist)
                    {
                        nearestDist = d;
                        nearestIndex = i;
                    }
                }

                if (nearestDist <= _slowdownRadius)
                {
                    float x = Mathf.InverseLerp(0f, _slowdownRadius, nearestDist);
                    float curve = _slowdownCurve != null ? Mathf.Clamp01(_slowdownCurve.Evaluate(x)) : x;
                    slowFactor = Mathf.Lerp(_minSpeedFactor, 1f, curve);

                    if (nearestIndex != -1 && nearestIndex != _lastDelayKnotIndex && nearestDist <= 0.2f)
                    {
                        _delayRemaining = _delayTime;
                        _lastDelayKnotIndex = nearestIndex;
                    }
                }
            }

            Progress += Runner.DeltaTime * _speed * slowFactor / _approxCount;

            // Stop処理
            if (Progress >= 1f)
            {
                if (_loop)
                {
                    Progress -= 1f;
                    _lastDelayKnotIndex = -1;
                }
                else
                {
                    Progress = 1f;
                    IsMoving = false;
                    // 元の展示位置へ戻す
                    _target.SetPositionAndRotation(_initialPos, _initialRot);
                    OnInteractEnd();
                }
            }
        }

        private void ApplyPose(float t)
        {
            Spline spline = _spline.Spline;
            spline.Evaluate(t, out var lp, out var lt, out var lu);

            Vector3 pos = _spline.transform.TransformPoint(lp);
            Vector3 tan = _spline.transform.TransformDirection(lt).normalized;
            Vector3 up = _spline.transform.TransformDirection(lu).normalized;

            if (tan.sqrMagnitude < 1e-6f)
                tan = _target.forward;
            if (up.sqrMagnitude < 1e-6f)
                up = Vector3.up;

            _target.SetPositionAndRotation(pos, Quaternion.LookRotation(tan, up));
        }

        // 点を取得してワールド座標に変換
        private void CacheKnotWorldPositions()
        {
            _knotWorldPositions.Clear();
            Spline spline = _spline?.Spline;

            if (spline != null)
                foreach (var knot in spline)
                {
                    Vector3 wp = _spline.transform.TransformPoint(knot.Position);
                    _knotWorldPositions.Add(wp);
                }
        }

        // Splineの長さを取得する
        private static float ApproxLength(SplineContainer container, int samples)
        {
            if (container == null || container.Spline == null)
                return 0f;

            Spline spline = container.Spline;
            samples = Mathf.Max(2, samples);
            Vector3 prev = container.transform.TransformPoint(spline.EvaluatePosition(0f));
            float len = 0f;

            for (int i = 1; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector3 p = container.transform.TransformPoint(spline.EvaluatePosition(t));
                len += Vector3.Distance(prev, p);
                prev = p;
            }

            return len;
        }

        private void OnCollisionEnter(Collision other)
        {
            if (!other.transform.root.CompareTag("Player")) return;
            if(!IsMoving) return;
            var nObj = other.transform.GetComponentInParent<NetworkObject>();
            RPC_OnHit(nObj);
        }


        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_OnHit(NetworkObject other)
        {
            if (!Runner.IsServer) return;
            if(StaticServiceLocator.Instance.Get<InGameManager>().PlayerDataDic[_ownerRef] == other) return;
            Vector3 playerPos = other.transform.position + Vector3.up * _hitEffectYOffset;
            _effectSpawner?.RequestPlayOneShotEffect(
                EffectType.HitNormal,
                playerPos,
                other.transform.rotation);
            var damage = other.GetComponentInParent<IDamageable>();
            var hitData = new HitData(HitActionType.RangedDamage, _damage, _ownerRef, damage.OwnerPlayerRef);
            damage.TakeHit(ref hitData);
        }
    }
}
