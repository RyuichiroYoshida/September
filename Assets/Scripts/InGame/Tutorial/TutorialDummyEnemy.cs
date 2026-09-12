using Fusion;
using InGame.Health;
using System;
using UnityEngine;

namespace September.InGame.Tutorial
{
    public class TutorialDummyEnemy : NetworkBehaviour, IDamageable
    {
        public PlayerRef OwnerPlayerRef => PlayerRef.None;
        private bool _isStartTutorial = false;
        private Action<HitData> _onAttack;   

        public bool IsAlive => true;

        public override void Spawned()
        {
            base.Spawned();
        }

        public void OnStartAbilityTutorial(Action<HitData>  onAttack)
        {
            this.gameObject.SetActive(true);
            _onAttack = onAttack;
            _isStartTutorial = true;
        }

        public void OnEndAbilityTutorial()
        {
            this.gameObject.SetActive(false);
            _onAttack = null;
            _isStartTutorial = false;
        }

        public void TakeHit(ref HitData hitData)
        {
            if (!_isStartTutorial) return;
            _onAttack?.Invoke(hitData);
        }
    }
}