using System;
using System.Collections.Generic;
using Fusion;
using NaughtyAttributes;
using September.Common;
using UnityEngine;

namespace InGame.Player.Ability
{
    
    
    /// <summary>
    /// アビリティの入力を受け取り実行を依頼するクラス
    /// ここでどのボタンでどのアビリティが実行されるか設定します
    /// </summary>
    public class AbilityInputHandler : NetworkBehaviour
    {
        [SerializeField, InfoBox("どのボタンでどのアビリティが発動するか設定します"), Label("アビリティの発動条件マップ")]
        private List<AbilityActionContext> _abilityActionContexts;

        private NetworkButtons _previousButtons;
        private IAbilityExecutor _abilityExecutor;

        public override void FixedUpdateNetwork()
        {
            if (!GetInput<PlayerInput>(out var input) || !HasInputAuthority) return;

            var context = new TriggerEventContext
            {
                CurrentButtons = input.Buttons,
                PreviousButtons = _previousButtons,
                Owner = gameObject
            };

            EvaluateConditions(context);
            _previousButtons = input.Buttons;
        }

        public void OnCollisionEnter(Collision collision)
        {
            var context = new TriggerEventContext
            {
                Owner = gameObject,
                EventPayload = collision
            };

            EvaluateConditions(context);
        }

        private void EvaluateConditions(TriggerEventContext triggerCtx)
        {
            foreach (var action in _abilityActionContexts)
            {
                if (action.Condition == null || !action.Condition.Evaluate(triggerCtx)) continue;
                var abilityCtx = new AbilityContext
                {
                    SourcePlayer = Object.InputAuthority.RawEncoded,
                    AbilityName = action.AbilityName,
                    ActionType = action.ActionType
                };

                _abilityExecutor ??= StaticServiceLocator.Instance.Get<IAbilityExecutor>();
                _abilityExecutor?.RequestAbilityExecution(abilityCtx);
            }
        }
    }
   
    #region 条件設定用のクラス群
    /// <summary>
    /// 入力トリガーとアビリティを紐付けたデータ
    /// </summary>
    [Serializable]
    public struct AbilityActionContext
    {
        [SerializeReference, SubclassSelector]
        public IActionCondition Condition;
        public AbilityName AbilityName;
        public AbilityActionType ActionType;
    }
    
    /// <summary>
    /// 条件の基底クラス
    /// </summary>
    public interface IActionCondition
    {
        string DisplayConditionSelectName { get; }  // 条件選択時の表示名
        string DisplayConditionName { get; }    // 条件選択後にインスペクタで表示する名前
        bool Evaluate(TriggerEventContext context);
    }

    /// <summary>
    /// ボタン系の条件の基底クラス
    /// </summary>
    [Serializable]
    public class ButtonActionConditionBase : IActionCondition
    {
        public PlayerButtons Button;
        public AbilityTriggerType TriggerType;

        public string DisplayConditionSelectName => $"ボタン系";
        public string DisplayConditionName => $"{Button} を {TriggerType} したとき";

        public bool Evaluate(TriggerEventContext ctx)
        {
            var index = (int)Button;
            return TriggerType switch
            {
                AbilityTriggerType.タップ => ctx.CurrentButtons.GetPressed(ctx.PreviousButtons).IsSet(index),
                AbilityTriggerType.ホールド => ctx.CurrentButtons.IsSet(index) && !ctx.CurrentButtons.GetPressed(ctx.PreviousButtons).IsSet(index),
                AbilityTriggerType.リリース => ctx.CurrentButtons.GetReleased(ctx.PreviousButtons).IsSet(index),
                _ => false
            };
        }
    }
    
    /// <summary>
    /// ぶつかった時の入力依頼情報を設定するクラス
    /// </summary>
    [Serializable]
    public class OnCollisionCondition : IActionCondition
    {
        public string DisplayConditionSelectName => "衝突イベント";
        [SerializeField]
        //private AbilityName _targetAbility = AbilityName.全てのアビリティ;

        public string DisplayConditionName => $"ぶつかった";

        public bool Evaluate(TriggerEventContext context)
        {
            // 衝突イベントとして渡された場合に true
            return context.EventPayload is Collision;
        }
    }
    #endregion
    
    public enum AbilityTriggerType
    {
        タップ,
        ホールド,
        リリース,
    }

    public enum AbilityActionType
    {
        発動,
        停止,
    }

    /// <summary>
    /// 実行するアビリティのEnum
    /// 後でAbilityBaseを継承したクラスに合わせて自動生成するようにする
    /// </summary>
    public enum AbilityName
    {
        None,
        クリエイトフロア,
        チャージショット,
        クリエイトシールド,
        全てのアビリティ,
    }
    
    /// <summary>
    /// アビリティ実行依頼時に必要な入力情報を保持する構造体
    /// </summary>
    [Serializable]
    public struct AbilityContext : INetworkStruct, IEquatable<AbilityContext>
    {
        public AbilityActionType ActionType;
        public int SourcePlayer;
        public AbilityName AbilityName;

        public bool Equals(AbilityContext other)
        {
            return ActionType == other.ActionType && SourcePlayer.Equals(other.SourcePlayer) && AbilityName == other.AbilityName;
        }

        public override bool Equals(object obj)
        {
            return obj is AbilityContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)ActionType, SourcePlayer, (int)AbilityName);
        }
    }
    
    /// <summary>
    /// 判定に使う情報を詰め込んだクラス
    /// </summary>
    public class TriggerEventContext
    {
        public NetworkButtons CurrentButtons;
        public NetworkButtons PreviousButtons;
        public GameObject Owner; // 発動者 or 被対象者
        public object EventPayload; // ぶつかったコライダーなどイベント用
    }
}
