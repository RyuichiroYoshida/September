using Fusion;
using UnityEngine;

namespace InGame.Exhibit.HazardTrail
{
    /// <summary>
    /// 追従対象の移動軌跡（足元）に沿って、一定距離・一定時間間隔ごとに地面ハザードオブジェクトをスポーンさせるエミッター。
    /// </summary>
    public class HazardTrailEmitter : NetworkBehaviour
    {
        [Header("ハザード生成設定")]
        [SerializeField] private NetworkPrefabRef _hazardPrefab;
        [SerializeField] private float _distanceInterval = 1.0f;
        [SerializeField] private float _minTimeInterval = 0.2f;
        [SerializeField] private Vector3 _spawnOffset = Vector3.zero;

        private Transform _targetTransform;
        private PlayerRef _currentOwner;
        private Vector3 _lastSpawnPos;
        private TickTimer _intervalTimer;
        private bool _isEmitting;

        /// <summary>
        /// 追従対象と所有者を設定し、ハザード生成を開始。
        /// </summary>
        public void StartEmitting(Transform targetTransform, PlayerRef owner)
        {
            _targetTransform = targetTransform;
            _currentOwner = owner;
            _lastSpawnPos = targetTransform.position;
            _intervalTimer = TickTimer.CreateFromSeconds(Runner, _minTimeInterval);
            _isEmitting = true;
        }
        /// <summary>
        /// ハザード生成を停止し、追従対象の参照をクリア。
        /// </summary>
        public void StopEmitting()
        {
            _isEmitting = false;
            _targetTransform = null;
            _currentOwner = PlayerRef.None;
        }
        /// <summary>
        /// 移動距離と経過時間から条件を満たした場合にハザードを生成。
        /// </summary>
        public void UpdateEmitter()
        {
            if (!_isEmitting || _targetTransform == null) return;
            // 直近の生成位置からの移動距離と、次回生成までのクールタイムを確認
            float dist = Vector3.Distance(_targetTransform.position, _lastSpawnPos);
            bool timePassed = _intervalTimer.ExpiredOrNotRunning(Runner);

            if (dist >= _distanceInterval && timePassed)
            {
                SpawnHazard(_targetTransform.position + _spawnOffset);
                _lastSpawnPos = _targetTransform.position;
                _intervalTimer = TickTimer.CreateFromSeconds(Runner, _minTimeInterval);
            }
        }
        /// <summary>
        /// 指定位置にネットワークハザードオブジェクトをスポーンし、初期化。
        /// </summary>
        private void SpawnHazard(Vector3 position)
        {
            Runner.Spawn(
                _hazardPrefab,
                position,
                Quaternion.identity,
                _currentOwner,
                (runner, obj) =>
                {
                    if (obj.TryGetComponent<GroundHazard>(out var hazard))
                    {
                        hazard.Initialize(_currentOwner, position);
                    }
                }
            );
        }
    }
}
