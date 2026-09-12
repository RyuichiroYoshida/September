using DG.Tweening;
using InGame.Player.Ult;
using September.Common;
using September.InGame.Common;
using UnityEngine;
using UnityEngine.UI;

namespace September.InGame.Ult
{
    public class UltUI : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private float _easeDuration = 0.2f;

        private void Start()
        {
            _image.fillAmount = 0f;

            var inGameManager = StaticServiceLocator.Instance.Get<InGameManager>();
            
            // プレイヤーがスポーンされた後に処理を行う
            inGameManager.GameStarted += () =>
            {
                var runner = inGameManager.Runner;

                if (runner == null)
                {
                    Debug.LogError("[UltUI] No runner found");
                    return;
                }

                if (!runner.TryGetPlayerObject(runner.LocalPlayer, out var player))
                {
                    Debug.LogError("[UltUI] No player found");
                    return;
                }

                if (!player.gameObject.TryGetComponent<UltCondition>(out var model))
                {
                    Debug.LogError("[UltUI] No UltCondition found");
                    return;
                }
                
                model.OnProgressChanged += () => SetGaugeProgress(model.Progress);
            };
        }

        private void SetGaugeProgress(float ratio)
        {
            _image.DOFillAmount(ratio, _easeDuration);
        }
    }
}
