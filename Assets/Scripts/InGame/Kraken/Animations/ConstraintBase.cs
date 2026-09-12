using UnityEngine;

namespace September.InGame.Kraken.Animations
{
    public abstract class ConstraintBase : MonoBehaviour
    {
        public abstract void PreSolve();
        public abstract void PostSolve();
        public abstract void ManualUpdate();
    }
}
