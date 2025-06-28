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

        [Networked]
        private float LastInteractTime { get; set; } = -9999f;
        
        [Networked]
        private float LastUsedCooldownTime { get; set; } = 0f;

        public SerializableDictionary<CharacterType, float> RequiredInteractTimeDictionary => _requiredInteractTimeDictionary;

        public void Interact(IInteractableContext context)
        {
            var charaType = context.CharacterType;

            if (!ValidateInteraction(context))
            {
                Debug.Log($"[InteractableBase] OnValidateInteraction により拒否: {context.Interactor}");
                return;
            }

            // 実行
            OnInteract(context);

            // クールダウン登録
            LastInteractTime = Runner ? Runner.SimulationTime : Time.time;
            LastUsedCooldownTime = _cooldownTimeDictionary.Dictionary.GetValueOrDefault(charaType, 0f);
        }

        

        /// <summary>
        /// 共通のバリデーション（null, クールダウン）
        /// インタラクト可能なときは true を返す
        /// </summary>
        public bool ValidateInteraction(IInteractableContext context)
        {
            var type = context.CharacterType;
            if (IsInCooldown())
            {
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
        /// インタラクト可能ならTrueを返す
        /// </summary>
        protected virtual bool OnValidateInteraction(IInteractableContext context, CharacterType charaType)
        {
            return true;
        }
        
        protected abstract void OnInteract(IInteractableContext context);

        protected bool IsInCooldown()
        {
            var currentTime = Runner ? Runner.SimulationTime : Time.time;
            float timeSinceLast = currentTime - LastInteractTime;
            return timeSinceLast < LastUsedCooldownTime;
        }

    }

    public interface IInteractableContext
    {
        int Interactor { get; }
        CharacterType CharacterType { get; set; }
    }

    // シンプルな実装例。必要に合わせて情報は追加してください
    public struct InteractableContext : IInteractableContext
    {
        public int Interactor { get; set; }
        public CharacterType CharacterType { get; set; }
    }
}
