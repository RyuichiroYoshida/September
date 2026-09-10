using System;
using InGame.Health;
using September.InGame.Jewelry.Drop;
using UnityEngine;

namespace September.InGame.Rules.PlayerDamaged
{
    [Serializable]
    public class JewelPlayerHitStrategy : IPlayerHitStrategy
    {
        [SerializeField] private JewelryDropHitProcessor _defaultDamageProcessor = new(DropType.Drop);

        public void OnHitTaken(ref HitData hitData)
        {
            if (hitData.HitActionType.IsDamage())
            {
                _defaultDamageProcessor.DropType = hitData.HitActionType == HitActionType.Damage ? DropType.Drop : DropType.Pickup;
                _defaultDamageProcessor.OnHitTaken(hitData);
            }
            else if (hitData.HitActionType == HitActionType.Custom)
            {
                hitData.CustomHitProcessor.OnHitTaken(hitData);
            }
        }
    }
}
