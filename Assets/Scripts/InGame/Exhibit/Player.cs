using Fusion;
using UnityEngine;

namespace September.InGame
{
    public class Player : NetworkBehaviour
    {
        [SerializeField] private float detectRadius = 5f;
        [SerializeField] private LayerMask exhibitLayer;
        [SerializeField] private AbilityType _abilityType;

        private ExhibitBase _currentExhibit;
        private IAbility _ability;
        private bool _isOkabe;
        
        public IAbility Ability => _ability;

        public override void Spawned()
        {
            base.Spawned();
            
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
            // 自分のキャラだけが処理を実行するように
            if(!HasInputAuthority)
                return;

            DetectExhibits();

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

        private void DetectExhibits()
        {
            // 指定の半径で展示物を検出
            Collider[] colliders = Physics.OverlapSphere(transform.position, detectRadius, exhibitLayer);

            ExhibitBase closestExhibit = null;
            float closestDistance = float.MaxValue;
            
            foreach (var col in colliders)
            {
                if (col.TryGetComponent<ExhibitBase>(out var exhibit))
                {
                    float distance = Vector3.Distance(transform.position, exhibit.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestExhibit = exhibit;
                    }
                }
            }
            
            _currentExhibit = closestExhibit;
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