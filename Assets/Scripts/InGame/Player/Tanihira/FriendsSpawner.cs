using Ingame.Tanihira;
using UnityEngine;

namespace InGame.Tanihira
{
    public class FriendsSpawner : MonoBehaviour
    {
        [Header("初期友達の設定")]
        [SerializeField] private Transform[] _spawnPosition;
        [SerializeField] private FriendType[] _friendsTypes;
        


        public void SpawnFriend(FriendType friendType,Transform spawnPosition)
        {
            
        }
    }

}