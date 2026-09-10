using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Jewelry.Common;
using September.Common;
using September.InGame.Effect;
using September.InGame.Fields;
using September.InGame.Jewelry;
using UnityEngine;

namespace InGame.Jewelry
{
    public class Jewelry : NetworkBehaviour, IJewelry
    {
        [Header("この宝石のパラメータ群"), SerializeField] JewelryInfo _jewelryParams;
        [SerializeField] JewelryControl _jewelryControl;
        [SerializeField] ParticleSystemRenderer[] _renderers;

        public JewelryInfo JewelryParams => _jewelryParams;
        public JewelryControl JewelryControl => _jewelryControl;

        private TickTimer _despawnTimer;

        public override void Spawned()
        {
            _despawnTimer = TickTimer.CreateFromSeconds(Runner, _jewelryParams.LifeTime);
        }

        public override void FixedUpdateNetwork()
        {
            // 自動消滅時間を過ぎたらデスポーン
            if (_despawnTimer.Expired(Runner))
            {
                Despawn();
            }

            // 範囲外に出たら即時デスポーン
            if (OutOfFieldArea.I.IsOutOfField(transform.position + Vector3.up * _jewelryParams.FallDepth))
            {
                PlayPickupEffect();
                Despawn();
            }

            return;

            // 自動消滅時の処理
            void Despawn()
            {
                Runner.Despawn(Object);
                DespawnedJewelryRepository.AddDespawnedJewelry(_jewelryParams.JewelryType);
            }
        }

        public override void Render()
        {
            // 消滅前の点滅演出
            if (_despawnTimer.RemainingTime(Runner) <= _jewelryParams.BlinkStartRemainingTime)
            {
                bool blink = Runner.SimulationTime * _jewelryParams.BlinkSpeed % 1f > 0.5f;

                foreach (ParticleSystemRenderer r in _renderers)
                {
                    r.enabled = blink;
                }
            }
        }

        public async UniTask PickupFrom(PlayerRef player)
        {
            var obj = Runner.GetPlayerObject(player);
            var container = obj.GetComponentInChildren<IJewelryContainer>();

            if (container == null) return;

            await _jewelryControl.PlayGetMove(obj.transform);

            container.PickUp(this);
        }

        public void PlayPickupEffect()
        {
            EffectSpawner effectSpawner = StaticServiceLocator.Instance.Get<EffectSpawner>();
            effectSpawner.RequestPlayOneShotEffect(_jewelryParams.PickupEffectType, transform.position + _jewelryParams.PickupEffectOffset, transform.rotation);
        }
    }
}
