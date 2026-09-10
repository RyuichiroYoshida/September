using System;
using InGame.Common;
using September.Common;
using September.InGame.Common.Stats;
using September.InGame.Effect;
using UnityEngine;

namespace InGame.Player.Ability.Effect
{
    [Serializable]
    public class AbilityOkabeUlt : AbilityUltBase
    {
        [SerializeField] private float _duration = 5f;
        [SerializeField] private StatusEffect _buffEffect;
        [SerializeField] private EffectType _effectType;
        
        private StatusEffectManager _statusEffectManager;
        private EffectSpawner _effectSpawner;
        private EffectID _effectID;

        protected override void OnCutInEnd()
        {
            Debug.Log("[AbilityOkabeUlt] Start");
            var player = Parameter.Owner;

            if (_statusEffectManager || player.TryGetComponent(out _statusEffectManager))
            {
                _statusEffectManager.AddEffect(_buffEffect);
            }
            
            _effectSpawner = StaticServiceLocator.Instance.Get<EffectSpawner>();
            _effectID = _effectSpawner.RequestPlayLoopEffect(_effectType, player.transform.position, Quaternion.identity, player.transform);
        }

        protected override void OnUpdateUlt(float deltaTime)
        {
            if (TimeSinceCutInEnd > _duration)
            {
                RequestEndAbility();
            }
        }

        protected override void OnEndUlt()
        {
            _statusEffectManager?.RemoveEffect(_buffEffect);
            _effectSpawner?.StopEffect(_effectID);
        }
    }
}
