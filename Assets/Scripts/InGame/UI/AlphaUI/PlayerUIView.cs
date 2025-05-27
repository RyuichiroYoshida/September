using NaughtyAttributes;
using TMPro;
using UnityEngine;

namespace September.InGame.UI
{
    // PlayerUIのCanvasに配置
    public class PlayerUIView : MonoBehaviour
    {
        [SerializeField,Label("Playerの頭上UI調整用")] private float _userNameUIYOffset;
        [SerializeField,Label("NickNameTextPrefab")]private TextMeshProUGUI _userNameTextPrefab;
        
        private TextMeshProUGUI _userNameTextInstance;
        
        public void ShowNickNameUI(string nickName)
        {
            if (_userNameTextInstance == null)
            {
                _userNameTextInstance = Instantiate(_userNameTextPrefab, transform);
            }
            
            _userNameTextInstance.text = nickName;
            _userNameTextInstance.transform.localPosition = new Vector3(0, _userNameUIYOffset, 0);
            _userNameTextInstance.gameObject.SetActive(true);
        }
    }
}