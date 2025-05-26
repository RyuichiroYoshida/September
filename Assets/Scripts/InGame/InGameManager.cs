using Fusion;
using InGame.Player;
using UnityEngine;

public class InGameManager : NetworkBehaviour
{
    [Networked] private TickTimer _tickTimer {get; set; }
    private int _gameTime = 1200;
    
    public override void Spawned()
    {
        StartTimer();
        HideCursor();
    }
    
    /// <summary>
    /// 各Playerの気絶時に呼ばれるメソッド
    /// </summary>
    public void RPC_OnPlayerKilled(PlayerManager killer, PlayerManager killed)
    {
        //　IsOgreの切り替え、キルログを出すためのイベントInvoke、PlayerDataBase更新等
    }

    void HideCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void StartTimer()
    {
        _tickTimer = TickTimer.CreateFromSeconds(Runner,_gameTime);
    }

    private void GameEnded()
    {
        
    }

    public override void FixedUpdateNetwork()
    {
        if (_tickTimer.Expired(Runner))
        {
            GameEnded();
            _tickTimer = TickTimer.None;
        }
    }
}
