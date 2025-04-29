using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using September.Common;
using September.InGame;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerUIContainer : MonoBehaviour,INetworkRunnerCallbacks
{
    public static PlayerUIContainer Instance;
    PlayerAvatar _playerAvatar;
    [SerializeField] private GameObject _hpBar;
    GameObject _ingameUI_Canvas;
    PlayerHpBarManager _playerHpBar;
    List<(PlayerAvatar, PlayerHpBarManager)> _playerHpBarList = new List<(PlayerAvatar, PlayerHpBarManager)>();
    NetworkRunner _networkRunner;
    private void Start()
    {
         _networkRunner = NetworkRunner.GetRunnerForScene(SceneManager.GetActiveScene());
         AddCallBacks(_networkRunner);

    }
    private void AddCallBacks(NetworkRunner runner)
    {
        if (runner != null)
        {
            Debug.Log("callback");
            runner.AddCallbacks(this);
        }
    }
    
    
    public async void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        await UniTask.Delay(50);
        var obj =  runner.GetPlayerObject(player); 
        if (_networkRunner.LocalPlayer == player)
        {
            Debug.Log($"{GetInstanceID()}");
            UI_OnPlayerSpawned(obj.GetComponent<PlayerAvatar>(),player);
        }
       
    }
    
    

    public void UI_OnPlayerSpawned(PlayerAvatar avatar, PlayerRef player)
    {
        _ingameUI_Canvas = GameObject.Find("IngameCanvas");
        var ui = Instantiate(_hpBar, _ingameUI_Canvas.transform);
        _playerHpBar = ui.GetComponent<PlayerHpBarManager>();
        _playerHpBarList.Add((avatar, _playerHpBar));
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        
    }

   

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
       
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
       
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
                                
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
       
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
     
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
      
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
       
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
       
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
       
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
      
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
      
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
      
    }
}
