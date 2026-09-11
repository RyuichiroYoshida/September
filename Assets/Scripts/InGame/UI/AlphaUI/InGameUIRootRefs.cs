using TMPro;
using InGame.Jewelry;
using UnityEngine;
using UnityEngine.UI;

namespace September.InGame.UI
{
    [DisallowMultipleComponent]
    public sealed class InGameUIRootRefs : MonoBehaviour
    {
        [Header("Panels / Texts")]
        public GameObject OptionUI;
        public GameObject LogPanel;
        public GameObject OgreUI;
        public ChangeTagOverlayMessage ChangeTagOverlayMessage;
        public TimeOverlayMessage TimeOverlayMessage;
        public CanvasGroup StatusUpGroup;
        public VerticalLayoutGroup StatusUpUIRoot;
        public TextMeshProUGUI ScoreText;
        public Image IconImage;
        public CanvasGroup FieldOutUI;
        public PlayerJewelryView JewelryView;
        public NoticeView NoticeUI;

        [Header("Bars")]
        public HpGaugeView HpBar;
        public Slider StaminaBar;

        [Header("Interact")]
        public InteractUi InteractUI;

        [Header("Timer")]
        public TextMeshProUGUI TimerText;
    }
}
