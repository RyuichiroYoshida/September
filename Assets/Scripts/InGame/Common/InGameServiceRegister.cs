using September.Common;
using UnityEngine;

namespace September.InGame.Common
{
    /// <summary>
    /// Awake時に子の IRegisterableService を見つけて ServiceLocator に登録
    /// </summary>
    public class InGameServiceRegister : MonoBehaviour
    {
        private void Awake()
        {
            var services = GetComponentsInChildren<IRegisterableService>(includeInactive: true);
            foreach (var service in services)
            {
                service.Register(StaticServiceLocator.Instance);
            }

            Debug.Log($"ServiceRegistrar: {services.Length} 件のサービスを登録しました。");
        }
    }
    
    public interface IRegisterableService
    {
        void Register(ServiceLocator locator);
    }
}
