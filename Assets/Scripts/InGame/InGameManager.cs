using System;
using Fusion;
using InGame.Player;
using September.Common;
using UnityEngine;

public class InGameManager : NetworkBehaviour
{
    [Networked] private TickTimer _tickTimer {get; set; }
    
    [Header("ゲーム時間（秒）")]
    [SerializeField]
    private int _gameTime = 1200;

    public Action OnOgreChanged;

    public override void Spawned()
    {
        StartTimer();
        HideCursor();
    }
    
    /// <summary>
    /// 各Playerの気絶時に呼ばれるメソッド
    /// </summary>
    public void RPC_OnPlayerKilled(PlayerRef killer, PlayerRef killed)
    {
        if (!Runner.IsServer) return;　 // サーバー側でのみ実行可能
        var killerData = PlayerDatabase.Instance.PlayerDataDic.Get(killer);　　//DataBaseから該当Playerの情報取得
        killerData.IsOgre = false;
        PlayerDatabase.Instance.PlayerDataDic.Set(killer, killerData);　　　　　//DataBase更新 
            
        var killedData = PlayerDatabase.Instance.PlayerDataDic.Get(killed);
        killedData.IsOgre = false;
        PlayerDatabase.Instance.PlayerDataDic.Set(killed, killedData);
        OnPlayerDeath?.Invoke();
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
