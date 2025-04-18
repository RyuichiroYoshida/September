using UnityEngine;

namespace September.InGame
{
    // モックアップ用
    // Playerにもたせる
    public class PlayerInfo : MonoBehaviour
    {
        [SerializeField] private FlightController _flightController;
        [SerializeField] private Color _playerColor;
        [SerializeField] private PlayerAvatar _playerAvatar;
        private IAbility _ability;
        
        [SerializeField] private float detectRadius = 5f;
        [SerializeField] private LayerMask exhibitLayer;

        private ExhibitBase _currentExhibit;

        private bool _isOkabe;

        public Color PlayerColor => _playerColor;
        public IAbility Ability => _ability;

        private void Start()
        {
            _ability = new RideAbility();
            _isOkabe = true;

            // ToDo : //public Text PlayerName => _playerName を　PlayerInfoにかく
            // if (_playerAvatar.PlayerName.text == "オカベライト")
            // {
            //     _isOkabe = true;
            //     Debug.Log(_playerAvatar.PlayerName);
            // }
        }

        private void Update()
        {
            // 指定の半径で展示物を検出
            Collider[] colliders = Physics.OverlapSphere(transform.position, detectRadius, exhibitLayer);

            _currentExhibit = null;

            foreach (var col in colliders)
            {
                if (col.TryGetComponent<ExhibitBase>(out var exhibit))
                {
                    _currentExhibit = exhibit;
                    break;
                }
            }

            // 入力でアビリティを使用
            if (_isOkabe && Input.GetKeyDown(KeyCode.E))
            {
                if (_currentExhibit != null)
                {
                    _ability?.InteractWith(_currentExhibit);
                }
            }
        }
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, detectRadius);
        }
#endif
    }
}