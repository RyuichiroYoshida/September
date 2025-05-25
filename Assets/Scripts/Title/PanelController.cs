using UnityEngine;

namespace September.Title
{
    [RequireComponent(typeof(CanvasGroup))]
    public class PanelController : MonoBehaviour
    {
        [SerializeField] bool _hideOnStart;
        CanvasGroup _panel;
        private void Awake()
        {
            _panel = GetComponent<CanvasGroup>();
            if(_hideOnStart) HidePanel();
        }

        public void ShowPanel()
        {
            _panel.alpha = 1;
            _panel.interactable = true;
            _panel.blocksRaycasts = true;
        }

        public void HidePanel()
        {
            _panel.alpha = 0;
            _panel.interactable = false;
            _panel.blocksRaycasts = false;
        }
    }
}