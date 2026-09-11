using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace InGame.Common.FootIk
{
    /// <summary>
    /// 各足の下へ Raycast して地面高さを求め、HumanoidFootIkJob を AnimationClipPlayer の出力に差し込んで
    /// 足が地面へ沈む/浮く「足滑り」を補正する。
    /// Humanoid Avatar が割り当てられた Animator が必要 (無ければ警告して無効化)。
    /// </summary>
    [RequireComponent(typeof(AnimationClipPlayer))]
    [DisallowMultipleComponent]
    public sealed class HumanoidFootGroundIk : MonoBehaviour
    {
        [Header("接地判定")]
        [SerializeField, Tooltip("地面とみなすレイヤー。自分自身のコライダーは自動で除外する")]
        private LayerMask _groundLayer = ~0;
        [SerializeField, Tooltip("足ボーンからどれだけ上から Ray を撃つか (m)")]
        private float _rayStartHeight = 0.5f;
        [SerializeField, Tooltip("足ボーンからどれだけ下まで地面を探すか (m)")]
        private float _rayDepth = 0.6f;
        [SerializeField, Tooltip("足ボーン原点から足裏までの高さ (m)。モデルに合わせて調整")]
        private float _footHeightOffset = 0.08f;

        [Header("適用")]
        [SerializeField, Range(0f, 1f), Tooltip("IK の適用率。空中では自動で 0 に落とす")]
        private float _weight = 1f;
        [SerializeField, Tooltip("適用率の遷移速度 (1/秒)")]
        private float _weightBlendSpeed = 8f;
        [SerializeField, Tooltip("地面の傾きに足首を沿わせる")]
        private bool _alignFootToSlope = true;

        private const int HitBufferSize = 8;

        private AnimationClipPlayer _player;
        private Transform _leftFoot;
        private Transform _rightFoot;
        private AnimationScriptPlayable _playable;
        private float _currentWeight;
        private readonly RaycastHit[] _hits = new RaycastHit[HitBufferSize];

        /// <summary>外部 (空中判定など) から適用率を制御する。</summary>
        public float Weight
        {
            get => _weight;
            set => _weight = Mathf.Clamp01(value);
        }

        private void Awake()
        {
            _player = GetComponent<AnimationClipPlayer>();
        }

        private void OnEnable()
        {
            _player.BeforeEvaluate += OnBeforeEvaluate;
        }

        private void OnDisable()
        {
            _player.BeforeEvaluate -= OnBeforeEvaluate;
            // 無効化中はジョブを素通しにする (グラフから外さず重み 0)。
            if (_playable.IsValid())
            {
                _playable.SetJobData(new HumanoidFootIkJob { Weight = 0f });
            }
            _currentWeight = 0f;
        }

        private void OnBeforeEvaluate()
        {
            if (!EnsureInstalled()) return;

            var job = new HumanoidFootIkJob
            {
                Left = Probe(_leftFoot),
                Right = Probe(_rightFoot),
            };

            // 両足とも地面が見つからない (空中) なら滑らかに 0 へ。
            float targetWeight = (job.Left.Valid > 0f || job.Right.Valid > 0f) ? _weight : 0f;
            _currentWeight = Mathf.MoveTowards(_currentWeight, targetWeight, _weightBlendSpeed * Time.deltaTime);
            job.Weight = _currentWeight;

            _playable.SetJobData(job);
        }

        /// <summary>グラフが再生成された場合も含め、ジョブが出力に差し込まれている状態にする。</summary>
        private bool EnsureInstalled()
        {
            if (_playable.IsValid()) return true;
            if (!_player.IsValid) return false;

            var animator = _player.Animator;
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                Debug.LogWarning($"[HumanoidFootGroundIk] {name}: Humanoid Avatar を持つ Animator が無いため足 IK を無効化します。", this);
                enabled = false;
                return false;
            }

            _leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (_leftFoot == null || _rightFoot == null)
            {
                Debug.LogWarning($"[HumanoidFootGroundIk] {name}: 足ボーンが Avatar に割り当てられていないため足 IK を無効化します。", this);
                enabled = false;
                return false;
            }

            _playable = AnimationScriptPlayable.Create(_player.Graph, new HumanoidFootIkJob(), 1);
            if (!_player.TryInstallOutputProcessor(_playable))
            {
                _playable.Destroy();
                return false;
            }
            return true;
        }

        /// <summary>前フレームの足ボーン位置を基準に真下へ Ray を撃ち、自分以外の最も近い地面を返す。</summary>
        private HumanoidFootIkJob.FootTarget Probe(Transform foot)
        {
            var result = new HumanoidFootIkJob.FootTarget { Valid = 0f, Normal = Vector3.up };
            Vector3 origin = foot.position + Vector3.up * _rayStartHeight;
            float maxDistance = _rayStartHeight + _rayDepth;

            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, maxDistance, _groundLayer, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.collider.transform.IsChildOf(transform)) continue; // 自分のコライダー
                if (hit.distance >= nearest) continue;

                nearest = hit.distance;
                result.Valid = 1f;
                result.GroundY = hit.point.y + _footHeightOffset;
                result.Normal = hit.normal;
                result.AlignToNormal = _alignFootToSlope ? 1f : 0f;
            }
            return result;
        }

        private void OnDrawGizmosSelected()
        {
            if (_leftFoot == null || _rightFoot == null) return;
            Gizmos.color = Color.cyan;
            foreach (var foot in new[] { _leftFoot, _rightFoot })
            {
                Vector3 origin = foot.position + Vector3.up * _rayStartHeight;
                Gizmos.DrawLine(origin, origin + Vector3.down * (_rayStartHeight + _rayDepth));
            }
        }
    }
}
