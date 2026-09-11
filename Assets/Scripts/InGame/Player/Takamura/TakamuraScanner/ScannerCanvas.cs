using Fusion;
using UnityEngine;
using UnityEngine.UI;

namespace InGame.Player
{
    public class ScannerCanvas : NetworkBehaviour
    {
        [SerializeField] Image _targetImage;

        /// <summary>
        /// 擬態対象を示すImageを展示物の上に重なるように移動するメソッド
        /// </summary>
        /// <param name="pos">擬態対象の座標</param>
        public void SetImageOverExhibit(Vector3 pos)
        {
            _targetImage.transform.position = pos;
        }

        /// <summary>
        /// Imageの視認状態を変更するメソッド
        /// </summary>
        /// <param name="visible">見えるようにするかどうか</param>
        public void ChangeImageVisibility(bool visible)
        {
            if (_targetImage)
                _targetImage.enabled = visible;
        }
    }
}
