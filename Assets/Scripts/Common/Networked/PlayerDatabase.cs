using System;
using System.Linq;
using Fusion;
using Random = UnityEngine.Random;

namespace September.Common
{
    public class PlayerDatabase : NetworkBehaviour
    {
        [Networked, OnChangedRender(nameof(OnChangedPlayerData)), Capacity(10)]
        public NetworkDictionary<PlayerRef, SessionPlayerData> PlayerDataDic => default;
        public Action<PlayerRef, SessionPlayerData> ChangedDataAction;
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
            foreach (var kv in PlayerDataDic)
            {
                ChangedDataAction?.Invoke(kv.Key, kv.Value);
            }
        }

        public bool CanAttack(PlayerRef attackerPlayerRef, PlayerRef victimPlayerRef)
        {
            return PlayerDataDic.Get(attackerPlayerRef).IsOgre && !PlayerDataDic.Get(victimPlayerRef).IsOgre;
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

