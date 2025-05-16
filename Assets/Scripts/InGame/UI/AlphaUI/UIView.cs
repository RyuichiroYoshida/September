using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UI;

namespace September.InGame.UI
{
    /// <summary>UIの管理</summary>
    public class UIView : MonoBehaviour
    {
        [SerializeField] private GameObject _uiPrefab;
        [SerializeField, Label("気絶バー")] private UIAnimation _hpBar;

        [SerializeField, Label("再生したいHPAnimationの名前")]
        private string _hpAnimationName;

        private Slider _hpBarSlider;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            _hpBarSlider = GetComponent<Slider>();
            
            if(_hpBarSlider == null)
                Debug.LogError($"{nameof(_hpBarSlider)} is null");
        }

        public void CreateMainUI()
        {
            if (_uiPrefab != null)
                Instantiate(_uiPrefab);
            else
                Debug.LogError("UIPrefab is null");
        }

        // Hpの更新
        public void ChangeHp(int value)
        {
            // Hp変更アニメーションを再生
            _hpBar.Play(_hpAnimationName);
            _hpBarSlider.value = value;
        }

        // Option画面を開く
        public void ShowOptionUI()
        {
        }

        // Option画面を閉じる
        public void CloseOptionUI()
        {
        }
    }
}