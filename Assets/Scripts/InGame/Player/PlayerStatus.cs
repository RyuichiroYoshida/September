using UnityEngine;

namespace InGame.Player
{
    [CreateAssetMenu(fileName = "PlayerStatus", menuName = "Scriptable Objects/PlayerStatus")]
    public class PlayerStatus : ScriptableObject
    {
        [SerializeField] string _characterName;
        [SerializeField] int _health;
        [SerializeField] float _speed;
        [SerializeField] float _stamina;
        [SerializeField] private float _staminaConsumption;
        [SerializeField] float _staminaRegen;
        [SerializeField] int _attackDamage;
        [SerializeField] int _attackDamageOgre;
        
        public int Health => _health;
        public float Speed => _speed;
        public float Stamina => _stamina;
        public float StaminaConsumption => _staminaConsumption;
        public float StaminaRegen => _staminaRegen;
        public int AttackDamage => _attackDamage;
        public int AttackDamageOgre => _attackDamageOgre;
    }
}
