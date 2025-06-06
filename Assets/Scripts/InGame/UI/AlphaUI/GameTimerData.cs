using NaughtyAttributes;
using UnityEngine;

namespace September.InGame.UI
{
    [CreateAssetMenu(fileName = "September/UI/GameTimerData", menuName = "Game Timer")]
    public class GameTimerData : ScriptableObject
    {
        [Label("スタート前の逃げる時間")]public int PreStartTime;
        [Label("タイマーの減る時間")]public float Duration;
        [Label("ReadyとGoの間")]public float AfterReadyDelay;
        [Header("ゲーム時間(秒数)")]
        [Label("ゲーム時間")]public float GameTime;
    }
}