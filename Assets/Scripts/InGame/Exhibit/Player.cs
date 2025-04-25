using Fusion;
using September.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace September.InGame
{
    public class Player : NetworkBehaviour
    {
        [SerializeField] private float _detectRadius = 5f;
        [SerializeField] private LayerMask _exhibitLayer;
        [SerializeField] private AbilityType _abilityType;
        [Networked] public NetworkButtons ButtonsPrevious { get; set; }

        private ExhibitBase _currentExhibit;
        private IAbility _ability;
        private bool _isOkabe;
        private readonly Collider[] _colliders = new Collider[10];

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

        public override void FixedUpdateNetwork()
        {
            // 自分のキャラだけが処理を実行するように
            if (!HasInputAuthority)
            {
                return;
            }
            
            DetectExhibits();

            // 入力でアビリティを使用
            if (!GetInput<MyInput>(out var input))
                return;

            var pressed = input.Buttons.GetPressed(ButtonsPrevious);
            ButtonsPrevious = input.Buttons;

            if (_isOkabe && pressed.IsSet(MyButtons.Interact))
            {
                if (_currentExhibit != null)
                {
                    _ability?.InteractWith(_currentExhibit);
                }
                else
                    Debug.LogWarning("No exhibit found");
            }
        }

        private void DetectExhibits()
        {
            var runner = NetworkRunner.GetRunnerForScene(SceneManager.GetActiveScene());
            // 指定の半径で展示物を検出
            runner.GetPhysicsScene().OverlapSphere(transform.position, _detectRadius, _colliders,
                _exhibitLayer, QueryTriggerInteraction.Ignore);

            ExhibitBase closestExhibit = null;
            float closestDistance = float.MaxValue;

            foreach (var col in _colliders)
            {
                if(col == null)
                    continue;
                
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
            Gizmos.DrawWireSphere(transform.position, _detectRadius);
        }
#endif
    }
}