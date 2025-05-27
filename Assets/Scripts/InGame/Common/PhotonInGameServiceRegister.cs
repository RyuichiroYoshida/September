using Fusion;
using InGame.Common;
using September.Common;
using UnityEngine;

namespace September.InGame.Common
{
    /// <summary>
    /// IRegisterableService を見つけて ServiceLocator に登録
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class PhotonInGameServiceRegister : MonoBehaviour
    {
        private const string AbilityExecutorPrefabID = "52643197c4870fc49ac21aa68653aed4";
        private NetworkRunner _networkRunner;
        
        private void Awake()
        {
            var childServices = GetComponentsInChildren<IRegisterableService>(includeInactive: true);
            foreach (var service in childServices)
            {
                service.Register(StaticServiceLocator.Instance);
            }
            
            SetClientService();
            SetHostService();
            Debug.Log($"ServiceRegistrar: {childServices.Length} 件のサービスを登録しました。");
        }

        private void SetClientService()
        {
            _networkRunner = FindAnyObjectByType<NetworkRunner>();
            StaticServiceLocator.Instance.Register(_networkRunner);
        }
        
        private async void SetHostService()
        {
            if (_networkRunner == null || !_networkRunner.IsServer) return;

            var spawner = StaticServiceLocator.Instance.Get<ISpawner>();
            await spawner.SpawnAsync(AbilityExecutorPrefabID, Vector3.zero, Quaternion.identity, transform);
        }
    }
    
    public interface IRegisterableService
    {
        void Register(ServiceLocator locator);
    }
}
