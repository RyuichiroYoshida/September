using September.Common;
using UnityEngine;
using UnityEngine.UI;

namespace September.Title
{
    public class RoomCreator : MonoBehaviour
    {
        [SerializeField] CanvasGroup _createRoomPanel;
        [SerializeField] InputField _userName;
        [SerializeField] InputField _roomName;
        [SerializeField] Slider _maxPlayers;
        [SerializeField] GameLauncher _launcher;
        private void Start()
        {
            HidePanel();
        }

        public void Create()
        {
            if (_roomName.text == "" || _userName.text == "") return;
            PlayerNetworkSettings.NickName = _userName.text;
            _launcher.CreateGame(_roomName.text, (int)_maxPlayers.value);
        }

        public void ShowPanel()
        {
            _createRoomPanel.alpha = 1;
            _createRoomPanel.interactable = true;
            _createRoomPanel.blocksRaycasts = true;
        }

        public void HidePanel()
        {
            _createRoomPanel.alpha = 0;
            _createRoomPanel.interactable = false;
            _createRoomPanel.blocksRaycasts = false;
        }
    }
}