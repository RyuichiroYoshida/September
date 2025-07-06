using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Health;
using InGame.Player;
using September.InGame.Common;
using September.InGame.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace September.Common
{
    public class PreparationState : ImtStateMachine<InGameManager>.State
    {
        protected internal override void OnEnter()
        {
            HideCursor();
            SetUpUI();
            if (Context.Runner.IsServer)
            {
                ChooseOgre();
                Initialize().Forget();
            }
        }

        private async UniTask Initialize()
        {
            await Runner.LoadScene("Field", LoadSceneMode.Additive);
            var container = CharacterDataContainer.Instance;
            foreach (var pair in PlayerDatabase.Instance.PlayerDataDic)
            {
                var player = await Context.Runner.SpawnAsync(
                    container.GetCharacterData(pair.Value.CharacterType).Prefab,
                    inputAuthority: pair.Key);
                Context.Runner.SetPlayerObject(pair.Key, player);
                if (!Context.PlayerDataDic.ContainsKey(pair.Key))
                {
                    Context.AddPlayerObject(pair.Key, player);
                }
                var playerHealth = player.GetComponent<PlayerHealth>();
                playerHealth.OnDeath += OnPlayerKilled;
                //PlayerHealthのOnDeathに登録
            }
            Context.Register(StaticServiceLocator.Instance);
            StartTimer().Forget();
        }
        private void SetUpUI()
        {
            UIController.I.SetUpStartUI();
            UIController.I.StartTimer();
        }
        /// <summary>
        /// 鬼を抽選するメソッド
        /// </summary>
        private void ChooseOgre()
        {
            var dic = PlayerDatabase.Instance.PlayerDataDic;
            if (dic.Count <= 0 || !Context.Runner.IsServer) return;
            
            var index = Random.Range(0, dic.Count);
            var ogreKey = dic.ToArray()[index].Key;
            var data = dic.Get(ogreKey);
            data.IsOgre = true;
            PlayerDatabase.Instance.PlayerDataDic.Set(ogreKey, data);
            RPC_SetOgreLamp(ogreKey);
        }
        /// <summary>
        /// 各Playerの気絶時に呼ばれるメソッド
        /// </summary>
        private void OnPlayerKilled(HitData data)
        {
            if (!Context.Runner.IsServer) return; // サーバー側でのみ実行可能
            
            var killerData = PlayerDatabase.Instance.PlayerDataDic.Get(data.ExecutorRef); //DataBaseから該当Playerの情報取得
            killerData.IsOgre = false;
            PlayerDatabase.Instance.PlayerDataDic.Set(data.ExecutorRef, killerData); //DataBase更新 

            var killedData = PlayerDatabase.Instance.PlayerDataDic.Get(data.TargetRef);
            killedData.IsOgre = true;
            PlayerDatabase.Instance.PlayerDataDic.Set(data.TargetRef, killedData);
            killerData.Score += Context.AddScore;
            Debug.Log($"鬼が{data.ExecutorRef}から{data.TargetRef}に変更された");
            RPC_SetOgreUI(data.ExecutorRef,data.TargetRef);
        }
        private async UniTask StartTimer()
        {
            for (int i = Context.TimerData.PreStartTime; i >= 1; i--)
            {
                //ReadyTime表示
                await UniTask.Delay(TimeSpan.FromSeconds(Context.TimerData.Duration), cancellationToken: Context.Cts.Token);
            }
            await UniTask.Delay(TimeSpan.FromSeconds(Context.TimerData.AfterReadyDelay), cancellationToken: Context.Cts.Token);
            //  ステート終了
            Context.Rpc_SendEvent((int)StateEventId.Finish);
        }
        private void HideCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        // 鬼変更時のUI更新通知
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SetOgreUI(PlayerRef executor, PlayerRef targetRef)
        {
            UIController.I.ShowNoticeKillLog($"鬼が{executor}から{targetRef}に変更された");
            
            if (executor == Context.Runner.LocalPlayer)
                UIController.I.ShowOgreLamp(false);
            else if(targetRef == Context.Runner.LocalPlayer)
                UIController.I.ShowOgreLamp(true);
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SetOgreLamp(PlayerRef ogreRef)
        {
            if (ogreRef == Context.Runner.LocalPlayer)
            {
                UIController.I.ShowOgreLamp(true);
            }
        }
    }
}