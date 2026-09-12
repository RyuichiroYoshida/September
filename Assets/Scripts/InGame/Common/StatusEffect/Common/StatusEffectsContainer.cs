using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace InGame.Common
{
    /// <summary>
    /// 状態異常をID管理するためのコンテナクラス
    /// RPCで送受信する時に使う
    /// </summary>
    [CreateAssetMenu(fileName = "StatusEffectsContainer", menuName = "Scriptable Objects/StatusEffectsContainer")]
    public class StatusEffectsContainer : AssetsContainerBase<StatusEffectsContainer>
    {
        [SerializeField] private StatusEffect[] _statusEffects;
        
        #if UNITY_EDITOR
        [InitializeOnLoadMethod]
        #else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        #endif
        private static void Init()
        {
            // Tの内容を確定させてから呼び出す必要があるため、継承先で呼び出している
            // もっといい方法はあると思う
            Load().Forget();
        }
        
        public StatusEffect GetStatusEffect(int id)
        {
            if (id < 0 || id >= _statusEffects.Length)
            {
                throw new IndexOutOfRangeException($"Index:{id} のステータスエフェクトが存在しません");
            }
            
            return _statusEffects[id];
        }

        public int GetStatusEffectIndex(StatusEffect statusEffect)
        {
            for (int i = 0; i < _statusEffects.Length; i++)
            {
                if (_statusEffects[i] == statusEffect || _statusEffects[i].name == statusEffect.name) return i;
            }

            throw new ArgumentException(
                $"ステータスエフェクト\"{statusEffect.name}\" が {nameof(StatusEffectsContainer)} に存在しません");
        }
    }
}
