using System;
using UnityEngine;
using UniRx;

namespace September.InGame.UI
{
    // 各UIのイベントを所持するクラス
    // 登録も自身で行う
    public class UIController : MonoBehaviour
    {
        [SerializeField] private UIView _uiView;
        #region イベント

        private readonly Subject<Unit> _onClickShowOptionButton = new();
        private readonly Subject<Unit> _onClickCloseOptionButton = new();
        private readonly Subject<int> _sliderValueChanged = new();

        #endregion
        
        # region 外部公開プロパティ
        
        public IObservable<Unit> OnClickShowOptionButton => _onClickShowOptionButton;
        public IObservable<Unit> OnClickCloseOptionButton => _onClickCloseOptionButton;
        public IObservable<int> OnSliderValueChanged => _sliderValueChanged;
        
        #endregion

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            OnSliderValueChanged.Subscribe(hp => _uiView.ChangeHp(hp));
            OnClickShowOptionButton.Subscribe(_ => _uiView.ShowOptionUI());
            OnClickCloseOptionButton.Subscribe(_ => _uiView.CloseOptionUI());
        }

        // UIを動的に生成する
        private void GameStart()
        {
            _uiView.CreateMainUI();
        }
    }
}