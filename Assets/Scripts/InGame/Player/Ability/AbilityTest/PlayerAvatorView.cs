using System;
using Cinemachine;
using Fusion;
using TMPro;
using UnityEngine;

public class PlayerAvatorView : MonoBehaviour
{
    [SerializeField] CinemachineFreeLook freeLookCamera;
    [SerializeField] TMP_Text playerNameText;
    public void MakeCameraTarget()
    {
        freeLookCamera.Priority = 100;
    }
    
    public void SetNickName(NetworkRunner runner, string name)
    {
        if (runner.IsServer)
        {
            playerNameText.text = name;
        }
        else
        {
            RPC_SetNickName(name);
        }
    }
    
    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_SetNickName(string name)
    {
        playerNameText.text = name;
    }

    private void LateUpdate()
    {
        playerNameText.transform.rotation = Camera.main.transform.rotation;
    }
}
