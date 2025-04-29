using Fusion;
using September.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace September.InGame
{
    public enum AbilityType
    {
        Ride,
        Clash
    }
    
    public abstract class BaseAbility : BasePlayerModule
    {
        [SerializeField] float _interactRadius = 5f;
        [SerializeField] LayerMask _exhibitLayer;
        [Networked] NetworkButtons ButtonsPrevious { get; set; }

        ExhibitBase _currentExhibit;
        readonly Collider[] _colliders = new Collider[10];
        public override void FixedUpdateNetwork()
        {
            // 入力でアビリティを使用
            if (!GetInput<MyInput>(out var input))
                return;

            var pressed = input.Buttons.GetPressed(ButtonsPrevious);
            ButtonsPrevious = input.Buttons;

            if (pressed.IsSet(MyButtons.Interact))
            {
                DetectExhibits();
                if (_currentExhibit != null)
                {
                    InteractWith(_currentExhibit);
                }
                else
                    Debug.LogWarning("No exhibit found");
            }
            if (pressed.IsSet(MyButtons.Attack))
            {
                Attack();
            }
        }
        private void DetectExhibits()
        {
            var runner = NetworkRunner.GetRunnerForScene(SceneManager.GetActiveScene());
            // 指定の半径で展示物を検出
            runner.GetPhysicsScene().OverlapSphere(transform.position, _interactRadius, _colliders,
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
        // 演出が必要なとき
        public abstract void Use();
        // 展示物の種類によってAbilityを変更する
        public abstract void InteractWith(ExhibitBase exhibit);
        public abstract void Attack();
        public abstract AbilityType GetAbilityType();
    }
}
