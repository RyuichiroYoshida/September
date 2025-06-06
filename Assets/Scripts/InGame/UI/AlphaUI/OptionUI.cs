using CRISound;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UI;

namespace InGame.UI
{
    public class OptionUI : MonoBehaviour
    {
        [SerializeField, Label("表示非表示させるUI")] private GameObject _optionUIPanel;
        [SerializeField, Label("BGMVolume")] private Slider _bgmVolumeSlider;
        [SerializeField, Label("SEVolume")] private Slider _seVolumeSlider;

        private CuePlayAtomExPlayer _cri;
        private GameInput _gameInput;
        private bool _isShow;
        private Vector2 _prevVolumeInput;
        
        private void Start()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            _bgmVolumeSlider.onValueChanged.RemoveAllListeners();
            _seVolumeSlider.onValueChanged.RemoveAllListeners();
        }

        private void Initialize()
        {
            _cri = CuePlayAtomExPlayer.Instance;
            _optionUIPanel.SetActive(false);
            _gameInput = new GameInput();
            _gameInput.Enable();

            _bgmVolumeSlider.onValueChanged.AddListener(_ => OnChangeCriBGMVolume());
            _seVolumeSlider.onValueChanged.AddListener(_ => OnChangeCriSEVolume());
        }

        
        private void Update()
        {
            // オプションUIの表示切り替え
            if (_gameInput.UI.Option.triggered)
            {
                _isShow = !_isShow;
                _optionUIPanel.SetActive(_isShow);

                if (_isShow)
                {
                    _optionUIPanel.transform.SetAsLastSibling();
                }
            }
            Vector2 currentInput = _gameInput.UI.Volume.ReadValue<Vector2>();

            // Y軸（上下）：BGM調整
            if (currentInput.y > 0.5f && _prevVolumeInput.y <= 0.5f)
                _bgmVolumeSlider.value = Mathf.Clamp01(_bgmVolumeSlider.value + 0.05f);
            else if (currentInput.y < -0.5f && _prevVolumeInput.y >= -0.5f)
                _bgmVolumeSlider.value = Mathf.Clamp01(_bgmVolumeSlider.value - 0.05f);

            // X軸（左右）：SE調整
            if (currentInput.x > 0.5f && _prevVolumeInput.x <= 0.5f)
                _seVolumeSlider.value = Mathf.Clamp01(_seVolumeSlider.value + 0.05f);
            else if (currentInput.x < -0.5f && _prevVolumeInput.x >= -0.5f)
                _seVolumeSlider.value = Mathf.Clamp01(_seVolumeSlider.value - 0.05f);

            _prevVolumeInput = currentInput;
        }

        private void OnChangeCriBGMVolume()
        {
            _cri.Player(SoundType.BGM).SetVolume(_bgmVolumeSlider.value);
        }

        private void OnChangeCriSEVolume()
        {
            _cri.Player(SoundType.SE).SetVolume(_seVolumeSlider.value);
        }
    }
}