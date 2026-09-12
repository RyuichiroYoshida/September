using UnityEngine;

namespace Ingame.Tanihira
{
    [CreateAssetMenu(fileName = "FriendStatus", menuName = "ScriptableObjects/FriendStatus")]
    public class FriendStatus : ScriptableObject
    {
        [SerializeField] private int _maxHealth;
        [SerializeField] private int _attackPower = 10;
        [SerializeField] private float _friendRotateSpeed = 120f;
        [SerializeField] private float _friendFormationMoveSpeed = 3.5f;
        [SerializeField] private float _friendFormationDistance = 1.0f;
        [SerializeField] private float _friendChaseSpeed = 5f;
        [SerializeField] private float _friendChaseStoppingDistance = 1.0f;
        [SerializeField] private float _friendStunTime = 10.0f;
        [SerializeField] private float _friendAcceleration = 10.0f;

        public int MaxHealth { get => _maxHealth; set => _maxHealth = value; }
        public int AttackPower { get => _attackPower; set => _attackPower = value; }
        public float FriendRotateSpeed { get => _friendRotateSpeed; set => _friendRotateSpeed = value; }
        /// <summary> 通常時の移動速度 </summary>
        public float FriendFormationSpeed { get => _friendFormationMoveSpeed; set => _friendFormationMoveSpeed = value; }
        /// <summary> 通常時の停止距離（プレイヤーとの距離）。 </summary>
        public float FriendFormationDistance { get => _friendFormationDistance; set => _friendFormationDistance = value; }
        /// <summary> 追跡時の移動速度 </summary>
        public float FriendChaseSpeed { get => _friendChaseSpeed; set => _friendChaseSpeed = value; }
        /// <summary> 追跡時の停止距離。目標までの距離がこの値以内になれば攻撃を開始 </summary>
        public float FriendChaseStoppingDistance { get => _friendChaseStoppingDistance; set => _friendChaseStoppingDistance = value; }
        public float FriendStunTime { get => _friendStunTime; set => _friendStunTime = value; }
        public float FriendAcceleration { get => _friendAcceleration; set => _friendAcceleration = value; }

        public FriendStatus Clone()
        {
            FriendStatus clone = ScriptableObject.CreateInstance<FriendStatus>();
            clone.MaxHealth = this.MaxHealth;
            clone.AttackPower = this.AttackPower;
            clone.FriendRotateSpeed = this.FriendRotateSpeed;
            clone.FriendFormationSpeed = this.FriendFormationSpeed;
            clone.FriendFormationDistance = this.FriendFormationDistance;
            clone.FriendChaseSpeed = this.FriendChaseSpeed;
            clone.FriendChaseStoppingDistance = this.FriendChaseStoppingDistance;
            clone.FriendStunTime = this.FriendStunTime;
            clone.FriendAcceleration = this.FriendAcceleration;
            return clone;
        }
    }
}
