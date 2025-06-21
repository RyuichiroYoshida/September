using Fusion;
using September.Common;
using UnityEngine;
using System.Collections.Generic;

namespace InGame.Interact
{
    [DisallowMultipleComponent]
    public abstract class InteractableBase : NetworkBehaviour
    {
        [SerializeField]
        private SerializableDictionary<CharacterType, float> _requiredInteractTimeDictionary = new();

        [SerializeField]
        private SerializableDictionary<CharacterType, float> _cooldownTimeDictionary = new();

        private float _lastInteractTime = -9999f;
        private float _lastUsedCooldownTime = 0f;

        public SerializableDictionary<CharacterType, float> RequiredInteractTimeDictionary => _requiredInteractTimeDictionary;

        public void Interact(IInteractableContext context)
        {
            if (!PlayerDatabase.Instance.PlayerDataDic.TryGet(PlayerRef.FromEncoded(context.Interactor), out var data))
            {
                Debug.LogWarning("[InteractableBase] インタラクト実行者のデータが見つかりません: " + context.Interactor);
                return;
            }

            var charaType = data.CharacterType;
            if (!Object.isActiveAndEnabled)
            {
                Debug.Log($"[InteractableBase] オブジェクトが非アクティブです: {context.Interactor}");
                return;
            }

            if (!ValidateInteraction(context))
            {
                Debug.Log($"[InteractableBase] OnValidateInteraction により拒否: {context.Interactor}");
                return;
            }

            // 実行
            OnInteract(context);

            // クールダウン登録
            _lastInteractTime = Runner ? Runner.SimulationTime : Time.time;
            _lastUsedCooldownTime = _cooldownTimeDictionary.Dictionary.GetValueOrDefault(charaType, 0f);
        }

        /// <summary>
        /// 共通のバリデーション（null, クールダウン）
        /// </summary>
        private bool ValidateInteraction(IInteractableContext context)
        {
            if (!PlayerDatabase.Instance.PlayerDataDic.TryGet(PlayerRef.FromEncoded(context.Interactor), out var data))
            {
                Debug.LogWarning("[InteractableBase] インタラクト実行者のデータが見つかりません: " + context.Interactor);
                return false;
            }

            var type = data.CharacterType;
            if (IsInCooldown())
            {
                Debug.Log($"[InteractableBase] クールダウン中で拒否: {context.Interactor}");
                return false;
            }

            if (!Object.isActiveAndEnabled)
            {
                Debug.Log($"[InteractableBase] オブジェクトが非アクティブです: {context.Interactor}");
                return false;
            }

            return OnValidateInteraction(context, type);
        }

        /// <summary>
        /// 派生クラスでの個別条件（ロック中、所有者チェックなど）
        /// </summary>
        protected virtual bool OnValidateInteraction(IInteractableContext context, CharacterType charaType)
        {
            return true;
        }
        
        protected abstract void OnInteract(IInteractableContext context);

        public bool IsInCooldown()
        {
            var currentTime = Runner ? Runner.SimulationTime : Time.time;
            float timeSinceLast = currentTime - _lastInteractTime;
            if (timeSinceLast < _lastUsedCooldownTime)
            {
                float remaining = _lastUsedCooldownTime - timeSinceLast;
                Debug.Log($"[InteractableBase] クールダウン中: 残り {remaining:F2} 秒");
                return true;
            }

            return false;
        }

    }

    public interface IInteractableContext
    {
        int Interactor { get; }
        Vector3 WorldPosition { get; }
        float RequiredInteractTime { get; }
    }

    public class InteractableContext : IInteractableContext
    {
        public int Interactor { get; set; }
        public Vector3 WorldPosition { get; set; }
        public float RequiredInteractTime { get; set; }
    }
}
