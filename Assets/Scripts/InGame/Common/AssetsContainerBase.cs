using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace InGame.Common
{
    public abstract class AssetsContainerBase<T> : ScriptableObject where T : AssetsContainerBase<T>
    {
        private static T _instance;
        private static string Address => typeof(T).Name;

        /// <summary>
        /// インスタンスを読み込む
        /// </summary>
        protected static async UniTask<T> Load()
        {
            if (_instance != null) return _instance;
            
            try
            {
                _instance = await Addressables.LoadAssetAsync<T>(Address);
                return _instance;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{typeof(T).Name}のインスタンスを取得できません。{typeof(T).Name}のアセットが存在することを確認し、Addressableのキーを\"{Address}\"に変更してください。");
            }

            return null;
        }

        public static bool TryGetInstance(out T instance)
        {
            if (_instance != null)
            {
                instance = _instance;
                return true;
            }
            
            instance = null;
            return false;
        }

        public static async UniTask<T> GetInstance() => await Load();
    }
}
