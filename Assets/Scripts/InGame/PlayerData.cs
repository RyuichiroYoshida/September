using UnityEngine;

namespace September.InGame
{
    [CreateAssetMenu(fileName = "Player Data", menuName = "ScriptableObjects/PlayerData", order = 1)]
    public class PlayerData : ScriptableObject
    {
        [SerializeField, InspectorName("体力")] int _hitPoint;
        [SerializeField, InspectorName("移動速度")] float _speed;
        [SerializeField, InspectorName("攻撃力")] int _attackDamage;
        [SerializeField, InspectorName("攻撃間隔")] float _attackInterval;
        [SerializeField, InspectorName("気絶時間")] float _stunTime;
        public int HitPoint => _hitPoint;
        public float Speed => _speed;
        public int AttackDamage => _attackDamage;
        public float AttackInterval => _attackInterval;
        public float StunTime => _stunTime;
    }
}