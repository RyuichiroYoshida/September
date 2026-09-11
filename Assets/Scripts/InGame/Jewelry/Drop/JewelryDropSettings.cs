using System;
using InGame.Health;
using InGame.Jewelry.Common;
using September.InGame.Jewelry.Drop.Strategies;
using September.InGame.Jewelry.Drop.Strategies.Amounts;
using September.InGame.Jewelry.Drop.Strategies.Chances;
using UnityEngine;
using Random = UnityEngine.Random;

namespace September.InGame.Jewelry.Drop
{
    [Serializable]
    public class JewelryDropSettings
    {
        [SerializeField] private JewelryType _jewelryType;
        public JewelryType JewelryType => _jewelryType;

        [Header("ドロップ確率（各要素の総和を確率として扱う）")]
        [SubclassSelector, SerializeReference] private IJewelryDropChance[] _dropChances;

        [Header("ドロップ量")]
        [SubclassSelector, SerializeReference] private IJewelryDropAmount[] _dropAmounts;

        public int GetDropAmount(HitData hitData, IJewelryContainer jewelryContainer, bool outputLog = false)
        {
            JewelryDropContext context = new(hitData, _jewelryType, jewelryContainer);

            // ドロップ確率の計算
            {
                if (outputLog) JewelryDropLogger.StartSection("CHANCE");

                // 各要素が返す確率の総和を計算
                float dropChanceSum = 0;
                foreach (IJewelryDropChance chance in _dropChances)
                {
                    float currChance = chance.GetChance(context);
                    dropChanceSum += currChance;

                    if (outputLog) JewelryDropLogger.AppendLog(chance, currChance);
                }

                if (outputLog) JewelryDropLogger.AppendLog($"- dropChanceSum: {dropChanceSum}");

                // 確率が通らなかったら終了
                if (dropChanceSum < Random.value)
                {
                    if (outputLog) JewelryDropLogger.JoinSettingsLog(_jewelryType, 0);
                    return 0;
                }
            }

            // ドロップ量の計算
            {
                if (outputLog) JewelryDropLogger.StartSection("AMOUNT");

                // 各要素が返す個数の総和を計算
                foreach (IJewelryDropAmount amount in _dropAmounts)
                {
                    int currAmount = amount.GetDropAmount(ref context);

                    if (outputLog) JewelryDropLogger.AppendLog(amount, currAmount);
                }

                // 所持宝石数より多くドロップしない
                int jewelryCount = jewelryContainer.GetJewelryCount(_jewelryType);
                context.Amount = Mathf.Min(Mathf.RoundToInt(context.Amount), jewelryCount);

                if (outputLog) JewelryDropLogger.AppendLog($"- dropAmountSum: {context.Amount} (jewelryCount: {jewelryCount})");
            }

            int dropAmount = context.Amount;

            if (outputLog) JewelryDropLogger.JoinSettingsLog(_jewelryType, dropAmount);

            return dropAmount;
        }
    }
}
