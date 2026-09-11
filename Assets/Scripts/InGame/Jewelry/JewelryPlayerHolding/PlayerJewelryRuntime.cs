using Fusion;
using InGame.Jewelry.Common;
using System;
using UnityEngine;

namespace InGame.Jewelry
{
    /// <summary>
    /// 宝石の所持情報を管理するクラス
    /// 値を同期させるためにNetworkBehaviourを継承させている
    /// </summary>
    public class PlayerJewelryRuntime : NetworkBehaviour
    {
        /// <summary>宝石の種類と個数の対応表</summary>
        [Networked, Capacity((int)JewelryType.JewelryTypeCount)]
        NetworkArray<int> _jewelryQuantities => default;

        /// <summary>直前の宝石の数を保存する配列</summary>
        int[] _preJewelryQuantities;
        JewelryInfo[] _jewelryInfos;
        bool _initialized;

        event Action<JewelryType, Sprite> _onInitialize;
        event Action<JewelryType, int> _onUpdateJewelryQuantity;

        public Action OnInitialize(Action<JewelryType, Sprite> act)
        {
            _onInitialize += act;
            return () => _onInitialize -= act;
        }
        public Action OnUpdateJewelryQuantity(Action<JewelryType, int> act)
        {
            _onUpdateJewelryQuantity += act;
            return () => _onUpdateJewelryQuantity -= act;
        }

        public void Init(PlayerJewelryDefinition definition)
        {
            if (definition == null || definition.HoldingJewelryInfos == null)
            {
                throw new InvalidOperationException();
            }

            var jewelryInfos = definition.HoldingJewelryInfos;

            // 宝石情報を保存
            _jewelryInfos = new JewelryInfo[(int)JewelryType.JewelryTypeCount];
            for (int i = 0; i < (int)JewelryType.JewelryTypeCount; i++)
            {
                _jewelryInfos[i] = jewelryInfos[i].JewelryInfo;
            }

            // 対応表の作成
            foreach (var info in jewelryInfos)
            {
                var jewelryType = info.JewelryInfo.JewelryType;
                var quantity = info.JewelryCount;

                if (HasStateAuthority)
                {
                    _jewelryQuantities.Set((int)jewelryType, quantity);
                }

                _onInitialize?.Invoke(jewelryType, info.JewelryInfo.JewelrySprite);
            }

            var score = CalculateJewelryScore();
            _onUpdateJewelryQuantity?.Invoke(JewelryType.NormalGem, score);

            // 直前の宝石の数を保存する配列の作成
            _preJewelryQuantities = new int[(int)JewelryType.JewelryTypeCount];
            for (int i = 0; i < _preJewelryQuantities.Length; i++)
            {
                _preJewelryQuantities[i] = _jewelryQuantities.Get(i);
            }
            _initialized = true;
        }

        public override void Render()
        {
            if (!_initialized) return;
            // 値の同期・変更が行われたタイミングで描画更新通知
            for (int i = 0; i < (int)JewelryType.JewelryTypeCount; i++)
            {
                var current = _jewelryQuantities.Get(i);

                if (current == _preJewelryQuantities[i]) continue;

                _preJewelryQuantities[i] = current;

                var score = CalculateJewelryScore();

                _onUpdateJewelryQuantity?.Invoke(JewelryType.NormalGem, score);
            }
        }

        /// <summary>
        /// 現在のスコアを計算するメソッド
        /// </summary>
        /// <returns>現在のスコア</returns>
        public int CalculateJewelryScore()
        {
            int result = 0;

            for (int i = 0; i < (int)JewelryType.JewelryTypeCount; i++)
            {
                var current = _jewelryQuantities.Get(i);

                // 所持数 * スコアを計算
                result += _jewelryInfos[i].Score * current;
            }

            return result;
        }

        public int GetJewelryCount()
        {
            int result = 0;

            for (int i = 0; i < (int)JewelryType.JewelryTypeCount; i++)
            {
                int current = _jewelryQuantities.Get(i);
                result += current;
            }

            return result;
        }

        /// <summary>
        /// 宝石を獲得した時に呼ばれるメソッド
        /// </summary>
        /// <param name="jewelryType">獲得した宝石の種類</param>
        public void GetJewelry(JewelryType jewelryType)
        {
            // enumの最後の要素（Count）が渡された場合はreturn
            if (jewelryType == JewelryType.JewelryTypeCount) return;

            if (HasStateAuthority)
            {
                var currentQuantity = _jewelryQuantities.Get((int)jewelryType);
                _jewelryQuantities.Set((int)jewelryType, currentQuantity + 1);
            }
        }

        /// <summary>
        /// 宝石を落とした時に呼ばれるメソッド
        /// </summary>
        /// <param name="jewelryType">落とした宝石の種類</param>
        public void DropJewelry(JewelryType jewelryType)
        {
            // enumの最後の要素（Count）が渡された場合はreturn
            if (jewelryType == JewelryType.JewelryTypeCount) return;

            if (HasStateAuthority)
            {
                var currentQuantity = _jewelryQuantities.Get((int)jewelryType);
                _jewelryQuantities.Set((int)jewelryType, currentQuantity - 1);
            }
        }

        /// <summary>
        /// 持っている宝石の数を種類に応じて取得するメソッド
        /// </summary>
        /// <param name="jewelryType">宝石の種類</param>
        /// <returns>現在持っている宝石の数 範囲外指定で-1が返る</returns>
        public int GetJewelryQuantity(JewelryType jewelryType)
        {
            return jewelryType != JewelryType.JewelryTypeCount
                ? _jewelryQuantities.Get((int)jewelryType)
                : -1;
        }
    }
}
