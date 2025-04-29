using Fusion;
using September.OgreSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace September.InGame
{
    public class PlayerOgreChooser : MonoBehaviour
    {
        [SerializeField] Button _button;
        [SerializeField] CanvasGroup _canvas;

        private void Awake()
        {
            var runner = NetworkRunner.GetRunnerForScene(SceneManager.GetActiveScene());
            if (!runner.IsServer)
            {
                _canvas.alpha = 0;
                _canvas.interactable = false;
                _canvas.blocksRaycasts = false;
                return;
            }
            _button.onClick.AddListener(() =>
            {
                runner.GetSingleton<PlayerDatabase>().ChooseOgre();
                Cursor.lockState = CursorLockMode.Locked;
                _canvas.alpha = 0;
                _canvas.interactable = false;
                _canvas.blocksRaycasts = false;
            });
        }
    }
}