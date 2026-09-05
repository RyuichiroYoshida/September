using System;
using NaughtyAttributes;
using September.Common;
using September.InGame.Common;
using UnityEngine;

namespace September.InGame.Jewelry.Drop.Strategies.Amounts
{
    [Serializable]
    public class RankingDropAmount : IJewelryDropAmount
    {
        [Header("順位に応じたドロップ量変動処理 (高順位が1, 低順位が0)")]
        [SerializeField, CurveRange(0, 0, 1, 5)] private AnimationCurve _rankDropCurve;

        private Ranking _ranking;

        public int GetDropAmount(ref JewelryDropContext context)
        {
            _ranking ??= StaticServiceLocator.Instance.Get<Ranking>();

            int playersCount = _ranking.GetCount();
            int victimRank = _ranking.GetRank(context.HitData.TargetRef);

            // 0~1 の範囲に正規化。
            // 数値が大きい方が順位が高いようにする
            float rankRatio = playersCount > 1
                    ? 1f - (victimRank - 1f) / (playersCount - 1f)
                    : 1f;

            int dropAmount = Mathf.RoundToInt(_rankDropCurve.Evaluate(rankRatio));

            context.Amount += dropAmount;
            return dropAmount;
        }
    }
}
