using Fusion;
using September.Common;
using UnityEngine;
using System.Collections.Generic;

namespace InGame.Interact
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider), typeof(NetworkObject))]
    public abstract class InteractableBase : NetworkBehaviour
    {
        [SerializeField]
        private SerializableDictionary<CharacterType, float> _requiredInteractTimeDictionary = new();

        [SerializeField]
        private SerializableDictionary<CharacterType, float> _cooldownTimeDictionary = new();

        private readonly Dictionary<int, float> _lastInteractTimePerPlayer = new();

        public SerializableDictionary<CharacterType, float> RequiredInteractTimeDictionary => _requiredInteractTimeDictionary;
        public SerializableDictionary<CharacterType, float> CooldownTimeDictionary => _cooldownTimeDictionary;

        public void Interact(IInteractableContext context)
        {
            if (!ValidateInteraction(context))
            {
                Debug.LogWarning($"[InteractableBase] インタラクト拒否: {context.Interactor}");
                return;
            }

            OnInteract(context);
            _lastInteractTimePerPlayer[context.Interactor] = Runner ? Runner.SimulationTime : Time.time;
        }

        /// <summary>
        /// 共通のバリデーション（null, クールダウン）
        /// </summary>
        protected virtual bool ValidateInteraction(IInteractableContext context)
        {
            if (!PlayerDatabase.Instance.PlayerDataDic.TryGet(PlayerRef.FromEncoded(context.Interactor), out var data))
            {
                Debug.LogWarning("[InteractableBase] インタラクト実行者のデータが見つかりません: " + context.Interactor);
                return false;
            }

            var type = data.CharacterType;
            if (IsInCooldown(context.Interactor, type))
            {
                Debug.Log($"[InteractableBase] クールダウン中: {context.Interactor}");
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
        protected abstract bool OnValidateInteraction(IInteractableContext context, CharacterType charaType);
        
        protected abstract void OnInteract(IInteractableContext context);

        private bool IsInCooldown(int interactor, CharacterType type)
        {
            if (!_lastInteractTimePerPlayer.TryGetValue(interactor, out var lastTime)) return false;

            var currentTime = Runner ? Runner.SimulationTime : Time.time;
            var cooldown = CooldownTimeDictionary.Dictionary.GetValueOrDefault(type, 0f);
            return currentTime - lastTime < cooldown;
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
