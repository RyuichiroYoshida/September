using September.Common;
using UnityEngine;
using UnityEngine.UI;

public class TitleBack : MonoBehaviour
{
    private Button _button;
    void Start()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(() => NetworkManager.Instance.QuitLobby().Forget());
    }
}
