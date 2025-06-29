using Fusion;
using September.Common;
using UnityEngine;
using System.Collections.Generic;

namespace InGame.Interact
{
    [DisallowMultipleComponent]
    public class InteractableBase : NetworkBehaviour
    {
        [SerializeField]
        private SerializableDictionary<CharacterType, float> _requiredInteractTimeDictionary = new();

        [SerializeField]
        private SerializableDictionary<CharacterType, float> _cooldownTimeDictionary = new();
        
        [SerializeField]
        private SerializableDictionary<CharacterType, CharacterInteractEffectBase> _characterEffects;  //キャラクターごとのインタラクト時の効果

        [Networked]
        private float LastInteractTime { get; set; } = -9999f;
        
        [Networked]
        private float LastUsedCooldownTime { get; set; } = 0f;

        public SerializableDictionary<CharacterType, float> RequiredInteractTimeDictionary => _requiredInteractTimeDictionary;
        private CharacterInteractEffectBase _activeEffectBase;

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

        protected virtual void OnInteract(IInteractableContext context)
        {
            var charaType = context.CharacterType;

            if (_characterEffects.Dictionary.TryGetValue(CharacterType.All, out var effect))
            {
                _activeEffectBase = effect.Clone();
                _activeEffectBase.OnInteractStart(context, this);
                return;
            }

            if (_characterEffects.Dictionary.TryGetValue(charaType, out effect))
            {
                _activeEffectBase = effect.Clone();
                _activeEffectBase.OnInteractStart(context, this);
                return;
            }

            Debug.LogWarning($"[{name}] {charaType} のインタラクト効果が設定されていません");
        }

        protected bool IsInCooldown()
        {
            var currentTime = Runner ? Runner.SimulationTime : Time.time;
            float timeSinceLast = currentTime - LastInteractTime;
            return timeSinceLast < LastUsedCooldownTime;
        }

        private void Update()
        {
            _activeEffectBase?.OnInteractUpdate(Time.deltaTime);
        }

        private void LateUpdate()
        {
            _activeEffectBase?.OnInteractLateUpdate(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            _activeEffectBase?.OnInteractFixedUpdate();
        }

        public override void FixedUpdateNetwork()
        {
            _activeEffectBase?.OnInteractFixedNetworkUpdate();
        }

        private void OnCollisionStay(Collision collision)
        {
            _activeEffectBase?.OnInteractCollisionStay(collision);
        }

        // 必要に応じて外部 or クールダウンなどから呼び出す用
        public void EndInteract()
        {
            _activeEffectBase?.OnInteractEnd();
            _activeEffectBase = null;
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
