using Cysharp.Threading.Tasks;
using September.Common;
using September.InGame.Tutorial;
using UnityEngine;
using UnityEngine.UI;

namespace September
{
    public class TutorialEndButton : MonoBehaviour
    {
        [SerializeField] private Button _tutorialButton;

        private void Start()
        {
            if (_tutorialButton) _tutorialButton.onClick.AddListener(ExitTutorial);
        }

        private void ExitTutorial()
        {
            // 単体起動とTitle経由の終了を、開始時のRunnerを管理するSetupに任せる。
            var setup = FindFirstObjectByType<TutorialSceneSetup>();
            if (setup) setup.ExitToTitleAsync().Forget();
            else if (NetworkManager.Instance) NetworkManager.Instance.QuitLobby().Forget();
        }

        private void OnDestroy()
        {
            if (_tutorialButton) _tutorialButton.onClick.RemoveListener(ExitTutorial);
        }
    }
}
