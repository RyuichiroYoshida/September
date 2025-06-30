using Fusion;
using September.InGame.Common;

namespace September.Common
{
    public class PlayingState : ImtStateMachine<InGameManager>.State
    {
        [Networked] TickTimer TickTimer { get; set; }
        protected internal override void OnEnter()
        {
            //  制限時間カウント開始
            TickTimer = TickTimer.CreateFromSeconds(Context.Runner, Context.TimerData.GameTime);
        }

        protected internal override void OnNetworkFixedUpdate()
        {
            if (TickTimer.Expired(Context.Runner))
            {
                TickTimer = TickTimer.None;
                Context.Rpc_SendEvent((int)StateEventId.Finish);
            }
        }
    }
}