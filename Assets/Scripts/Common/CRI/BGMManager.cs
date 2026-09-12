using Cysharp.Threading.Tasks;
using System;
using September.Common;
using September.Common.CRI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CRISound
{
    public static class BGMManager
    {
        private static bool _isInitialized;
        private static string _currentCueName;

        public static event Action OnBGMSwitching;

        public static void Initialize()
        {
            if(_isInitialized)
                return;
            
            _isInitialized = true;

            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        /// <summary>
        /// <see cref="SceneBGMContainer"/> に設定されたBGMを再生します。
        /// </summary>
        public static async UniTaskVoid ChangeBGM(string sceneName)
        {
            Debug.Log($"[BGMManager] ChangeBGM: {sceneName}");

            GetSceneBGMResult result = await SceneBGMContainer.GetBGMAsync(sceneName);
            if (!result.IsSuccess) return;
            ChangeBGM(result.BGM);
        }

        private static void ChangeBGM(SceneBGM bgm)
        {
            string newCueName = bgm.BGMType switch
            {
                BGMType.Constant => bgm.GetConstantBGM(),
                BGMType.CharacterData => bgm.GetCharacterBGM(LocalPlayer.CharacterType),
                _ => ""
            };

            if (newCueName == _currentCueName) return;

            if (!string.IsNullOrEmpty(_currentCueName)) StopBGM();

            if (!string.IsNullOrEmpty(newCueName))
            {
                Debug.Log($"[BGMManager] PlayBGM: {newCueName}");
                CRIAudio.PlayBGM("ALLCue", newCueName);
                _currentCueName = newCueName;
            }

            OnBGMSwitching?.Invoke();
        }

        public static void StopBGM()
        {
            Debug.Log("[BGMManager] StopBGM");
            CRIAudio.StopBGM("ALLCue", _currentCueName);
            _currentCueName = "";
        }

        private static async void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!CuePlayAtomExPlayer.Instance.IsReady)
            {
                await UniTask.WaitUntil(() => CuePlayAtomExPlayer.Instance.IsReady);
            }

            // タイトルに戻った時音量をリセット(SEも含め)
            if (scene.name == "Title")
            {
                CuePlayAtomExPlayer.Instance.ResetCategoryVolume();
            }

            Debug.Log($"[BGMManager] OnSceneLoaded: {scene.name}");

            GetSceneBGMResult result = await SceneBGMContainer.GetBGMAsync(scene.name);
            if (!result.IsSuccess) return;

            // シーンロード時に自動再生するか判定
            if (!result.BGM.PlayOnSceneLoaded) return;

            ChangeBGM(result.BGM);
        }
    }
}
