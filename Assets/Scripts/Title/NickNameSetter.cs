using September.Common;
using UnityEngine;
using UnityEngine.UI;

namespace September.Title
{
    /// <summary>
    /// プレイヤー名の入力欄と保存データを同期する。
    /// </summary>
    [RequireComponent(typeof(InputField))]
    public class NickNameSetter : MonoBehaviour
    {
        InputField _inputField;

        private void Awake()
        {
            _inputField = GetComponent<InputField>();
            _inputField.text = NickNameProvider.GetNickName();
            _inputField.onEndEdit.AddListener(NickNameProvider.SetNickName);
        }

        private void OnDestroy()
        {
            _inputField.onEndEdit.RemoveListener(NickNameProvider.SetNickName);
        }

        /// <summary>
        /// 入力中のプレイヤー名を検証して保存する。
        /// </summary>
        /// <returns>有効なプレイヤー名を保存できた場合はtrue。</returns>
        public bool TryApplyNickName()
        {
            string nickName = _inputField.text.Trim();
            if (string.IsNullOrEmpty(nickName)) return false;

            _inputField.text = nickName;
            NickNameProvider.SetNickName(nickName);
            return true;
        }
    }
}
