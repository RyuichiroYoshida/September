using Fusion;
using Ingame.Tanihira;
using UnityEngine;

namespace InGame.Tanihira
{
    public class FriendsSpawner : NetworkBehaviour
    {
        [Header("初期友達の設定")]
        [SerializeField] private Transform[] _spawnPosition;
        [SerializeField] private FriendType[] _friendsTypes;
        [SerializeField] private FriendDatabase _friendDatabase;

        public override void Spawned()
        {
            Initialize();
        }

        //初期化処理
        private void Initialize()
        {
            //初期で登録されたフレンドを生成
            for (int i = 0; i < _spawnPosition.Length; i++)
            {
                SpawnFriend(_friendsTypes[i], _spawnPosition[i]);
            }
        }

        /// <summary>
        /// フレンドを生成
        /// </summary>
        public void SpawnFriend(FriendType friendType,Transform spawnPosition)
        {
            var prefab = _friendDatabase.GetFriendObject(friendType);
            if (prefab)
            {
                Instantiate(prefab, spawnPosition.position, spawnPosition.rotation);
            }
        }
    }

}