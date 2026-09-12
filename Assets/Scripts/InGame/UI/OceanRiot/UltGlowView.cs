using System.Collections.Generic;

using InGame.Player.Ult;
using September.Common;
using September.InGame.Common;
using UnityEngine;

namespace September
{
    public class UltGlowView : MonoBehaviour
    {
        [SerializeField] private List<GameObject> _glowObjects;
        private void Start()
        {
            for (int i = 0; i < _glowObjects.Count; i++)
            {
                _glowObjects[i].SetActive(false);
            }

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

                model.OnProgressChanged += () => SetGlowObject(model.Progress >= 1f);
            };
        }

        private void SetGlowObject(bool isActive)
        {
            for (int i = 0; i < _glowObjects.Count; i++)
            {
                _glowObjects[i].SetActive(isActive);
            }
        }
    }
}
