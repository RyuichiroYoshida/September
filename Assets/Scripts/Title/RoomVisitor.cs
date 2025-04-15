using September.Common;
using UnityEngine;
using UnityEngine.UI;

namespace September.Title
{
    public class RoomVisitor : MonoBehaviour
    {
        [SerializeField] InputField _userName;
        [SerializeField] InputField _roomName;
        [SerializeField] GameLauncher _gameLauncher;
        public void Join()
        {
            if (_userName.text == "" || _roomName.text == "") return;
            PlayerNetworkSettings.NickName = _userName.text;
            _gameLauncher.JoinGame(_roomName.text);
        }
    }
}