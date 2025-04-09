using System;
using System.Collections.Generic;

public class GameSettings
{
    public class SceneSetting
    {
        public string BaseSceneName = "";
        public Action StartDriver = null;
        public List<string> AdditiveSceneName = new List<string>();
    }

    
    // NOTE: @PlaySceneは現在設定中のシーンが
    static Dictionary<string, SceneSetting> _sceneTypeDic = new Dictionary<string, SceneSetting>()
    {
        {
            "Ingame" ,
            new SceneSetting(){
                BaseSceneName = "@PlayScene",
                AdditiveSceneName = new List<string>(){
                    "CRIExecuter",
                }
            }
        },
    };

    public static SceneSetting GetSetting(string key) => _sceneTypeDic.ContainsKey(key) ? _sceneTypeDic[key] : throw new KeyNotFoundException("SceneSettingのキーがありません:" + key);

#if UNITY_EDITOR
    public static Dictionary<string, SceneSetting> SceneTypeDic => _sceneTypeDic;
#endif
}