using UnityEngine;

public interface IGameEventListener
{
    /// <summary>
    /// 鬼が自分になった時に通知をする
    /// </summary>
    void BecomeOger();
}
