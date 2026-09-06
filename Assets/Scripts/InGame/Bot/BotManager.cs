using InGame.Player;

namespace InGame.Bot
{
    public class BotManager : PlayerManager
    {
        /// <summary> Bot はローカルの視点入力でカメラを回さない </summary>
        protected override bool UsesLocalLookInput => false;
    }
}
