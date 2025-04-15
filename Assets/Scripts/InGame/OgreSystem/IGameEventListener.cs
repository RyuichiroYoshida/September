
namespace September.OgreSystem
{
    public interface IGameEventListener
    {
        /// <summary>
        /// 鬼が自分になった時に通知をする
        /// </summary>
        void OnBecomeOgre();

        /// <summary>
        /// 気絶した時に通知する
        /// </summary>
        void OnParalyzed();
        
        /// <summary>
        /// 通常状態に戻った時に通知する
        /// </summary>
        void OnBecomeNormal();
    }
}

