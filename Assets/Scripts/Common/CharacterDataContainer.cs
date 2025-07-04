using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace September.Common
{
    [CreateAssetMenu(fileName = "Character Data Container", menuName = "ScriptableObjects/CharacterDataContainer")]
    public class CharacterDataContainer : ScriptableObject
    {
        const int DataCount = 2;
        static CharacterDataContainer _instance;
        public static CharacterDataContainer Instance => _instance;
        [Serializable]
        public struct CharacterData
        {
            public CharacterType Type;
            public string DisplayName;
            public NetworkPrefabRef Prefab;
        }
        [SerializeField, ArrayLength(DataCount)] CharacterData[] _characterData;

        public static async UniTaskVoid LoadAssetAsync(string path)
        {
            _instance = await Addressables.LoadAssetAsync<CharacterDataContainer>(path);
        }

        public CharacterData GetCharacterData(CharacterType characterType)
        {
            return _characterData.FirstOrDefault(data=>data.Type == characterType);
        }

        public CharacterData GetCharacterData(int index)
        {
            return _characterData[index];
        }

        public string[] GetNames()
        {
            return _characterData.Select(data => data.DisplayName).ToArray();
        }
    }
}