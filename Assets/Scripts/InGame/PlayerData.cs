using Fusion;
using September.Common;
using UnityEngine;
using UnityEngine.UI;

namespace September.InGame
{
    public class PlayerData : NetworkBehaviour
    {
        [SerializeField] Text _playerName;
        [Networked, OnChangedRender(nameof(OnNickNameChanged))] NetworkString<_16> NickName { get; set; }

        public override void Spawned()
        {
            if (HasInputAuthority) 
            {
                Rpc_SetNickname(PlayerNetworkSettings.NickName);
            }
            else
            {
                _playerName.text = NickName.Value;
            }
            Runner.SetPlayerObject(Runner.LocalPlayer, Object);
        }

        public void OnNickNameChanged()
        {
            _playerName.text = NickName.Value;
        }

        // Only required in host/server mode.
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void Rpc_SetNickname(string nickname) 
        {
            NickName = nickname;
        }
    }
}