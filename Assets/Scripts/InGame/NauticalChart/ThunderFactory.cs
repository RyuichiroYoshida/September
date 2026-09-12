using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using September.Common;
using September.InGame.Effect;
using UnityEngine;

namespace September.InGame.NauticalChart
{
    public class ThunderFactory : NetworkBehaviour
    {
        [Header("Prefab参照")]
        [Tooltip("雷の生成領域"), SerializeField] private BoxCollider _spawnArea;
        [SerializeField] private SkyboxChanger _skyboxChanger;

        [Header("時間")]
        [Tooltip("雷の生存時間"), SerializeField] private float _thunderLifeTime = 2.5f;
        [Tooltip("雷の生成間隔"), SerializeField] private float _thunderSpawnIntervalTime = 2.0f;

        [Header("個数制限")]
        [Tooltip("雷の生成個数制限"), SerializeField] private int _thunderSpawnCapacity = 5;

        private EffectSpawner _effectSpawner;
        CancellationTokenSource _cts;

        [Networked] public TickTimer _tickTimer { get; private set; }

        public override void Spawned()
        {
            base.Spawned();
            _cts = new CancellationTokenSource();
            _effectSpawner ??= StaticServiceLocator.Instance.Get<EffectSpawner>();
        }

        /// <summary> 雷の生成処理 </summary>
        public async UniTaskVoid ThunderSpawn(float duration)
        {
            _tickTimer = TickTimer.CreateFromSeconds(Runner, duration - _thunderLifeTime);

            // エフェクトIDを生成して、雷のエフェクトを生成
            for (int i = 0; i < _thunderSpawnCapacity; i++)
            {
                // 経過時間が総スカイボックス変更時間を超えたらループを終了
                if (_tickTimer.Expired(Runner)) break;

                var id = _effectSpawner.RequestPlayLoopEffect
                    (EffectType.Thunder, SpawnTransform(), Quaternion.identity, transform);
                ThunderDestroyAsync(id).Forget();
                await UniTask.WaitForSeconds(_thunderSpawnIntervalTime, cancellationToken: _cts.Token);

            }
        }

        /// <summary> 雷のエフェクトを一定時間後に停止する非同期処理 </summary>
        /// <param name="effectId"> 停止するエフェクトのID </param>
        private async UniTaskVoid ThunderDestroyAsync(EffectID effectId)
        {
            await UniTask.WaitForSeconds(_thunderLifeTime, cancellationToken: _cts.Token);
            _effectSpawner.StopEffect(effectId);
        }

        /// <summary> 雷の生成位置をランダムに決定する </summary>
        private Vector3 SpawnTransform()
        {
            return new Vector3
            (UnityEngine.Random.Range(_spawnArea.bounds.min.x, _spawnArea.bounds.max.x),
                _spawnArea.bounds.max.y,
                UnityEngine.Random.Range(_spawnArea.bounds.min.z, _spawnArea.bounds.max.z));
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
