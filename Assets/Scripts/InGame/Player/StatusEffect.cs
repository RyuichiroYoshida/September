using System;
using UnityEngine;

namespace InGame.Player
{
    [CreateAssetMenu(menuName = "Scriptable Objects/Player/StatusEffect")]
    public class StatusEffect : ScriptableObject
    {
        [SerializeField] private DurationType _durationType;
        [SerializeField] private float _duration;
        [SerializeField] private bool _isPeriodic;
        [SerializeField] private float _period;
        [SerializeField] private StatModifier[] _modifier;
        [SerializeField] private StackOperation _stackOp = new(0, false);
        
        // Setting Parameter
        public DurationType DurationType => _durationType;
        /// <summary> 適用期間 </summary>
        public float Duration => Mathf.Max(_duration, 0);
        public bool IsPeriodic => _isPeriodic;
        /// <summary> Effect適用周期<br/>value ＜= 0 : not periodic </summary>
        public float Period => Mathf.Max(_period, 0.01f);
        public StatModifier[] ParamModifiers => _modifier;
        public StackOperation StackOp => _stackOp;
    }

    [Serializable]
    public struct StatModifier
    {
        [SerializeField] PlayerStatus.StatType _statType;
        [SerializeField] ModifierOperation _modifierOp;
        [SerializeField] float _magnitude;
            
        /// <summary> どの値に変更を加えるか </summary>
        public PlayerStatus.StatType StatType => _statType;
        /// <summary> 計算方法 </summary>
        public ModifierOperation ModifierOp => _modifierOp;
        /// <summary> 変更量 </summary>
        public float Magnitude => _magnitude;
    }

    [Serializable]
    public struct StackOperation
    {
        [SerializeField] private int _limitCount;
        [SerializeField] private bool _refreshDuration;
        [SerializeField] private ExpirationPolicyType _expirationPolicy;
        [SerializeField] private bool _modifierAppliesPerStack;
            
        /// <summary> 最大スタック数 </summary>
        public int LimitCount => Mathf.Max(_limitCount, 1);
        /// <summary> 複数スタック時に Duration をリセットするか </summary>
        public bool RefreshDuration => _refreshDuration;
        /// <summary> 終了時のスタックの減り方 </summary>
        public ExpirationPolicyType ExpirationPolicy => _expirationPolicy;
        /// <summary> スタック数に応じて効果量を増やすか </summary>
        public bool ModifierAppliesPerStack => _modifierAppliesPerStack;

        public StackOperation(int limitCount, bool refreshDuration, 
            ExpirationPolicyType expirationPolicy = ExpirationPolicyType.RemoveSingleStack, bool modifierAppliesPerStack = true)
        {
            _limitCount = limitCount;
            _refreshDuration = refreshDuration;
            _expirationPolicy = expirationPolicy;
            _modifierAppliesPerStack = modifierAppliesPerStack;
        }
    }

    public enum DurationType
    {
        Instant,
        HasDuration,
        Infinite
    }
            
    /// <summary> 計算方法 </summary>
    public enum ModifierOperation
    {
        Add,
        Multiply,
        Override
    }

    /// <summary> スタック削除時の設定 </summary>
    public enum ExpirationPolicyType
    {
        /// <summary> 全てのスタック削除 </summary>
        RemoveAllStack,
        /// <summary> スタック1つだけ削除 </summary>
        RemoveSingleStack,
        /// <summary> スタック1つ削除と効果時間 Refresh </summary>
        AndRefreshDuration
    }
}
