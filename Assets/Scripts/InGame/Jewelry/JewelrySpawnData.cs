using System;
using Fusion;
using InGame.Jewelry.Common;
using UnityEngine;

namespace InGame.Jewelry
{
    [CreateAssetMenu(fileName = "September/Jewelry/JewelrySpawnData", menuName = "Jewelry Spawn Data")]
    public class JewelrySpawnData : ScriptableObject
    {
        [SerializeField] private JewelrySpawnSetting[] _spawnSettings;
        [Header("宝石種類ごとのPrefab")]
        [SerializeField] private JewelryPrefabEntry[] _prefabs;

        public JewelrySpawnSetting[] SpawnSettings => _spawnSettings;
        public JewelryPrefabEntry[] Prefabs => _prefabs;

        public NetworkObject GetPrefab(JewelryType jewelryType)
        {
            if (_prefabs == null) return null;

            foreach (var prefab in _prefabs)
            {
                if (prefab != null && prefab.JewelryType == jewelryType)
                    return prefab.Prefab;
            }

            return null;
        }
    }

    /// <summary>1回分の宝石出現設定</summary>
    [Serializable]
    public class JewelrySpawnSetting
    {
        [Header("出現時間")]
        [SerializeField] private float _spawnTime;

        [Header("出現する宝石と個数")]
        [SerializeField] private JewelrySpawnItem[] _items;

        [Header("出現範囲")]
        [Min(0f)]
        [SerializeField] private float _spawnRange;

        [Header("出現位置のindex")]
        [Tooltip("-1の場合はランダム。0以上の場合はSpawner側の地点配列のindex")]
        [SerializeField] private int _positionIndex = -1;

        [Header("出現位置からの高さ")]
        [SerializeField] private float _height;

        [Header("スポーン予告メッセージを出すか")]
        [SerializeField] private bool _showSpawnMessage;

        public float SpawnTime => _spawnTime;
        public JewelrySpawnItem[] Items => _items;
        public float SpawnRange => _spawnRange;
        public int PositionIndex => _positionIndex;
        public float Height => _height;
        public bool ShowSpawnMessage => _showSpawnMessage;
    }

    /// <summary>1種類の宝石の出現設定</summary>
    [Serializable]
    public class JewelrySpawnItem
    {
        [SerializeField] private JewelryType _jewelryType;

        [Min(0)]
        [SerializeField] private int _count;

        public JewelryType JewelryType => _jewelryType;
        public int Count => _count;
    }

    /// <summary>宝石種類とNetworkObject prefabの対応</summary>
    [Serializable]
    public class JewelryPrefabEntry
    {
        [SerializeField] private JewelryType _jewelryType;
        [SerializeField] private NetworkObject _prefab;

        public JewelryType JewelryType => _jewelryType;
        public NetworkObject Prefab => _prefab;
    }
}
