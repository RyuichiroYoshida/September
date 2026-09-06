using System;
using NaughtyAttributes;
using UnityEngine;

namespace September.InGame.Jewelry.Drop.Strategies.Chances
{
    [Serializable]
    public class ContainsDropChance : IJewelryDropChance
    {
        [Header("所持宝石スコアに応じたドロップ確率")]
        [SerializeField, CurveRange(0f, 0f, 20f, 1f, EColor.Red)]
        private AnimationCurve _containerDropRate = AnimationCurve.Linear(0, 0, 11, 1);

        public float GetChance(in JewelryDropContext context)
        {
            int jewelryCount = context.VictimJewelryContainer.CalculateJewelryScore();
            float chance = _containerDropRate.Evaluate(jewelryCount);
            return chance;
        }
    }
}
