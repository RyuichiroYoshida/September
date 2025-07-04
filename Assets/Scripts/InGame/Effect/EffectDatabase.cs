using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EffectDatabase", menuName = "Scriptable Objects/EffectDatabase")]
public class EffectDatabase : ScriptableObject
{
    [System.Serializable]
    public struct EffectData
    {
        public EffectType EffectType;
        [HideInInspector] public string Guid;
        public GameObject Prefab;
    }
    
    [SerializeField] private List<EffectData> _effects;
    private Dictionary<EffectType, EffectData> _map;

    private void OnEnable()
    {
        if (_map != null) return; // 既に初期化済みなら何もしない
        
        _map = new Dictionary<EffectType, EffectData>();
        foreach (var effect in _effects)
        {
            if (effect.Guid != null)
            {
                _map[effect.EffectType] = effect;
            }
        }
    }

    public EffectData GetEffectData(EffectType effectType)
    {
        _map.TryGetValue(effectType, out EffectData data);
        return data;
    }
}
