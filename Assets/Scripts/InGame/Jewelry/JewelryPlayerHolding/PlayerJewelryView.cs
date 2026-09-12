using TMPro;
using UnityEngine;
using Fusion;
using System;
using Common.Extensions;
using UnityEngine.UI;
using InGame.Jewelry.Common;

namespace InGame.Jewelry
{
    public class PlayerJewelryView : NetworkBehaviour
    {
        [Serializable]
        class JewelryUI
        {
            [SerializeField] Image _jewelryImage;
            [SerializeField] TextMeshProUGUI _jewelryCountText;

            public Image JewelryImage => _jewelryImage;
            public TextMeshProUGUI JewelryCountText => _jewelryCountText;
        }

        [SerializeField] JewelryUI[] _jewelryUIArray;
        [SerializeField] private bool _hideLocal = true;

        public override void Spawned()
        {
            if (_hideLocal && Object.InputAuthority == Runner.LocalPlayer)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 表示の初期化メソッド（アイコンのみ）
        /// </summary>
        /// <param name="jewelryType">宝石の種類</param>
        /// <param name="sprite">アイコン</param>
        public void Init(JewelryType jewelryType, InGame.Jewelry.Common.JewelryInfo jewelryInfo)
        {
            if (!Validate(jewelryType)) return;

            var image = _jewelryUIArray[(int)jewelryType].JewelryImage;
            image.sprite = _hideLocal
                ? jewelryInfo.JewelryOverheadSprite
                : jewelryInfo.JewelryUISprite;
        }

        /// <summary>
        /// 宝石の個数表示を更新するメソッド
        /// </summary>
        /// <param name="jewelryType">宝石の種類</param>
        /// <param name="count">宝石の数</param>
        public void UpdateJewelryCount(JewelryType jewelryType, int count)
        {
            if (!Validate(jewelryType)) return;

            var text = _jewelryUIArray[(int)jewelryType].JewelryCountText;
            text.text = count.ToString();
        }

        private bool Validate(JewelryType jewelryType)
        {
            // 宝石の所持情報を満足に表示できない場合はreturn
            if (_jewelryUIArray == null || _jewelryUIArray.Length <= (int)jewelryType)
            {
                Debug.LogWarning($"宝石UIが不足しています。{(int)jewelryType + 1}個のUIが必要です: {gameObject.GetHierarchyPath()}", this);
                return false;
            }

            if (_jewelryUIArray[(int)jewelryType].JewelryCountText == null)
            {
                Debug.LogWarning($"テキストコンポーネントが見つかりませんでした: {gameObject.GetHierarchyPath()}", this);
                return false;
            }

            if (_jewelryUIArray[(int)jewelryType].JewelryImage == null)
            {
                Debug.LogWarning($"Imageコンポーネントが見つかりませんでした: {gameObject.GetHierarchyPath()}", this);
                return false;
            }

            return true;
        }
    }
}
