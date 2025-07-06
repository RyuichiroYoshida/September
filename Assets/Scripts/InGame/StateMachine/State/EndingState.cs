using System;
using System.Collections.Generic;
using System.Linq;
using CRISound;
using Cysharp.Threading.Tasks;
using Fusion;
using September.InGame.Common;
using UnityEngine;

namespace September.Common
{
    public class EndingState : ImtStateMachine<InGameManager>.State
    {
        protected internal override void OnEnter()
        {
            GameEnded().Forget();
        }

        private async UniTaskVoid GameEnded()
        {
            GetScore();
            await UniTask.Delay(TimeSpan.FromSeconds(Context.TimerData.EndGameDelay));
            Context.Cts.Cancel();
            ShowCursor();
            if(!string.IsNullOrEmpty(Context.CurrentBGM)) CRIAudio.StopBGM("BGM", Context.CurrentBGM);
            await NetworkManager.Instance.QuitInGame();
        }
        private void ShowCursor()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        private void GetScore()
        {
            List<(string playerName,int score,bool isOgre)> data = new();
            foreach (var pair in PlayerDatabase.Instance.PlayerDataDic)
            {
                data.Add((pair.Value.DisplayNickName, pair.Value.Score,pair.Value.IsOgre));
            }
            var ordered = data.OrderBy(x => x.isOgre ? 1 : 0)
                .ThenByDescending(x => x.score)
                .ToList();
            var names = ordered.Select(x => x.playerName).ToArray();
            var scores = ordered.Select(x => x.score).ToArray();
            RPC_SetRankingData(names, scores);
        }
        [Rpc]
        private void RPC_SetRankingData(string[] names, int[] scores)
        {
            RankingDataHolder.Instance.SetData(names, scores);
        }
    }
}