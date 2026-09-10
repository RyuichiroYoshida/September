using System;
using UnityEngine;

namespace September.InGame.Jewelry.Drop.Strategies.Amounts
{
    [Serializable]
    public class RatioDropAmount : IJewelryDropAmount
    {
        [SerializeField, Tooltip("死亡時の所持宝石数からドロップする数")] private int _minDropAmount = 1;
        [SerializeField, Tooltip("死亡時の所持宝石数からドロップする割合")] private float _minDropRatio = 0f;
        [SerializeField, Tooltip("minDropXXX の計算後に残った所持宝石数からドロップする割合")] private float _additionalDropRatio = 0.5f;

        public int GetDropAmount(ref JewelryDropContext context)
        {
            int jewelryQuantity = context.VictimJewelryContainer.GetJewelryCount(context.JewelryType);
            int minDrop = _minDropAmount + Mathf.FloorToInt(jewelryQuantity * _minDropRatio);
            int sumDrop = minDrop + Mathf.FloorToInt((jewelryQuantity - minDrop) * _additionalDropRatio);
            int dropAmount = Mathf.Min(sumDrop, jewelryQuantity);

            context.Amount += dropAmount;
            return dropAmount;
        }
    }
}
