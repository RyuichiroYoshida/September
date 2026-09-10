using Fusion;
using InGame.Health;
using September.Common;

namespace September.InGame.Rules
{
    public class PlayerKillUseCase
    {
        private readonly IPlayerKilledStrategy _strategy;

        public PlayerKillUseCase(IPlayerKilledStrategy strategy)
        {
            _strategy = strategy;
        }

        public void Execute(HitData hitData)
        {
            var killer = hitData.ExecutorRef;
            var victim = hitData.TargetRef;

            _strategy.ProcessKillEvent(hitData);

            UpdateStunData(killer, victim);
        }

        private void UpdateStunData(PlayerRef killer, PlayerRef victim)
        {
            // セルフキルの場合は変更しない
            if (killer == victim) return;

            SessionPlayerData killerData = PlayerDatabase.Instance.PlayerDataDic.Get(killer);

            if (!killerData.StunData.TryGet(victim, out int count))
            {
                killerData.StunData.Set(victim, count + 1);
            }
            else
            {
                killerData.StunData.Set(victim, 1);
            }

            PlayerDatabase.Instance.PlayerDataDic.Set(killer, killerData);
            // スコアの更新処理（スタン回数に応じてスコアが変化するため）
            PlayerDatabase.Instance.Server_RecalculateScore(killer);
        }
    }
}
