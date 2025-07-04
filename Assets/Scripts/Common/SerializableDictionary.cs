using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KeyとValueのペアをUnityのInspectorで表示できるようにするためのシリアライズ可能なDictionary
/// NaughyyAttributesのLabelには対応しています
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
[Serializable]
public class SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver
{
    [SerializeField] private List<TKey> keys = new();
    [SerializeField] private List<TValue> values = new();

    private Dictionary<TKey, TValue> dictionary = new();

    public Dictionary<TKey, TValue> Dictionary => dictionary;

    public void Add(TKey key, TValue value)
    {
        if (dictionary.ContainsKey(key))
        {
            Debug.LogWarning($"Key '{key}' already exists. Skipped Add.");
            return;
        }

        dictionary.Add(key, value);
        keys.Add(key);
        values.Add(value);
    }

    public bool Remove(TKey key)
    {
        int index = keys.IndexOf(key);
        if (index >= 0)
        {
            keys.RemoveAt(index);
            values.RemoveAt(index);
        }
        return dictionary.Remove(key);
    }

    public bool ContainsKey(TKey key)
    {
        return dictionary.ContainsKey(key);
    }

    public TValue this[TKey key]
    {
        get => dictionary[key];
        set
        {
            if (dictionary.ContainsKey(key))
            {
                int index = keys.IndexOf(key);
                values[index] = value;
                dictionary[key] = value;
            }
            else
            {
                Add(key, value);
            }
        }
    }

    public void OnBeforeSerialize()
    {
        keys.Clear();
        values.Clear();

        foreach (var kvp in dictionary)
        {
            keys.Add(kvp.Key);
            values.Add(kvp.Value);
        }
    }

    public void OnAfterDeserialize()
    {
        dictionary = new Dictionary<TKey, TValue>();
        for (int i = 0; i < Math.Min(keys.Count, values.Count); i++)
        {
            if (!dictionary.ContainsKey(keys[i]))
            {
                dictionary.Add(keys[i], values[i]);
            }
            else
            {
                Debug.LogWarning($"重複するキーがあります: {keys[i]}");
            }
        }
    }
}
