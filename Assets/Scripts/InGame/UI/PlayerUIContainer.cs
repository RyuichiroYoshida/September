using System;
using Fusion;
using September.Common;
using September.InGame;
using September.OgreSystem;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerUIContainer : NetworkBehaviour
{
    PlayerAvatar _playerAvatar;
    [SerializeField] private GameObject _hpBar;
    GameObject _ingameUI_Canvas;
    PlayerHpBarManager _playerHpBar;
    NetworkDictionary<PlayerAvatar, PlayerHpBarManager> _playerHpBarDict;
    public override void Spawned()
    {
        GameLauncher.Instance.OnPlayerSpawned += UI_OnPlayerSpawned;
        
    }

    public override void FixedUpdateNetwork()
    {
         
    }


    public void UI_OnPlayerSpawned(NetworkObject networkObject, PlayerRef player)
    {
        _playerAvatar = networkObject.GetComponent<PlayerAvatar>();
        _ingameUI_Canvas = gameObject.transform.GetChild(0).gameObject;
        var ui = Instantiate(_hpBar, _ingameUI_Canvas.transform);
        _playerHpBar = ui.GetComponent<PlayerHpBarManager>();
        _playerHpBarDict.Add(_playerAvatar, _playerHpBar);
    }
   
}
