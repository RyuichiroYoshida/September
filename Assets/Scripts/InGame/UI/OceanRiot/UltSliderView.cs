using DG.Tweening;
using InGame.Player.Ult;
using September.Common;
using September.InGame.Common;
using UnityEngine;

namespace September
{
    public class UltSliderView : MonoBehaviour
    {
        [SerializeField] private float _easeDuration = 0.2f;
        private void Start()
        {

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
                
                model.OnProgressChanged += () => SetGaugeRotation(model.Progress);
            };
        }

        private void SetGaugeRotation(float ratio)
        {
            var angle = -(Mathf.Clamp01(ratio) * 360f);
            transform.DOLocalRotate(new Vector3(0f, 0f, angle), _easeDuration);
        }
    }
}
