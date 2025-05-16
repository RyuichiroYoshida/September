using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

namespace September.InGame.UI
{
    [Serializable]
    public class UIAnimationEntry
    {
        [Label("UIの種類")] public UIType UIType;
        [Label("再生したいAnimation")] public RuntimeAnimatorController Animator;
    }

    [CreateAssetMenu(fileName = "UIAnimationDataBase", menuName = "UI/UIAnimationDataBase")]
    public class UIAnimationDataBase : ScriptableObject
    {
        public List<UIAnimationEntry> Entries = new();

        private Dictionary<UIType, RuntimeAnimatorController> _cache;

        // Dictionaryの紐づけ
        public RuntimeAnimatorController GetClip(UIType uiType)
        {
            if (_cache == null)
            {
                _cache = new();
                foreach (UIAnimationEntry entry in Entries)
                {
                    _cache[entry.UIType] = entry.Animator;
                }
            }

            _cache.TryGetValue(uiType, out RuntimeAnimatorController clip);
            return clip;
        }
    }
}