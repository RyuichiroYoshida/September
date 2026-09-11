using UnityEngine;

namespace InGame.Interact
{
    /// <summary>スキャン対象であることを表すクラス</summary>
    public class TakamuraScanTarget : MonoBehaviour
    {
        [SerializeField] Transform _pivot;
        [SerializeField] Transform _scanPos;
        public Transform ScanPos => _scanPos;

        /// <summary>
        /// PivotからMeshまでのワールド座標上の差分を返すメソッド
        /// </summary>
        /// <returns>PivotからMeshまでのワールド座標上の差分</returns>
        public Vector3 GetPivotOffset()
        {
            return _pivot != null
                ? transform.position - _pivot.position
                : Vector3.zero;
        }

        /// <summary>
        /// Pivotの正面を返すメソッド
        /// </summary>
        /// <returns>Pivotの正面</returns>
        public Vector3 GetPivotForward()
        {
            return _pivot != null
                ? _pivot.forward
                : transform.forward;
        }
    }
}
