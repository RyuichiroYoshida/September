using Fusion;
using UniRx;
using Unity.Mathematics;

namespace InGame.Player
{
    public class PlayerHealth : NetworkBehaviour, IDamageable
    {
        public readonly BehaviorSubject<int> OnHealthChanged = new(0);

        [Networked, OnChangedRender(nameof(OnChangeHealth))] private int Health { get; set; }
        void OnChangeHealth() => OnHealthChanged.OnNext(Health);
        [Networked] public int MaxHealth { get; private set; }

        public void Init(int health)
        {
            if (HasStateAuthority)
            {
                Health = health;
                MaxHealth = health;
                
                OnHealthChanged.OnNext(Health);
            }
        }

        // ダメージを受ける
        public void TakeDamage(int damage)
        {
            if (HasStateAuthority)
            {
                Health = math.max(Health - damage, 0);
                if (Health <= 0) Dead();
            }
        }

        public void Dead()
        {
            
        }
    }
}
