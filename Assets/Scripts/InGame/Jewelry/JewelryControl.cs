using Cysharp.Threading.Tasks;
using DG.Tweening;
using Fusion;
using UnityEngine;

namespace InGame.Jewelry
{
    public class JewelryControl : NetworkBehaviour
    {
        [SerializeField] private float _gravity = 20f;
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private float _groundCheckDistance = 0.2f;
        [SerializeField] private float _getCoolTime = 3f;
        [SerializeField] private Collider _collider;
        [SerializeField] private Rigidbody _rigidbody;

        private Vector3 Velocity { get => _rigidbody.linearVelocity; set => _rigidbody.linearVelocity = value; }
        private bool _isGrounded;

        private bool _physicsEnabled = true;

        [Header("Get Move")]
        [SerializeField] private Vector3 _offset = new(0f, 1.5f, 0f);
        [SerializeField] private AnimationCurve _curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve _heightOffsetCurve;
        [SerializeField] private float _heightOffsetScale = 1f;

        public void Start()
        {
            Initialize().Forget();
        }

        public async UniTask Initialize()
        {
            _collider.enabled = false;
            await UniTask.WaitForSeconds(_getCoolTime, cancellationToken: destroyCancellationToken);
            _collider.enabled = true;
        }

        public void RandomThrow(float horizontalThrowForce, float upwardThrowForce)
        {
            Vector3 dir = Random.insideUnitSphere;
            dir.y = 0f;
            dir.Normalize();

            Vector3 force = dir * horizontalThrowForce + Vector3.up * upwardThrowForce;
            Throw(force);
        }

        public void Throw(Vector3 velocity)
        {
            Velocity = velocity;
        }

        /// <summary>
        /// 目標位置に向かって放物運動を行います
        /// </summary>
        /// <param name="position">目標位置</param>
        /// <param name="maxHeight">最高到達点の高さ（変位）　値は0以上にしてください</param>
        public void ThrowTo(Vector3 position, float maxHeight)
        {
            if (maxHeight < 0)
            {
                maxHeight = Mathf.Abs(position.y - transform.position.y) + 2f;
                Debug.LogWarning("maxHeightは正の値にしてください", this);
            }
            float vy = Mathf.Sqrt(2 * _gravity * maxHeight); // y方向の初速
            float a = vy * vy - 2 * _gravity * (position.y - transform.position.y);
            if (a < 0)
            {
                Debug.LogWarning("maxHeightが最高到達点より低い位置にあるため、放物線を求めることができませんでした", this);
                return;
            }
            float t = (vy + Mathf.Sqrt(a)) / _gravity; // 最終地点に到達するまでの時間
            Vector3 vh = (position - transform.position) / t; // 水平方向の初速
            Vector3 v = new(vh.x, vy, vh.z); // 初速
            Velocity = v;
        }

        /// <summary>
        /// 目標位置に向かって放物運動を行います
        /// </summary>
        public void ThrowTo(Vector3 position, float maxHeight, float duration)
        {
            float dy = position.y - transform.position.y;
            float gravity = 2 * maxHeight / (duration * duration) * Mathf.Pow(1 + Mathf.Sqrt(1 - dy / maxHeight), 2);

            if (maxHeight < 0)
            {
                maxHeight = Mathf.Abs(position.y - transform.position.y) + 2f;
                Debug.LogWarning("maxHeightは正の値にしてください", this);
            }

            float vy = Mathf.Sqrt(2 * gravity * maxHeight); // y方向の初速
            float a = vy * vy - 2 * gravity * (position.y - transform.position.y);
            if (a < 0)
            {
                Debug.LogWarning("maxHeightが最高到達点より低い位置にあるため、放物線を求めることができませんでした", this);
                return;
            }
            Vector3 vh = (position - transform.position) / duration; // 水平方向の初速
            Vector3 v = new(vh.x, vy, vh.z); // 初速

            _gravity = gravity;
            Velocity = v;
        }

        /// <summary>
        /// 物理判定を無効化し、目標位置に向かって放物運動を行います
        /// </summary>
        public void ThrowToNonPhysics(Vector3 position, float maxHeight, float duration, float delay, AnimationCurve verticalEase, AnimationCurve horizontalEase)
        {
            _physicsEnabled = false;
            _collider.enabled = false;
            _rigidbody.isKinematic = true;

            float dy = position.y - transform.position.y;
            float gravity = 2 * maxHeight / (duration * duration) * Mathf.Pow(1 + Mathf.Sqrt(1 - dy / maxHeight), 2);

            if (maxHeight < 0)
            {
                maxHeight = Mathf.Abs(position.y - transform.position.y) + 2f;
                Debug.LogWarning("maxHeightは正の値にしてください", this);
            }

            float vy = Mathf.Sqrt(2 * gravity * maxHeight); // y方向の初速
            float a = vy * vy - 2 * gravity * (position.y - transform.position.y);
            if (a < 0)
            {
                Debug.LogWarning("maxHeightが最高到達点より低い位置にあるため、放物線を求めることができませんでした", this);
                return;
            }
            Vector3 vh = (position - transform.position) / duration; // 水平方向の初速
            Vector3 v = new(vh.x, vy, vh.z); // 初速

            Vector3 startPos = transform.position;

            DOTween.To(() => 0f, t =>
            {
                float tv = verticalEase.Evaluate(t) * duration;
                float th = horizontalEase.Evaluate(t) * duration;

                float y = v.y * tv - 0.5f * gravity * tv * tv;

                Vector3 p = new(v.x * th, y, v.z * th);
                transform.position = startPos + p;
            }, 1, duration).SetEase(Ease.Linear)
            .SetDelay(delay)
            .onComplete += SetGrounded;
        }

        public async UniTask PlayGetMove(Transform target)
        {
            const float duration = 1f;

            _physicsEnabled = false;
            _collider.enabled = false;
            _rigidbody.isKinematic = true;

            Vector3 startPos = transform.position;

            await DOTween.To(() => 0f, t =>
            {
                Vector3 heightOffset = Vector3.up * (_heightOffsetCurve.Evaluate(t) * _heightOffsetScale);

                float t0 = _curve.Evaluate(t);
                Vector3 targetPos = target.position + target.rotation * _offset;

                transform.position = Vector3.LerpUnclamped(startPos, targetPos, t0) + heightOffset;
            }, 1f, duration).SetEase(Ease.Linear);
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _isGrounded || !_physicsEnabled)
                return;

            Velocity += Vector3.down * _gravity * Runner.DeltaTime;

            if (IsGrounded())
            {
                SetGrounded();
            }
        }

        private bool IsGrounded()
        {
            Vector3 origin = transform.position + Vector3.up * 0.1f;

            if (Physics.Raycast(origin, Vector3.down, out _, _groundCheckDistance + .1f, _groundLayer))
            {
                return true;
            }
            return false;
        }

        private void SetGrounded()
        {
            _collider.enabled = true;
            _rigidbody.constraints = RigidbodyConstraints.FreezeAll;
            _isGrounded = true;
        }
    }
}
