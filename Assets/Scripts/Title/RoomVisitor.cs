using September.Common;
using UnityEngine;
using UnityEngine.UI;

namespace September.Title
{
    public class RoomVisitor : MonoBehaviour
    {
        [SerializeField] InputField _userName;
        [SerializeField] InputField _roomName;
        public void Join()
        {
            if (_userName.text == "" || _roomName.text == "") return;
            PlayerNetworkSettings.NickName = _userName.text;
            NetworkManager.Instance.JoinLobby(_roomName.text);
        }
    }
}