using UnityEngine;

namespace September.InGame.UI
{
    [CreateAssetMenu(fileName = "September/UI/GameTimerData", menuName = "Game Timer")]
    public class GameTimerData : ScriptableObject
    {
        public int Time;
        public float Duration;
        public float AfterReadyDelay;
        [Header("ゲーム時間(秒数)")]
        public float GameTime;
    }
}