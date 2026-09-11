using UnityEngine;

namespace September.InGame.Kraken.Animations
{
    public class ConstraintManager : MonoBehaviour
    {
        [SerializeField] private ConstraintBase[] _constraints;

        public void Solve()
        {
            foreach (var constraint in _constraints)
            {
                constraint.PreSolve();
            }

            foreach (var constraint in _constraints)
            {
                constraint.ManualUpdate();
            }

            foreach (var constraint in _constraints)
            {
                constraint.PostSolve();
            }
        }
    }
}
