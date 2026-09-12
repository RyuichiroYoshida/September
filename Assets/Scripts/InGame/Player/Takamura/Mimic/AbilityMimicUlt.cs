using System;
using Fusion;
using InGame.Player.Ability.Effect;
using September.Common;
using UnityEngine;

namespace InGame.Player.Takamura.Mimic
{
    /// <summary>
    /// 視界内の指定キャラクターへ擬態するULT。
    /// 対象選択はConditionから行い、カットイン終了後にPrefab交換を予約する。
    /// </summary>
    [Serializable]
    public sealed class AbilityMimicUlt : AbilityUltBase
    {
        [Header("擬態対象")]
        [SerializeField, Min(0f)] private float _maxDistance = 15f;
        [SerializeField, Range(0f, 180f)] private float _maxAngle = 30f;
        [SerializeField] private LayerMask _lineOfSightMask = ~0;

        [Header("擬態設定")]
        [SerializeField, Min(0.1f)] private float _mimicDuration = 15f;
        private NetworkObject _preparedOwner;
        private NetworkObject _preparedTarget;
        private CharacterType _preparedCharacterType;

        /// <summary>
        /// 擬態対象を準備するメソッド
        /// </summary>
        /// <param name="owner">スキルを使うプレイヤー</param>
        /// <param name="input">入力情報</param>
        /// <returns>準備できたかどうか</returns>
        public bool TryPrepareTarget(GameObject owner, in PlayerInput input)
        {
            _preparedOwner = owner ? owner.GetComponent<NetworkObject>() : null;
            _preparedTarget = null;
            _preparedCharacterType = CharacterType.None;

            return _preparedOwner
                   && MimicTargetFinder.TryFindTarget(
                       _preparedOwner,
                       in input,
                       _maxDistance,
                       _maxAngle,
                       _lineOfSightMask,
                       out _preparedTarget,
                       out _preparedCharacterType);    // 静的メソッドで対象を計算
        }

        protected override void OnCutInStart()
        {
            // 準備ができていなければUlt終了のリクエスト
            if (!_preparedOwner || !_preparedTarget)
                RequestEndAbility();
        }

        protected override void OnCutInEnd()
        {
            // 準備ができていなければUlt終了のリクエスト
            if (!_preparedOwner || !_preparedTarget ||
                _preparedCharacterType == CharacterType.None
                || _preparedCharacterType == CharacterType.All)
            {
                RequestEndAbility();
                return;
            }

            // 擬態の予約をするメソッド
            MimicTransformationService.ReserveTransform(
                _preparedOwner,
                _preparedTarget.InputAuthority,
                _preparedCharacterType,
                _mimicDuration);

            RequestEndAbility();
        }

        protected override void OnEndUlt()
        {
            _preparedOwner = null;
            _preparedTarget = null;
            _preparedCharacterType = CharacterType.None;
        }
    }
}
