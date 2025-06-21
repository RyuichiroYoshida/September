using System;
using Fusion;
using September.Common;
using UnityEngine;
using System.Collections.Generic;

namespace InGame.Interact
{
    [DisallowMultipleComponent]
    public abstract class InteractableBase : NetworkBehaviour
    {
        //各キャラクターのインタラクトにかかる時間の辞書
        [SerializeField]
        protected SerializableDictionary<CharacterType, float> _requiredInteractTimeDictionary = new();

        //各キャラクターのインタラクト後に適用されるクールダウン時間の辞書
        [SerializeField]
        protected SerializableDictionary<CharacterType, float> _cooldownTimeDictionary = new();
        
        // 各キャラクターがインタラクト可能かどうかの辞書
        [SerializeField]
        protected SerializableDictionary<CharacterType, bool> _allowInteractDictionary = new();

        [Networked]
        protected float LastInteractTime { get; set; } = -9999f;
        
        [Networked]
        protected float LastUsedCooldownTime { get; set; } = 0f;

        public SerializableDictionary<CharacterType, float> RequiredInteractTimeDictionary => _requiredInteractTimeDictionary;

        private void Start()
        {
            
        }

        public void Interact(IInteractableContext context)
        {
            if (!GetSessionPlayerData(context.Interactor, out var data)) return;

            var charaType = data.CharacterType;

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

        private static bool GetSessionPlayerData(int interactor, out SessionPlayerData data)
        {
            if (PlayerDatabase.Instance.PlayerDataDic.TryGet(PlayerRef.FromEncoded(interactor), out data))
            {
                return true;
            }

            Debug.LogWarning("[InteractableBase] インタラクト実行者のデータが見つかりません: " + interactor);
            return false;
        }

        /// <summary>
        /// 共通のバリデーション（null, クールダウン）
        /// インタラクト可能なときは true を返す
        /// </summary>
        public bool ValidateInteraction(IInteractableContext context)
        {
            if (!GetSessionPlayerData(context.Interactor, out var data))
            {
                Debug.Log("[InteractableBase] インタラクト実行者のデータが見つかりません: " + context.Interactor);
                return false;
            }

            var type = data.CharacterType;
            
            if (!_allowInteractDictionary.Dictionary.TryGetValue(type, out bool isAllowed) || !isAllowed)
            {
                Debug.Log($"[InteractableBase] インタラクトが許可されていません: {context.Interactor}, Type: {type}");
                return false;
            }
            
            if (IsInCooldown())
            {
                Debug.Log($"[InteractableBase] クールダウン中: {context.Interactor}, Type: {type}");
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

        protected virtual bool IsInCooldown()
        {
            var currentTime = Runner ? Runner.SimulationTime : Time.time;
            float timeSinceLast = currentTime - LastInteractTime;
            return timeSinceLast < LastUsedCooldownTime;
        }
    }

    public interface IInteractableContext
    {
        int Interactor { get; }
    }

    // シンプルな実装例。必要に合わせて情報は追加してください
    public struct InteractableContext : IInteractableContext
    {
        public int Interactor { get; set; }
    }
}
