using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

namespace September.InGame.NauticalChart
{
    /// <summary> 嵐の管理を行うクラス </summary>
    public class StormManager : NetworkBehaviour
    { 
        [SerializeField] private SkyboxChanger _skyboxChanger;
        [SerializeField] private ThunderFactory _thunderFactory;

        public void StartStorm(float stormDuration, float thunderDuration)
        {
            StartStorm(stormDuration, destroyCancellationToken).Forget();

            if (HasStateAuthority)
            {
                StartThunder(thunderDuration);
            }
        }

        private void StartThunder(float duration)
        {
            _thunderFactory.ThunderSpawn(duration).Forget();
        }

        private async UniTask StartStorm(float duration, CancellationToken token)
        {
            _skyboxChanger.SkyboxChangeStorm();
            await UniTask.WaitForSeconds(duration, cancellationToken: token);
            _skyboxChanger.RestoreSkybox();
        }
    }
}
