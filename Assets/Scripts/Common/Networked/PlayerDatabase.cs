using System;
using System.Linq;
using Fusion;
using UnityEngine;
using Random = UnityEngine.Random;

namespace September.Common
{
    public class PlayerDatabase : NetworkBehaviour
    {
        [Networked, OnChangedRender(nameof(OnChangedPlayerData)), Capacity(4), HideInInspector]
        public NetworkDictionary<PlayerRef, SessionPlayerData> PlayerDataDic => default;
        public Action<NetworkDictionary<PlayerRef, SessionPlayerData>> ChangedDataAction;
        public static PlayerDatabase Instance;
        public override void Spawned()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Runner.Despawn(Object);
            }
        }
        public void AddPlayerData(PlayerRef playerRef)
        {
            if (playerRef != Runner.LocalPlayer) return;
            var localNickName = NickNameProvider.GetNickName();
            var nickNameOrder = PlayerDataDic.Count(kv => kv.Value.PureNickName == localNickName);
            Rpc_SetPlayerData(playerRef, new SessionPlayerData(localNickName, nickNameOrder));
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
        public void Rpc_SetPlayerData(PlayerRef playerRef, SessionPlayerData data)
        {
            PlayerDataDic.Set(playerRef, data);
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
        public void Rpc_SetCharacter(PlayerRef playerRef, CharacterType characterType)
        {
            if (!PlayerDataDic.TryGet(playerRef, out var playerData)) return;
            playerData.CharacterType = characterType;
            PlayerDataDic.Set(playerRef, playerData);
        }

        void OnChangedPlayerData()
        {
            ChangedDataAction?.Invoke(PlayerDataDic);
        }
        
        /// <summary>
        /// 鬼を抽選するメソッド
        /// </summary>
        public void ChooseOgre()
        {
            if (PlayerDataDic.Count <= 0 || !Runner.IsServer) return;
            
            var index = Random.Range(0, PlayerDataDic.Count);
            var ogreKey = PlayerDataDic.ToArray()[index].Key;
            var data = PlayerDataDic.Get(ogreKey);
            data.IsOgre = true;
            PlayerDataDic.Set(ogreKey, data);
        }
    }
}

