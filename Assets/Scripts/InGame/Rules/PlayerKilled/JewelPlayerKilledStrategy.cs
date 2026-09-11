using System;
using InGame.Health;
using September.InGame.Jewelry.Drop;
using UnityEngine;

namespace September.InGame.Rules
{
    [Serializable]
    public class JewelPlayerKilledStrategy : IPlayerKilledStrategy
    {
        [SerializeField] private JewelryDropHitProcessor _defaultDamageProcessor = new(DropType.Drop);

        public void ProcessKillEvent(HitData hitData)
        {
            _defaultDamageProcessor.DropType = hitData.HitActionType == HitActionType.Damage ? DropType.Drop : DropType.Pickup;
            _defaultDamageProcessor.OnHitTaken(hitData);
        }
    }
}
