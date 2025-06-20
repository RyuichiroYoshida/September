using System.Linq;
using September.Common;
using UnityEngine;

namespace September.InGame.Common
{
    /// <summary>
    /// IRegisterableService を見つけて ServiceLocator に登録
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class InGameServiceRegister : MonoBehaviour
    {
        private void Awake()
        {
            var childServices = GetComponentsInChildren<IRegisterableService>(includeInactive: true);
            foreach (var service in childServices)
            {
                service.Register(StaticServiceLocator.Instance);
            }
            
            Debug.Log($"ServiceRegistrar: {childServices.Length} 件のサービスを登録しました。");
        }
    }

}
