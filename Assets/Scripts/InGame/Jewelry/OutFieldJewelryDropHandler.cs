using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Health;
using InGame.Jewelry.Common;
using InGame.Player;
using NaughtyAttributes;
using September.Common.Attribute;
using September.InGame.Jewelry.Drop;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace September.InGame.Jewelry
{
    public class OutFieldJewelryDropHandler : NetworkBehaviour
    {
        [SerializeField] private PlayerRespawn _playerRespawn;
        [SerializeField, RequireInterface(typeof(IJewelryContainer))] private Component _jewelryContainerObj;
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private PlayerHealth _playerHealth;

        [Header("宝石ドロップ設定")]
        [InfoBox("直前に攻撃を受けてから落下した場合、その攻撃情報をもとに宝石ドロップを処理します。")]
        [SerializeField] private JewelryDropSettingsContainer[] _dropSettings;
        [SerializeField, Tooltip("直前に受けた攻撃情報の保持秒数")]
        private float _hitRetentionTime = 20f;

        [Header("宝石ドロップアニメーション設定")]
        [SerializeField] private float _randomOffsetRange = 3f;
        [SerializeField] private float _searchFieldRadius = 10f;
        [SerializeField] private float _height = 1f;
        [SerializeField] private float _randomHeight = 0.3f;
        [SerializeField] private float _duration = 0.7f;
        [SerializeField] private float _randomDuration = 0.3f;
        [SerializeField] private float _randomDelay = 0.2f;
        [SerializeField] private AnimationCurve _verticalEase;
        [SerializeField] private AnimationCurve _horizontalEase;

        private IJewelryContainer _jewelryContainer;
        private HitData _recentHitData;
        private TickTimer _recentHitRetentionTimer;

        private Vector3 _prevGroundedPos;

        public override void Spawned()
        {
            if (!HasStateAuthority) return;

            _jewelryContainer = _jewelryContainerObj as IJewelryContainer;
            _playerRespawn.OnOutFieldEvent += OnOutField;
            _playerHealth.OnHitTaken += OnHitTaken;

            CleanHitData();
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            if (_playerMovement.IsGroundNet)
            {
                _prevGroundedPos = transform.position;
            }

            if (_recentHitRetentionTimer.Expired(Runner))
            {
                CleanHitData();
            }
        }

        private void OnOutField()
        {
            foreach (JewelryDropSettingsContainer dropSettings in _dropSettings)
            {
                HandleJewelryDrop(dropSettings.Settings);
            }

            JewelryDropLogger.OutputLog();

            CleanHitData();

            return;

            void HandleJewelryDrop(JewelryDropSettings dropSettings)
            {
                int count = dropSettings.GetDropAmount(_recentHitData, _jewelryContainer, true);
                var dropped = new IJewelry[count];
                _jewelryContainer.DropJewelry(dropSettings.JewelryType, count, dropped);

                DebugDrawUtility.DrawWireSphere(_prevGroundedPos, 1f, Color.green, 5f);

                foreach (IJewelry drop in dropped)
                {
                    if (drop is global::InGame.Jewelry.Jewelry jewelry)
                    {
                        if (_recentHitData.ExecutorRef.IsNone)
                        {
                            Vector3 pos = GetRandomPosition();
                            DebugDrawUtility.DrawWireSphere(pos, 1f, Color.red, 5f);
                            jewelry.JewelryControl.ThrowToNonPhysics(
                                pos,
                                (pos.y - transform.position.y) + _height + Random.Range(-.5f, .5f) * _randomHeight,
                                _duration + Random.Range(-.5f, .5f) * _randomDuration,
                                Random.value * _randomDelay,
                                _verticalEase,
                                _horizontalEase);
                        }
                        else
                        {
                            jewelry.PickupFrom(_recentHitData.ExecutorRef).Forget();
                        }
                    }
                }
            }

            Vector3 GetRandomPosition()
            {
                var insideUnitCircle = Random.insideUnitCircle;
                var randomOffset = new Vector3(insideUnitCircle.x, 0, insideUnitCircle.y) * _randomOffsetRange;
                var target = _prevGroundedPos + randomOffset;
                if (NavMesh.SamplePosition(target, out NavMeshHit hit, _searchFieldRadius, NavMesh.AllAreas))
                {
                    return hit.position;
                }

                return _prevGroundedPos;
            }
        }

        private void OnHitTaken(HitData hitData)
        {
            _recentHitData = hitData;
            _recentHitRetentionTimer = TickTimer.CreateFromSeconds(Runner, _hitRetentionTime);
        }

        private void CleanHitData()
        {
            // 自身が被害者となるHitDataで初期化
            _recentHitData = new HitData(HitActionType.Damage, 0, PlayerRef.None, Object.InputAuthority);
        }
    }
}
