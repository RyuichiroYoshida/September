using System;
using CRISound;
using Cysharp.Threading.Tasks;
using September.InGame.Common;
using September.InGame.UI;
using UnityEngine;

namespace September.Common
{
    public class EndingState : ImtStateMachine<InGameManager>.State
    {
        [SerializeField] private InGameStatusView _statusView;
        protected internal override void OnEnter()
        {
            Context.GameEnded?.Invoke();
            GameEnded().Forget();
        }

        private async UniTaskVoid GameEnded()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(Context.TimerData.EndGameDelay));
            Context.Cts.Cancel();
            
            //if(!string.IsNullOrEmpty(Context.CurrentBGM)) CRIAudio.StopBGM("BGM", Context.CurrentBGM);
            // ここにエンド処理
            PlayerDatabase.Instance.Server_PushResultToClients();
            UIController.I.ShowResultAnimation();

            // UI削除処理をクライアントでも確実に実行
            if (_statusView?.UIRoot?.gameObject != null)
            {
                Destroy(_statusView.UIRoot.gameObject);
            }

            GameInput.I.ToggleMoveInput(false);
            GameInput.I.ToggleLookInput(false);
            CRIAudio.StopSE();      // 鳴ってるSEを止める 2DPlayer用
            CRIAudio.Stop3DSEAll(); // 3DPlayer用
            CRIAudio.PlaySE("ALLCue", SoundCues.SE.UI_GameFinish.Name); // タイムアップ音
            BGMManager.ChangeBGM("Result").Forget(); // リザルトシーン用のBGMを再生
        }
    }
}
