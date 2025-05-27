using Cysharp.Threading.Tasks;
using September.Common;
using UnityEngine;

namespace InGame.Common
{
    public interface ISpawner
    {
        /// プレハブのGUIDを指定してオブジェクトを生成。戻り値として破棄するための識別子を返す(NetworkObjectに依存したくないため)
        int Spawn(string prefabGuid, Vector3 position, Quaternion rotation, Transform transform = null, IPlayerRef inputAuthority = null);
        
        UniTask<int> SpawnAsync(string prefabGuid, Vector3 position, Quaternion rotation, Transform transform = null, IPlayerRef inputAuthority = null);
        
        GameObject GetSpawnedObject(int id);
    
        ///　識別子を元にキャッシュしたオブジェクトを破棄
        void Despawn(int id);
    }
}