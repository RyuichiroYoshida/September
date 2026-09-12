using Result;
using UnityEngine;

namespace InGame.Exhibit.InteractEffect
{
    [CreateAssetMenu(menuName = "ScriptableObjects/HammerAttackSettings", fileName = "HammerAttackSettings", order = 0)]
    public class HammerAttackSettings : ScriptableObject
    {
        [SerializeField] private SerializableDictionary<ExhibitType, float> _disableDuration;

        public bool TryGetDisableDuration(ExhibitType exhibitType, out float duration)
        {
            return _disableDuration.Dictionary.TryGetValue(exhibitType, out duration);
        }
    }
}
