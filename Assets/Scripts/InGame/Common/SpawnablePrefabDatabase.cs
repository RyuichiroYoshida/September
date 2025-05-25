using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

[CreateAssetMenu(fileName = "SpawnablePrefabDatabase", menuName = "ScriptableObjects/SpawnablePrefabDatabase")]
public class SpawnablePrefabDatabase : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        [HideInInspector] public string Guid;
        public GameObject Prefab;
    }

    [SerializeField] private List<Entry> _entries;
    private Dictionary<string, GameObject> _map;

    private void OnEnable()
    {
        _map = new Dictionary<string, GameObject>();
        foreach (var entry in _entries)
        {
            if (entry.Guid != null && entry.Prefab != null)
            {
                _map[entry.Guid] = entry.Prefab;
            }
        }
    }

    public GameObject GetPrefab(string guid)
    {
        _map.TryGetValue(guid, out var prefab);
        return prefab;
    }

    public GameObject GetPrefabByNetworkObject(GameObject prefab)
    {
        foreach (var kvp in _map)
        {
            if (kvp.Value == prefab)
                return kvp.Value;
        }
        return null;
    }
}