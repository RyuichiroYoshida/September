using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UniRx;
using UnityEngine;
using UnityEngine.Profiling;

namespace September.InGame.Common.Stats
{
    /// <summary>
    /// ステータスを管理するコンポーネント
    /// </summary>
    public abstract class StatsManager : NetworkBehaviour
    {
        [SerializeField] private StatsEffectorBase[] _modifiers;

        /// <summary> エフェクト適用前のステータス </summary>
        [Networked] private ref StatsContainer BaseStats => ref MakeRef<StatsContainer>();

        private StatusPipeline _pipeline;

        /// <summary> エフェクト適用後のステータス </summary>
        protected StatsContainer CurrentStats;

        /// <summary> 変更を検知する用 </summary>
        private StatsContainer _prevStats;

        /// <summary> ステータスの変更を外に通知する用 </summary>
        private Dictionary<StatType, Subject<float>> _statOnChangedSubjectsTable;

        /// <summary> 変更があったステータスを追跡する用 </summary>
        private readonly HashSet<StatType> _statDirtyFlags = new();

        /// <summary>
        /// ステータスの種類や値を初期化するために使用する
        /// </summary>
        protected abstract StatsContainer GetInitialStats();

        public sealed override void Spawned()
        {
            BaseStats = GetInitialStats();
            _prevStats = GetInitialStats();
            CurrentStats = GetInitialStats();
            _pipeline = new StatusPipeline(_modifiers);
            _statOnChangedSubjectsTable = BaseStats.Stats.ToDictionary(s => s.Key, _ => new Subject<float>());
        }

        public sealed override void FixedUpdateNetwork()
        {
            CalcStats();
            NotifyChangedStats();
        }

        /// <summary>
        /// 変更のあったステータスを外部に通知する
        /// </summary>
        private void NotifyChangedStats()
        {
            foreach (var dirtyStatType in _statDirtyFlags)
            {
                OnNextStatOnChanged(dirtyStatType, CurrentStats.Stats[dirtyStatType].Value);
            }

            _statDirtyFlags.Clear();
        }

        /// <summary>
        /// ステータスを再計算する
        /// </summary>
        private void CalcStats()
        {
            Profiler.BeginSample("StatsManager.CalcStats");

            _pipeline.CalcStats(BaseStats, ref CurrentStats);

            // 変更があったステータスを検出
            foreach (var (statType, prevStat) in _prevStats.Stats)
            {
                if (CurrentStats.TryGetStat(statType, out var currentStat) &&
                    (!Mathf.Approximately(prevStat.Value, currentStat.Value) ||
                     !prevStat.MaxValue.Equals(currentStat.MaxValue)))
                {
                    _statDirtyFlags.Add(statType);
                }
            }

            _prevStats = CurrentStats;

            Profiler.EndSample();
        }

        /// <summary>
        /// ステータスの変化を外部に通知する
        /// </summary>
        private void OnNextStatOnChanged(StatType statType, float value)
        {
            if (_statOnChangedSubjectsTable.TryGetValue(statType, out var subject))
            {
                subject.OnNext(value);
            }
        }

        /// <summary>
        /// 指定のステータスが変化したら通知を受け取る
        /// </summary>
        public void SubscribeStatOnChanged(StatType statType, Action<float> action)
        {
            if (_statOnChangedSubjectsTable.TryGetValue(statType, out var subject))
            {
                subject.Subscribe(action);
            }
        }

        public void SetBaseValue(StatType statType, float value)
        {
            BaseStats.TrySetStatValue(statType, value);
            CalcStats(); // 変更を即時反映
        }

        public void AddBaseValue(StatType statType, float value)
        {
            BaseStats.TryAddStatValue(statType, value);
            CalcStats();
        }
    }
}
