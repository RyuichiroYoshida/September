using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Common;
using InGame.Player.Ability;
using September.Common;
using UnityEngine;

namespace September.InGame.Common
{
    /// <summary>
    /// 必要なオブジェクトを生成し、IRegisterableService をすべて登録する初期化クラス
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class PhotonInGameServiceRegister : MonoBehaviour
    {
        [SerializeField] NetworkObject _abilityExecutorPrefab;
        private const string AbilityExecutorPrefabID = "52643197c4870fc49ac21aa68653aed4";
        private NetworkRunner _networkRunner;

        private async void Awake()
        {
            _networkRunner = FindAnyObjectByType<NetworkRunner>();
            if (_networkRunner == null)
            {
                Debug.LogError("NetworkRunner が見つかりませんでした");
                return;
            }
            StaticServiceLocator.Instance.Register(_networkRunner);

            //RegisterChildrenServices();
            
            // await SpawnLocalObjects();
            // if (_networkRunner.IsServer)
            // {
            //     await SpawnSharedObjects(); // サーバー専用オブジェクト（他に必要なら）
            // }
        }
        
        private async UniTask SpawnLocalObjects()
        {
            _networkRunner = FindAnyObjectByType<NetworkRunner>();
            if (_networkRunner == null)
            {
                Debug.LogError("NetworkRunner が見つかりませんでした");
            }
            StaticServiceLocator.Instance.Register(_networkRunner);
            var spawner = StaticServiceLocator.Instance.Get<ISpawner>();
            
            var spawned = await _networkRunner.SpawnAsync( _abilityExecutorPrefab, Vector3.zero, Quaternion.identity, _networkRunner.LocalPlayer);
            spawned.GetComponent<AbilityExecutor>().Register(StaticServiceLocator.Instance);
        }

        private async UniTask SpawnSharedObjects()
        {
            var spawner = StaticServiceLocator.Instance.Get<ISpawner>();
            
            // 必要に応じて複数追加可能
            await spawner.SpawnAsync(AbilityExecutorPrefabID, Vector3.zero, Quaternion.identity, transform, _networkRunner.LocalPlayer.RawEncoded);
        }

        private void RegisterChildrenServices()
        {
            var allServices = GetComponentsInChildren<IRegisterableService>();

            if (allServices != null)
            {
                foreach (var service in allServices)
                {
                    service.Register(StaticServiceLocator.Instance);
                }

                Debug.Log($"[PhotonInGameServiceRegister] {allServices.Count()} 件のサービスを登録しました");
            }
        }
    }

    public interface IRegisterableService
    {
        void Register(ServiceLocator locator);
    }
}
