using Fusion;
using Ingame.Tanihira;
using UnityEngine;
using UnityEngine.AI;

namespace InGame.Tanihira
{
    public class FriendsSpawner : NetworkBehaviour, InGame.Player.IMimicCleanup, InGame.Player.IMimicInitialize
    {
        [Header("初期友達の設定")]
        [SerializeField] private FriendType[] _friendsTypes;
        [SerializeField] private FriendDatabase _friendDatabase;
        [SerializeField] private FormationManager _formationManager;
        [SerializeField] private NetworkObject _ownerPlayer;
        [SerializeField] private Transform _firstSpawnPoint;
        [SerializeField] private float _navmeshSerchRadius = 5.0f;
        private NetworkRunner _networkRunner;
        private readonly System.Collections.Generic.List<NetworkObject> _spawnedFriends = new();
        private bool _isInitialized;

        public void Start()
        {
            Initialize();
        }

        //初期化処理
        private void Initialize()
        {
            if (_isInitialized)
                return;

            _networkRunner = Runner != null ? Runner : FindFirstObjectByType<NetworkRunner>();
            if (_networkRunner == null)
            {
                Debug.LogError("NetworkRunnerがありません");
                return;
            }

            _isInitialized = true;

            if (HasStateAuthority)
            {
                //初期で登録されたフレンドを生成
                for (int i = 0; i < _friendsTypes.Length; i++)
                {
                    SpawnFriend(_friendsTypes[i], _firstSpawnPoint);
                }
            }
            
            _formationManager.RegisterFriendFormation();
        }

        // インターフェース実装
        public void InitializeAfterMimicSpawn()
        {
            Initialize();
        }

        /// <summary>
        /// フレンドを生成
        /// </summary>
        public FriendBase SpawnFriend(FriendType friendType,Transform spawnPosition)
        {
            var prefab = _friendDatabase.GetFriendObject(friendType);
            
            if (prefab)
            {
                Vector3 fixedPos = spawnPosition.position; 
                NavMeshAgent navMeshAgent = prefab.GetComponent<NavMeshAgent>();
                NavMeshHit hit;
                if (NavMesh.SamplePosition(spawnPosition.position, out hit, _navmeshSerchRadius, NavMesh.AllAreas))
                {
                    // NavMesh上にワープ
                    fixedPos = hit.position + Vector3.up * navMeshAgent.baseOffset;
                }
                else
                {
                    Debug.LogWarning($"{friendType} はNavMesh上にスポーンできませんでした");
                }
                
                NetworkObject spawnedObject = _networkRunner.Spawn(prefab, fixedPos, Quaternion.identity, null);
                FriendBase friend = spawnedObject.GetComponent<FriendBase>();

                if (friend)
                {
                    _spawnedFriends.Add(spawnedObject);
                    // フレンドにFormationManagerとownerPlayerを設定
                    friend.SetFormationManager(_formationManager);
                    friend.SetOwnerPlayer(_ownerPlayer);
                    if (_formationManager)
                    {
                        //友達登録
                        var pos = _formationManager.Register(friend);
                        friend.SetDestination(pos);
                    }
                }

                return friend;
            }
            
            return null;
        }

        // インターフェース実装
        public void CleanupBeforeMimicDespawn()
        {
            if (!HasStateAuthority || _networkRunner == null)
                return;

            foreach (var friendObject in _spawnedFriends)
            {
                if (friendObject)
                    _networkRunner.Despawn(friendObject);
            }

            _spawnedFriends.Clear();
        }
    }
}
