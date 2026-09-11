using Fusion;

namespace September.InGame.Mountable
{
    interface IMountable
    {
        /// <summary>
        /// マウント開始
        /// </summary>
        public void GetOn(PlayerRef player);
        
        /// <summary>
        /// マウント終了
        /// </summary>
        public void GetOff(PlayerRef player);
    }
}