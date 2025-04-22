using UnityEngine;

namespace September.InGame
{
    public class Player : MonoBehaviour
    {
        [SerializeField] private float detectRadius = 5f;
        [SerializeField] private LayerMask exhibitLayer;
        [SerializeField] private AbilityType _abilityType;

        private ExhibitBase _currentExhibit;
        private IAbility _ability;
        private bool _isOkabe;
        
        public IAbility Ability => _ability;

        private void Start()
        {
            switch (_abilityType)
            {
                case AbilityType.Ride:
                    _ability = new RideAbility();
                    break;
                    case AbilityType.Clash:
                    _ability = new ClashAbility();
                        break;
                default:
                    Debug.LogError($"Unknown ability type: {_abilityType}");
                    break;
            }
            
            _isOkabe = true;
        }

        private void Update()
        {
            // 指定の半径で展示物を検出
            Collider[] colliders = Physics.OverlapSphere(transform.position, detectRadius, exhibitLayer);
            
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
                else
                {
                    Debug.LogWarning("No exhibit found");
                }
            }
        }

        private void Attack()
        {
            
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