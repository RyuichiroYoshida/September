using System;
using NaughtyAttributes;
using UnityEngine;

namespace September.InGame.Jewelry.Drop.Strategies.Chances
{
    [Serializable]
    public class DamageDropChance : IJewelryDropChance
    {
        [Header("ダメージ量に応じたドロップ確率変動")]
        [SerializeField, CurveRange(0f, 0f, 200f, 1f, EColor.Red)]
        private AnimationCurve _dropChanceCurve = AnimationCurve.Linear(0, 0, 200, 1);

        public float GetChance(in JewelryDropContext context)
        {
            float chance = _dropChanceCurve.Evaluate(context.HitData.Amount);
            return chance;
        }
    }
}
