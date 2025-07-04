using Fusion;
using NaughtyAttributes;
using September.Common;
using TMPro;
using UnityEngine;

namespace September.InGame.UI
{
    public class PlayerUIView : NetworkBehaviour
    {
        [SerializeField, Label("NickNameTextPrefab")] private TextMeshProUGUI _userNameTextPrefab;

        private TextMeshProUGUI _userNameTextInstance;

        public override void Spawned()
        {
            if (_userNameTextInstance == null)
            {
                _userNameTextInstance = Instantiate(_userNameTextPrefab, transform);
            }
            
            // 自分自身の頭上UIは非表示
            if (HasInputAuthority)
            {
                _userNameTextInstance.gameObject.SetActive(false);
            }
            else
            {
                _userNameTextInstance.text = PlayerDatabase.Instance.PlayerDataDic.Get(Object.InputAuthority).DisplayNickName;
                _userNameTextInstance.gameObject.SetActive(true);
            }
        }
    }
}