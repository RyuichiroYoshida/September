using September.Common;
using UnityEngine;
using UnityEngine.UI;

namespace September.Title
{
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
    }
}