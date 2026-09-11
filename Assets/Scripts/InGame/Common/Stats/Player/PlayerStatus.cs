using September.InGame.Common.Stats;
using UnityEngine;

namespace InGame.Player
{
    /// <summary>
    /// プレイヤーステータスを管理するコンポーネント
    /// </summary>
    public class PlayerStatus : StatsManager
    {
        [SerializeField] private PlayerParameter _params;

        protected override StatsContainer GetInitialStats()
        {
            var stats = _params.GetStats();
            // 既存のPlayerParameterにも回避スタミナを補完する。
            stats.Stats.Set(StatType.EvasionStamina, new Stat(StatType.EvasionStamina, 3f, 0f, 3f));
            return stats;
        }

        // プレイヤー共通ステータスへのアクセスを便利にする用（なくても良い。他のコンポーネントからも以下のように CurrentStats.GetStat でとってきても良い）
        public int MaxHealth => (int)CurrentStats.GetStat(StatType.Health).MaxValue;
        public int CurrentHealth => (int)CurrentStats.GetStat(StatType.Health).Value;
        public float MaxStamina => CurrentStats.GetStat(StatType.Stamina).MaxValue;
        public float CurrentStamina => CurrentStats.GetStat(StatType.Stamina).Value;
        public int MaxEvasionStamina => (int)CurrentStats.GetStat(StatType.EvasionStamina).MaxValue;
        public int CurrentEvasionStamina => (int)CurrentStats.GetStat(StatType.EvasionStamina).Value;
        public float StaminaRegen => CurrentStats.GetStat(StatType.StaminaRegen).Value;
        public float StaminaConsumption => CurrentStats.GetStat(StatType.StaminaConsumption).Value;
        public float Speed => CurrentStats.GetStat(StatType.Speed).Value;
        public float AttackDamage => CurrentStats.GetStat(StatType.AttackDamage).Value;
        public float InteractDurationMultiply => CurrentStats.GetStat(StatType.InteractDurationMultiply).Value;
        public float StunDurationMultiply => CurrentStats.GetStat(StatType.StunDurationMultiply).Value;
    }
}
