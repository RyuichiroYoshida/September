using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace September
{
    // EventSystemが決定入力を処理する前に、操作対象のボタンを選択する。
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Button))]
    public class ButtonSelector : MonoBehaviour
    {
        private Button _button;
        private CancellationTokenSource _selectionCancellation;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            _selectionCancellation = new CancellationTokenSource();
            SelectButtonAsync(_selectionCancellation.Token).Forget();
        }

        private async UniTask SelectButtonAsync(CancellationToken token)
        {
            // UIの有効化を待つ。待機中にパネルが閉じた場合は選択しない。
            if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow()) return;
            SelectButton();
        }

        private void Update()
        {
            if (Gamepad.current == null || EventSystem.current == null) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            // 選択が消えた場合だけ戻し、他の有効なボタンへの移動は妨げない。
            if (selected == null || !selected.activeInHierarchy) SelectButton();
        }

        private void SelectButton()
        {
            if (!isActiveAndEnabled || !_button.IsActive() || !_button.IsInteractable() || !EventSystem.current) return;
            EventSystem.current.SetSelectedGameObject(_button.gameObject);
        }

        private void OnDisable()
        {
            _selectionCancellation?.Cancel();
            _selectionCancellation?.Dispose();
            _selectionCancellation = null;
        }
    }
}
