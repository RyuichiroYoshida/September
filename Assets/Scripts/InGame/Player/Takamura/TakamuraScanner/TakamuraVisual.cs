using System;
using UnityEngine;
using Fusion;
using InGame.Interact;

namespace InGame.Player
{
    /// <summary>
    /// キャラクターの見た目を変える処理を持つクラス
    /// </summary>
    [Serializable]
    public class TakamuraVisual : NetworkBehaviour
    {
        [SerializeField] GameObject _character;
        GameObject _currentMimicTarget;

        public void Mimic(TakamuraScanTarget target)
        {
#if UNITY_EDITOR
            Debug.Log($"{GetInstanceID()} : 擬態");
#endif
            _character?.gameObject.SetActive(false);

            // 擬態オブジェクトの生成
            var mimicTarget = Instantiate(target, transform);
            _currentMimicTarget = mimicTarget.gameObject;

            // 擬態オブジェクトのTransform調整
            var mimicTransform = mimicTarget.transform;

            var sourceScale = target.transform.lossyScale;
            var destinationParentScale = mimicTransform.parent.lossyScale;
            mimicTransform.localScale = new Vector3(
                DivideScale(sourceScale.x, destinationParentScale.x),
                DivideScale(sourceScale.y, destinationParentScale.y),
                DivideScale(sourceScale.z, destinationParentScale.z));

            mimicTransform.rotation = target.transform.rotation;

            var destinationForward = _character != null
                ? _character.transform.forward
                : transform.forward;
            AlignForward(mimicTransform, mimicTarget.GetPivotForward(), destinationForward);

            var pivotOffset = mimicTarget.GetPivotOffset();
            mimicTransform.position = transform.position + pivotOffset;
        }

        float DivideScale(float sourceScale, float parentScale)
        {
            return Mathf.Approximately(parentScale, 0f) ? sourceScale : sourceScale / parentScale;
        }

        void AlignForward(Transform mimicTransform, Vector3 pivot, Vector3 destinationForward)
        {
            // 擬態オブジェクトの正面をxz平面に投影
            var pivotForward = Vector3.ProjectOnPlane(pivot, Vector3.up);
            // キャラクターの正面をxz平面に投影
            var targetForward = Vector3.ProjectOnPlane(destinationForward, Vector3.up);
            if (pivotForward.sqrMagnitude <= Mathf.Epsilon ||
                targetForward.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            // それぞれの角度の差分を取得
            var angle = Vector3.SignedAngle(pivotForward, targetForward, Vector3.up);
            // 差分だけ回転
            mimicTransform.Rotate(Vector3.up, angle, Space.World);
        }

        /// <summary>
        /// 擬態解除時の見た目を変えるメソッド
        /// </summary>
        public void Reveal()
        {
#if UNITY_EDITOR
            Debug.Log($"{GetInstanceID()} : 擬態解除");
#endif
            _character?.gameObject.SetActive(true);
            Destroy(_currentMimicTarget);
        }
    }
}
