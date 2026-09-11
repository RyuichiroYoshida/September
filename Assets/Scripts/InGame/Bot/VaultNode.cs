using UnityEngine;

namespace InGame.Bot
{
    public class VaultNode : MonoBehaviour
    {
        [SerializeField] private float _distance;
        [SerializeField] private float _groundRayDistance;
        [SerializeField] private LayerMask _groundMask;

        private Vector3 _currentPos;
        private Vector3? _goalPos;

        /// <summary>
        /// îÚÇ—âzÇ¶ÇΩêÊÇÃèÍèäÇéÊìæÇ∑ÇÈ
        /// </summary>
        public void RayGround()
        {
            Vector3 origin = transform.position + transform.forward * _distance;
            Ray ray = new Ray(origin, Vector3.down);

            if (Physics.Raycast(ray, out var hit, _groundRayDistance, _groundMask))
            {
                _goalPos = hit.point;
            }
            else
            {
                _goalPos = origin + Vector3.down * _groundRayDistance;
            }
        }

        public Vector3 GetStartPos()
        {
            return this.transform.position;
        }

        public Vector3 GetEndPos()
        {
            if (_goalPos == null)
            {
                RayGround();
            }
            return _goalPos.Value;
        }

        public void OnDrawGizmos()
        {
            Gizmos.color = Color.green;

            Vector3 forwardPos = transform.position + transform.forward * _distance;
            Gizmos.DrawLine(transform.position, forwardPos);

            if (_goalPos == null || _currentPos != transform.position)
            {
                _currentPos = transform.position;
                RayGround();
            }

            if (_goalPos.HasValue)
            {
                Gizmos.DrawLine(forwardPos, _goalPos.Value);
            }
        }
    }
}
