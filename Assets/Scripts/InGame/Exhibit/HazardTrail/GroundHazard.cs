using Fusion;
using UnityEngine;

namespace InGame.Exhibit.HazardTrail
{
    /// <summary>
    /// 地面に設置されるハザードオブジェクトの基盤。
    /// </summary>
    public class GroundHazard : NetworkBehaviour
    {
        [Header("寿命設定")]
        [SerializeField] private float _lifetime = 3.0f;

        [Networked] private TickTimer LifeTimer { get; set; }
        [Networked] public PlayerRef OwnerPlayerRef { get; private set; }
        private IHazardEffect[] _effects;

        public void Initialize(PlayerRef owner,Vector3 position)
        {
            OwnerPlayerRef = owner;
            transform.position = position;
            LifeTimer = TickTimer.CreateFromSeconds(Runner, _lifetime);
        }

        public override void Spawned()
        {
            _effects = GetComponentsInChildren<IHazardEffect>();

            if (!HasStateAuthority) return;

            foreach (var effect in _effects)
            {
                effect.OnHazardSpawn(Runner, OwnerPlayerRef);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            // 寿命チェック
            if (LifeTimer.Expired(Runner))
            {
                Runner.Despawn(Object);
                return;
            }

            // 各効果（ダメージ等）の定期判定を実行
            if (_effects != null)
            {
                foreach (var effect in _effects)
                {
                    effect.OnHazardTick(Runner, OwnerPlayerRef);
                }
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_effects != null)
            {
                foreach (var effect in _effects)
                {
                    effect.OnHazardDespawn(runner);
                }
            }
            base.Despawned(runner, hasState);
        }
    }
}
