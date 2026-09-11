using Fusion;
using InGame.Exhibit;
using September.Common;
using September.InGame.Common;
using UnityEngine;

namespace September.InGame.UI.Presenters.Tag
{
    public class TagRulePlayerKilledUIPresenter : NetworkBehaviour, IPlayerKilledPresenter
    {
        private void Start()
        {
            var inGameManager = StaticServiceLocator.Instance.Get<InGameManager>();
            inGameManager.PlayerKilled += OnPlayerKilled;
        }

        public void OnPlayerKilled(PlayerRef killer, PlayerRef victim)
        {
            Debug.Log($"[InGameLog][KilledEvent][aaa] killer={killer}, victim={victim}", this);
            RPC_ShowKillLog(killer, victim);

            SessionPlayerData killerData = PlayerDatabase.Instance.PlayerDataDic.Get(killer);
            if (killerData.IsOgre && killer != victim)
            {
                OnChangeOgre(killer, victim);
            }
        }

        private void OnChangeOgre(PlayerRef killer, PlayerRef victim)
        {
            RPC_SetOgreUI(killer, victim);
            RPC_ShowStatusUpUI(killer, false);
            RPC_ShowStatusUpUI(victim, true);
        }

        // 鬼変更時の鬼ランプの表示・非表示処理
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SetOgreUI(PlayerRef executor, PlayerRef targetRef)
        {
            // 鬼じゃなかったら消す
            if (executor == Runner.LocalPlayer)
            {
                UIController.I.ShowOgreLamp(false);
            }

            // 鬼だったら付ける
            else if (targetRef == Runner.LocalPlayer)
            {
                UIController.I.ShowOgreLamp(true);
                UIController.I.ChangeTagNotice(2);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ShowStatusUpUI(PlayerRef playerRef, bool showStatusUpUI)
        {
            if (Runner.LocalPlayer == playerRef)
            {
                if (showStatusUpUI)
                {
                    UIController.I.ShowStatusUpUI(-1, StatusUpType.Ogre);
                }
                else
                {
                    UIController.I.ShowStatusUpUI(-1, StatusUpType.None);
                }
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ShowKillLog(PlayerRef killer, PlayerRef killed)
        {
            Debug.Log($"[InGameLog][KillRPC][aaa] killer={killer}, killed={killed}", this);
            if (PlayerDatabase.Instance.PlayerDataDic.TryGet(killer, out SessionPlayerData killerData) &&
                PlayerDatabase.Instance.PlayerDataDic.TryGet(killed, out SessionPlayerData killedData))
            {
                string killerName = killerData.DisplayNickName;
                string killedName = killedData.DisplayNickName;
                UIController.I.ShowLog($"{killerName} が {killedName} を倒した");
            }
        }
    }
}
