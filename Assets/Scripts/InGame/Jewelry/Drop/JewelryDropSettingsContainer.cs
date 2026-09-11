using UnityEngine;

namespace September.InGame.Jewelry.Drop
{
    [CreateAssetMenu(fileName = "JewelryDropSettings", menuName = "ScriptableObjects/Drop Settings")]
    public class JewelryDropSettingsContainer : ScriptableObject
    {
        [SerializeField] private JewelryDropSettings _settings;

        public JewelryDropSettings Settings => _settings;
    }
}
