using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class TestTextUi : MonoBehaviour
{
    private Text text;
    // Start is called before the first frame update
    void Start()
    {
        text = GetComponent<Text>();
    }

    public async UniTask SetMessage(string message)
    {
        text.text = message;
        await UniTask.Delay(System.TimeSpan.FromSeconds(3));
        text.text = "";
    }
}
