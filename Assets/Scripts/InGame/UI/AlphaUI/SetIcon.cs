using System.Threading;
using Fusion;
using September.Common;
using September.InGame.UI;
using UnityEngine;
using UnityEngine.UI;

public class SetIcon : NetworkBehaviour
{
    [SerializeField] private IconData _iconData;
    
    private CancellationTokenSource _cts;
    
    private Image _image;
  
    public override void Spawned()
    {
        _cts = new CancellationTokenSource();
    }

    public void ShowIcon(PlayerRef playerRef)
    {
        if(!Runner.IsServer) return;
        var type = PlayerDatabase.Instance.PlayerDataDic[playerRef].CharacterType;
        RPC_SetIcon(playerRef, type);
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetIcon(PlayerRef playerRef, CharacterType characterType)
    {
        _image = UIController.I.UIRootRefs.IconImage;
        if (playerRef != Runner.LocalPlayer) return;
        _image.sprite = GetIcon(characterType);
    }
    
    

    private Sprite GetIcon(CharacterType characterType)
    {
        if (!_iconData.IconDictionary.ContainsKey(characterType))
        {
            Debug.LogError($"Character type {characterType} not found!");
            return null;
        }
        return _iconData.IconDictionary[characterType];
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        _cts.Cancel();
        _cts.Dispose();
    }
}

