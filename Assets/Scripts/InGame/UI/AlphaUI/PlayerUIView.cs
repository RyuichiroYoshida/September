using Fusion;
using NaughtyAttributes;
using TMPro;
using UnityEngine;

namespace September.InGame.UI
{
    // PlayerUIのCanvasに配置
    public class PlayerUIView : MonoBehaviour,IUIView
    {
        [SerializeField,Label("Playerの頭上UI調整用")] private float _userNameUIYOffset;
        [SerializeField, Label("ユーザーネームを表示するUI")] private TextMeshPro _userNameCanvasPrefab;

        
        public void Initialize()
        {
            
        }
        
        // PlayerのSpawn時に同時に生成する
        public void ShowPlayerName(NetworkObject networkObject, string playerName)
        {
            _userNameCanvasPrefab.text = playerName;
            var userNameUI = Instantiate(_userNameCanvasPrefab.gameObject, networkObject.transform);
            userNameUI.transform.localPosition = new Vector3(0, _userNameUIYOffset, 0);
        }
    }
}