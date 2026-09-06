using System;
using UnityEngine;

namespace September.InGame.Jewelry.Drop.Strategies.Amounts
{
    [Serializable]
    public class DamageDropAmount : IJewelryDropAmount
    {
        [Header("ダメージ量に応じたドロップ量の変動")]
        [SerializeField]
        private AnimationCurve _dropRate = AnimationCurve.Linear(0, 0, 200, 1);

        public int GetDropAmount(ref JewelryDropContext context)
        {
            int dropAmount = Mathf.RoundToInt(_dropRate.Evaluate(context.HitData.Amount));
            context.Amount += dropAmount;
            return dropAmount;
        }
    }
}
