using UnityEngine;

namespace September
{
    public class ControlGuideView : MonoBehaviour
    {
        [SerializeField]
        private SerializableDictionary<ControlDescriptionType, GameObject> _descriptionObjects;

        /// <summary>
        /// ControlDescriptionの内容をもとに操作説明UIを表示する
        /// </summary>
        public void GenerateDescription(ControlDescriptionType descriptionType)
        {
            ClearChildren();

            if (_descriptionObjects.Dictionary.TryGetValue(descriptionType, out var descriptionObject))
            {
                descriptionObject.SetActive(true);
            }
        }

        /// <summary>
        /// 生成前に子オブジェクトを全て非表示にする
        /// </summary>
        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                transform.GetChild(i).gameObject.SetActive(false);
            }
        }

    }
}
