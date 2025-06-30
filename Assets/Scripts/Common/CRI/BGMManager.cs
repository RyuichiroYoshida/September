using UnityEngine.SceneManagement;

namespace CRISound
{
    public class BGMManager
    {
        private static bool _isInitialized;
        private static string _currentCueName;

        public static void Initialize()
        {
            if(_isInitialized)
                return;
            
            _isInitialized = true;

            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            string newCueName = GetCueNameByScene(scene.name);
            
            if(!string.IsNullOrEmpty(_currentCueName))
            {
                CRIAudio.StopBGM("BGM", _currentCueName);
            }
            
            if(!string.IsNullOrEmpty(newCueName))
            {
                CRIAudio.PlayBGM("BGM", newCueName);
                _currentCueName = newCueName;
            }
        }
        
        private static string GetCueNameByScene(string sceneName)
        {
            return sceneName switch
            {
                "TitleScene" => "Default_BGM",
                "InGameMock" => "Default_BGM",
                "BattleScene" => "Default_BGM",
                "EndingScene" => "Default_BGM",
                _ => "Default_BGM",
            };
        }
    }
}