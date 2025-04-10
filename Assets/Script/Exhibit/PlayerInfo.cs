using UnityEngine;

namespace September.InGame
{
    // モックアップ用
    // Playerにもたせる
    public class PlayerInfo : MonoBehaviour
    {
        [SerializeField] private Color _playerColor;
        private IAbility _ability;

        public Color PlayerColor => _playerColor;
        public IAbility Ability => _ability;

        private void Start()
        {
            _ability = new RideAbility();
        }
    }
}