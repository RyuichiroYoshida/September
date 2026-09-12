using UnityEngine;
using UnityEngine.UI;
using September.Common;

namespace September
{
    public class TutorialStartButton : MonoBehaviour
    {
        [SerializeField] private Button _tutorialButton;

        private void Start()
        {
            // 既存のボタン登録に追加
            _tutorialButton.onClick.AddListener(() =>
                NetworkManager.Instance.LoadTutorialScene().Forget());
        }
    }
}
