using System.Collections.Generic;
using System.Linq;
using September.Common;
using UnityEngine;

namespace Result
{
    public class ScoreTracker
    {
        // インタラクト回数
        private readonly Dictionary<ExhibitType, int> _count =  new();
        private ExhibitScoreConfig _config;
        
        private int _grapplingHookCount;
        private int _friendExhibitCount;
        private int _krakenDamageScore;
        
        private readonly Dictionary<ExhibitType, int> _destroyedExhibitCounts = new();

        // スコアの加算処理
        public void AddGrapplingHook() => _grapplingHookCount++;
        public void AddFriendExhibit() => _friendExhibitCount++;
        public void AddKrakenDamage(int score) => _krakenDamageScore += score;

        public int GrapplingHookCount => _grapplingHookCount;
        // 時間ないからこっちは使わない
        public int FriendExhibitCount => _friendExhibitCount;
        public int KrakenDamageScore => _krakenDamageScore;

        public ScoreTracker(ExhibitScoreConfig config)
        {
            _config = config;
        }
        
        public void SetConfig(ExhibitScoreConfig config) => _config = config;
        public IReadOnlyDictionary<ExhibitType,int> ExhibitCounts => _count;
        public IReadOnlyDictionary<ExhibitType,int> DestroyedExhibitCounts => _destroyedExhibitCounts;
        
        // インタラクト回数を追加
        public void AddInteract(ExhibitType t)
        {
            _count.TryAdd(t, 0);
            _count[t]++;
        }
        
        public void SetInteractCount(ExhibitType type, int value)
        {
            _count[type] = value < 0 ? 0 : value;
        }
        
        public int GetInteractCount(ExhibitType type)
        {
            return _count.GetValueOrDefault(type, 0);
        }

        public int GetTotalInteractCount()
        {
            return _count.Values.Sum();
        }
        
        public void AddDestroyed(ExhibitType t)
        {
            _destroyedExhibitCounts.TryAdd(t, 0);
            _destroyedExhibitCounts[t]++;
        }
        
        // 合計値を計算
        public int CalcTotal(SessionPlayerData playerData)
        {
            int sum = 0;
            // 展示物
            sum += _count.Sum(kv => (_config ? _config.GetPoint(kv.Key) : 0) * kv.Value);
            // 破壊
            sum += _destroyedExhibitCounts.Sum(kv => _config .GetDestroyPoint(kv.Key) * kv.Value);
            // 気絶
            const int stunPoint = 150;
            int stunCount = 0;
            foreach (var kv in playerData.StunData)
                stunCount += kv.Value;
            sum += stunCount * stunPoint;
            
            // クラーケンへのダメージ
            sum += _krakenDamageScore;
            
            return sum;
        }

        public void Clear()
        {
            _count.Clear();
            _destroyedExhibitCounts.Clear();
            _grapplingHookCount = 0;
            _friendExhibitCount = 0;
            _krakenDamageScore = 0;
        }
    }
}