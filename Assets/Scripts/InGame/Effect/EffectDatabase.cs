using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EffectDatabase", menuName = "Scriptable Objects/EffectDatabase")]
public class EffectDatabase : ScriptableObject
{
    [System.Serializable]
    public struct EffectData
    {
        [HideInInspector] public string Guid;
        public GameObject Prefab;
        public EffectType EffectType;
    }
    
    [SerializeField] private List<EffectData> _effects;
    private Dictionary<string, EffectData> _map;

    private void OnEnable()
    {
        if (_map != null) return; // 既に初期化済みなら何もしない
        
        _map = new Dictionary<string, EffectData>();
        foreach (var effect in _effects)
        {
            if (effect.Guid != null && effect.Prefab != null)
            {
                _map[effect.Guid] = effect;
            }
        }
    }

    public EffectData GetEffectData(string guid)
    {
        _map.TryGetValue(guid, out EffectData data);
        return data;
    }
}
