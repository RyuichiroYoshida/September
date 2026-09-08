using InGame.Interact;
using System;
using UnityEngine;

namespace InGame.Exhibit.Candle
{
    /// <summary>
    /// 蝋燭オブジェクトへのインタラクト処理を担うエフェクトクラス。
    /// プレイヤーのインタラクト入力を受け取り、対応する <see cref="CandleInteractInvoker"/> のギミック発火RPCを呼び出します。
    /// </summary>
    [Serializable]
    public class CandleInteractEffect : CharacterInteractEffectBase
    {
        [SerializeField] private CandleInteractInvoker _invoker;
        public override void OnInteractStart(IInteractableContext context, InteractableBase target)
        {
            var invoker = _invoker;
            if (invoker != null)
            {
                invoker.StartAttack(context.Interactor);
            }
            else
            {
                Debug.LogError($"[CandleInteractEffect] {nameof(CandleInteractInvoker)} が設定されていないか、見つかりません。", target);
            }
        }
        public override CharacterInteractEffectBase Clone()
        {
            return new CandleInteractEffect
            {
                _invoker = _invoker
            };
        }
    }
}