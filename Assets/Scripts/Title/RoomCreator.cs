using September.Common;
using UnityEngine;
using UnityEngine.UI;

namespace September.Title
{
    public class RoomCreator : MonoBehaviour
    {
        [SerializeField] InputField _userName;
        [SerializeField] InputField _roomName;
        [SerializeField] Slider _maxPlayers;

        public void Create()
        {
            if (_roomName.text == "" || _userName.text == "") return;
            PlayerNetworkSettings.NickName = _userName.text;
            NetworkManager.Instance.CreateLobby(_roomName.text, (int)_maxPlayers.value);
        }
    }
}