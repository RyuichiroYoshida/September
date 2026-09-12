
namespace September.Common
{
    /// <summary>
    /// チュートリアル専用のステート。
    /// IsGameEnded() 系の判定を通すためだけの最小実装。
    /// </summary>
    public class TutorialState : ImtStateMachine<InGame.Common.InGameManager>.State
    {
        protected internal override void OnEnter()
        {
            // Tutorialでは特別な初期化は不要
        }
    }
}