using System.Linq;
using Fusion;
using September.InGame;
using UnityEngine;

namespace September.OgreSystem
{
    public class PlayerDatabase : SimulationBehaviour
    {
        /// <summary>
        /// 鬼を抽選するメソッド
        /// </summary>
        public void ChooseOgre()
        {
            if (Runner.ActivePlayers.Count() <= 0) return;
            
            var index = Random.Range(0, Runner.ActivePlayers.Count());
            var ogreRef = Runner.ActivePlayers.ToArray()[index];
            foreach (var player in Runner.ActivePlayers)
            {
                SetOgre(player, ogreRef == player);
            }
        }

        void SetOgre(PlayerRef playerRef, bool isOgre)
        {
            if (Runner.GetPlayerObject(playerRef).TryGetComponent(out PlayerController playerStatus))
            {
                playerStatus.IsOgre = isOgre;
            }
            else
            {
                Debug.Log("PlayerStatus is not found");
            }
        }
    }
}

