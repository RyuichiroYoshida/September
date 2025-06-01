using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Common;
using September.Common;
using UnityEngine;

namespace September.InGame.Common
{
    /// <summary>
    /// 完全にホスト専用のSpawner。クライアントは一切呼び出さないこと。
    /// </summary>
    public class PhotonSpawner : MonoBehaviour, ISpawner, IRegisterableService
    {
        [SerializeField] private SpawnablePrefabDatabase _database;
        private readonly Dictionary<int, NetworkObject> _spawnedObjects = new();
        private int _nextId = 0;

        private NetworkRunner _runner;

        private void Awake()
        {
            _runner = FindFirstObjectByType<NetworkRunner>();
            if (_runner == null)
            {
                Debug.LogError("NetworkRunnerがありません");
            }
            StaticServiceLocator.Instance.Register<ISpawner>(this);
        }

        /// <summary>
        /// ホスト専用：オブジェクト生成
        /// </summary>
        public int Spawn(string prefabGuid, Vector3 position, Quaternion rotation, Transform setTransform = null, int playerRef = default, Action onSpawned = null)
        {
            if (_runner == null)
            {
                _runner = FindFirstObjectByType<NetworkRunner>();
                if (_runner == null)
                {
                    Debug.LogError("NetworkRunnerがありません");
                    return -1;
                }
            }

            if (!_runner.IsServer)
            {
                Debug.LogError("Spawn() はホスト専用です。クライアントでは呼び出さないでください");
                return -1;
            }
            var castPlayerRef = PlayerRef.FromEncoded(playerRef);
            var prefab = _database.GetPrefab(prefabGuid);
            if (prefab == null)
            {
                Debug.LogError($"GUID '{prefabGuid}' に対応するプレハブが見つかりません");
                return -1;
            }

            var obj = _runner.Spawn(prefab, position, rotation, castPlayerRef);
            if (setTransform != null)
            {
                obj.transform.SetParent(setTransform);
            }
            int id = _nextId++;
            _spawnedObjects[id] = obj;
            return id;
        }

        public async UniTask<int> SpawnAsync(string prefabGuid, Vector3 position, Quaternion rotation, Transform setTransform = null, int inputAuthority = default, Action onSpawned = null)
        {
            if (_runner == null)
            {
                _runner = FindFirstObjectByType<NetworkRunner>();
                if (_runner == null)
                {
                    Debug.LogError("NetworkRunnerがありません");
                    return -1;
                }
            }

            if (!_runner.IsServer)
            {
                Debug.LogError("SpawnAsync() はホスト専用です。クライアントでは呼び出さないでください");
                return -1;
            }

            var prefab = _database.GetPrefab(prefabGuid);
            if (prefab == null)
            {
                Debug.LogError($"GUID '{prefabGuid}' に対応するプレハブが見つかりません");
                return -1;
            }

            NetworkObject obj = await _runner.SpawnAsync(prefab, position, rotation, PlayerRef.FromEncoded(inputAuthority), onBeforeSpawned: (_, spawnedObj) =>
            {
                if (setTransform != null)
                {
                    spawnedObj.transform.SetParent(setTransform, true);
                }
            });

            int id = _nextId++;
            _spawnedObjects[id] = obj;
            return id;
        }
        
        public GameObject GetSpawnedObject(int objId)
        {
            if (_spawnedObjects.TryGetValue(objId, out var obj) && obj != null && obj.IsValid)
            {
                return obj.gameObject;
            }
            Debug.LogWarning($"ID {objId} に対応する NetworkObject が見つかりませんでした");
            return null;
        }

        /// <summary>
        /// ホスト専用：オブジェクト破棄
        /// </summary>
        public void Despawn(int objId)
        {
            if (_runner == null)
            {
                _runner = FindFirstObjectByType<NetworkRunner>();
                if (_runner == null)
                {
                    Debug.LogError("NetworkRunnerがありません");
                    return;
                }
            }

            if (!_runner.IsServer)
            {
                Debug.LogError("Despawn() はホスト専用です。クライアントでは呼び出さないでください");
                return;
            }

            if (_spawnedObjects.TryGetValue(objId, out var obj) && obj != null && obj.IsValid)
            {
                _runner.Despawn(obj);
                _spawnedObjects.Remove(objId);
            }
            else
            {
                Debug.LogWarning($"ID {objId} に対応する NetworkObject が見つかりませんでした");
            }
        }

        public void Register(ServiceLocator locator)
        {
            locator.Register<ISpawner>(this);
        }
    }
}
