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
        public int MaxHealth { get; private set; }
        
        public float MaxStamina { get; private set; }
        public int AttackDamage { get; private set; }
        public int AttackDamageOgre { get; private set; }
        
        public bool ISLocalPlayer => _playerManager && _playerManager.IsLocalPlayer;
        
        
        #region Events 
        
        public ReactiveProperty<int> CurrentHealth { get; private set; } = new();
        public ReactiveProperty<int> CurrentStamina { get; private set; } = new();
        
        #endregion

        public void Initialize(PlayerManager playerManager, PlayerHealth health, PlayerMovement movement)
        {
            _playerManager = playerManager;
            MaxHealth = health.MaxHealth;
            MaxStamina = movement.MaxStamina;
        }
    }
}

// public void Start()
// {
//     if (!_health) _health = GetComponent<PlayerHealth>();
//     Health = _health.OnHealthChanged;
//
//     if (!_movement) _movement = GetComponent<PlayerMovement>();
//     StaminaSubject = _movement.OnStaminaChanged;
// }

// public int MaxHealth // ゲーム中変わらない値はSubjectじゃなくていいよな
// {
//     get
//     {
//         if (!_health) _health = GetComponent<PlayerHealth>();
//         return _health.MaxHealth;
//     }
// }

// public float MaxStamina
// {
//     get
//     {
//         if (!_movement) _movement = GetComponent<PlayerMovement>();
//         return _movement.MaxStamina;
//     }
// }

//public bool IsOgre { get; private set; }

// public bool IsLocalPlayer
// {
//     get
//     {
//         if (!_playerManager) _playerManager = GetComponent<PlayerManager>();
//         return _playerManager.IsLocalPlayer;
//     }
// }

// public BehaviorSubject<int> Health = new(0);
//
// public BehaviorSubject<float> StaminaSubject = new(0);
//
// public Subject<bool> IsOgre = new();