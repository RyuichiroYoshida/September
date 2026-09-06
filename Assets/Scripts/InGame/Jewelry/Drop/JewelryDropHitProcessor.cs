using System;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Health;
using InGame.Jewelry.Common;
using September.Common;
using UnityEngine;

namespace September.InGame.Jewelry.Drop
{
    [Serializable]
    public class JewelryDropHitProcessor : IHitProcessor
    {
        [SerializeField] private DropType _dropType;

        [Header("ドロップ数設定")]
        [SerializeField] private JewelryDropSettingsContainer[] _dropSettingsList;

        private readonly IJewelry[] _resultBuffer = new IJewelry[30];

        public DropType DropType { get => _dropType; set => _dropType = value; }

        public JewelryDropHitProcessor(DropType dropType)
        {
            DropType = dropType;
        }

        public void OnHitTaken(HitData hitData)
        {
            var runner = NetworkRunner.Instances[0];
            if (!runner) return;

            var targetObj = PlayerDatabase.Instance.PlayerObjectDic[hitData.TargetRef];
            var jewelryContainer = targetObj.GetComponentInChildren<IJewelryContainer>();
            if (jewelryContainer == null) return;

            foreach (var dropSettings in _dropSettingsList)
            {
                DropJewelry(dropSettings.Settings, jewelryContainer, hitData);
            }

            JewelryDropLogger.OutputLog();
        }

        private void DropJewelry(JewelryDropSettings dropSettings, IJewelryContainer victimJewelryContainer, HitData hitData)
        {
            int dropAmount = dropSettings.GetDropAmount(hitData, victimJewelryContainer, outputLog: true);

            int count = victimJewelryContainer.DropJewelry(dropSettings.JewelryType, dropAmount, _resultBuffer);

            // Dropの場合、デフォルトでその場にドロップするので何もしない
            if (DropType == DropType.Drop) return;

            // Pickup処理
            PickupJewelries(_resultBuffer.AsSpan(..count), hitData.ExecutorRef);
        }

        public static void PickupJewelries(ReadOnlySpan<IJewelry> jewelries, PlayerRef pickupPlayerRef)
        {
            foreach (IJewelry jewelry in jewelries)
            {
                if (jewelry is global::InGame.Jewelry.Jewelry jewel)
                {
                    jewel.PickupFrom(pickupPlayerRef).Forget();
                }
            }
        }
    }

    public enum DropType
    {
        Drop,
        Pickup
    }
}
