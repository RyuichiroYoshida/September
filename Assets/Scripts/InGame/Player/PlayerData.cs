using UniRx;
using UnityEngine;

namespace InGame.Player
{
    // いろんなPlayerのデータの中継
    [RequireComponent(typeof(PlayerManager))]
    [RequireComponent(typeof(PlayerHealth))]
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerData : MonoBehaviour
    {
        PlayerManager _playerManager;
        PlayerHealth _health;
        PlayerMovement _movement;
        
        public string Nickname { get; private set; }
        public bool IsLocalPlayer
        {
            get
            {
                if (!_playerManager) _playerManager = GetComponent<PlayerManager>();
                return _playerManager.IsLocalPlayer;
            }
        }
        public BehaviorSubject<int> Health = new(0);
        public int MaxHealth // ゲーム中変わらない値はSubjectじゃなくていいよな
        {
            get
            {
                if (!_health) _health = GetComponent<PlayerHealth>();
                return _health.MaxHealth;
            }
        }
        public BehaviorSubject<float> StaminaSubject = new(0);
        public float MaxStamina
        {
            get
            {
                if (!_movement) _movement = GetComponent<PlayerMovement>();
                return _movement.MaxStamina;
            }
        }
        public int AttackDamage { get; private set; }
        public int AttackDamageOgre { get; private set; }
        public bool IsOgre { get; private set; }

        public void Start()
        {
            if (!_health) _health = GetComponent<PlayerHealth>();
            Health = _health.OnHealthChanged;
            
            if (!_movement) _movement = GetComponent<PlayerMovement>();
            StaminaSubject = _movement.OnStaminaChanged;
        }
    }
}
