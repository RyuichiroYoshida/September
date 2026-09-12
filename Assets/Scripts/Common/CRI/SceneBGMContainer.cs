using System;
using Cysharp.Threading.Tasks;
using InGame.Common;
using NaughtyAttributes;
using UnityEngine;

namespace September.Common.CRI
{
    [CreateAssetMenu(menuName = "Scriptable Objects/SceneBGMContainer")]
    public class SceneBGMContainer : AssetsContainerBase<SceneBGMContainer>
    {
        [SerializeField] private SceneBGM[] _maps;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void Init()
        {
            // Tの内容を確定させてから呼び出す必要があるため、継承先で呼び出している
            // もっといい方法はあると思う
            Load().Forget();
        }

        public bool TryGetBGM(string sceneName, out SceneBGM bgm)
        {
            bgm = default;

            foreach (var map in _maps)
            {
                if (map.SceneName == sceneName)
                {
                    bgm = map;
                    return true;
                }
            }

            return false;
        }

        public static async UniTask<GetSceneBGMResult> GetBGMAsync(string sceneName)
        {
            var instance = await GetInstance();

            if (instance == null)
            {
                // 失敗
                Debug.LogError("SceneBGMContainer is not loaded.");
                return new GetSceneBGMResult { IsSuccess = false };
            }

            if (!instance.TryGetBGM(sceneName, out SceneBGM bgm))
            {
                // 失敗
                Debug.LogWarning($"SceneBGM not found for scene: {sceneName}");
                return new GetSceneBGMResult { IsSuccess = false };
            }

            // 成功
            return new GetSceneBGMResult { IsSuccess = true, BGM = bgm };
        }
    }

    public struct GetSceneBGMResult
    {
        public bool IsSuccess;
        public SceneBGM BGM;
    }

    [Serializable]
    public struct SceneBGM
    {
        [Scene] public string SceneName;
        public BGMType BGMType;

        [SerializeField, AllowNesting, ShowIf("BGMType", BGMType.Constant)]
        private string _bgmName;

        public bool PlayOnSceneLoaded;

        public string GetConstantBGM()
        {
            if (BGMType != BGMType.Constant)
            {
                Debug.LogError($"BGMType is not Constant. BGMName: {_bgmName}");
                return null;
            }

            return _bgmName;
        }

        public string GetCharacterBGM(CharacterType characterType)
        {
            if (BGMType != BGMType.CharacterData)
            {
                Debug.LogError($"BGMType is not Character. BGMName: {_bgmName}");
                return null;
            }

            var data = CharacterDataContainer.Instance.GetCharacterData(characterType);
            if (string.IsNullOrEmpty(data.BGM))
            {
                Debug.LogError($"CharacterBGM does not contain {characterType}");
                return null;
            }

            return data.BGM;
        }
    }

    public enum BGMType
    {
        Constant,
        CharacterData,
    }
}
