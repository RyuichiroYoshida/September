using Fusion;
using InGame.Health;
using September.Common;

namespace September.InGame.Rules
{
    /// <summary>
    /// 鬼ごっこルールにおけるプレイヤーキル時の処理
    /// </summary>
    public class TagPlayerKilledStrategy : IPlayerKilledStrategy
    {
        public void ProcessKillEvent(HitData hitData)
        {
            PlayerRef killer = hitData.ExecutorRef;
            PlayerRef victim = hitData.TargetRef;

            SessionPlayerData killerData = PlayerDatabase.Instance.PlayerDataDic.Get(killer);
            SessionPlayerData victimData = PlayerDatabase.Instance.PlayerDataDic.Get(victim);

            if (!killerData.IsOgre || killer == victim) return;

            killerData.IsOgre = false;
            PlayerDatabase.Instance.PlayerDataDic.Set(killer, killerData);
            victimData.IsOgre = true;
            PlayerDatabase.Instance.PlayerDataDic.Set(victim, victimData);
            PlayerDatabase.Instance.Server_AddOgreCount(victim);
        }
    }
}
