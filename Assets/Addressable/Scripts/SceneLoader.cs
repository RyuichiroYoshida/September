using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;


#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// シーンローダー
/// NOTE: シーンの読み込みを管理する
/// </summary>
public class SceneLoader
{
    // 制約
    // Editorでこのstaticクラスを作ってはいけない

    //シーン依存系
    static SceneDependencies _sceneDependencies;


    static public void CheckScene()
    {

    }



    static void SceneInit()
    {
        //現在のシーンの初期化処理をする
        var execArrayInit = GameObject.FindObjectsOfType<GameExecuterBase>();
        foreach (var exec in execArrayInit)
        {
            exec.InitializeScene();
        }
    }

    static void SceneTerm()
    {
        //現在のシーンの解放処理をする
        var execArrayTerm = GameObject.FindObjectsOfType<GameExecuterBase>();
        foreach (var exec in execArrayTerm)
        {
            exec.FinalizeScene();
        }
    }



    /// <summary>
    /// シーン呼び出し
    /// NOTE: 単一のシーンを呼び出す
    /// </summary>
    /// <param name="sceneName"></param>
    static public async void LoadSceneSimple(string sceneName)
    {
        //ベースシーンの読み込みをする＆待つ
        var baseSceneHandle = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        await UniTask.WaitUntil(() => baseSceneHandle.isDone);
    }


    /// <summary>
    /// シーン呼び出し
    /// NOTE: LoadSceneMode.Additiveを使用し、複数のシーンを結合する
    /// </summary>
    /// <param name="sceneName"></param>
    static public async void LoadScene(string sceneName)
    {
        if (_sceneDependencies == null)
        {
            var handle = Addressables.LoadAssetAsync<SceneDependencies>(SceneDependencies.AssetPath);
            await UniTask.WaitUntil(() => handle.IsDone);
            _sceneDependencies = handle.Result;
        }
        //okamuraAdd
        Debug.Log("LoadScene timing");
        var nextScene = _sceneDependencies.Get(sceneName);
        //依存シーンがある場合はベースシーンを拾ってきて再生する
        GameSettings.SceneSetting setting = null;
        if (nextScene.SceneType != SceneType.Normal && nextScene.SceneType != SceneType.Ignore)
        {
            setting = GameSettings.GetSetting(nextScene.SceneType.ToString());
        }

        SceneTerm();

        //シーン名
        if (!(setting == null || (setting != null && setting.BaseSceneName == "@PlayScene")))
        {
            sceneName = setting.BaseSceneName;
        }

#if USE_ADDRESSABLES
        //TBD
#else
        //ベースシーンの読み込みをする＆待つ
        var baseSceneHandle = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        await UniTask.WaitUntil(() => baseSceneHandle.isDone);

        if (setting != null)
        {
            //追加シーンの読み込みをする＆待つ
            List<AsyncOperation> handles = new List<AsyncOperation>();
            foreach (var addScene in setting.AdditiveSceneName)
            {
                handles.Add(SceneManager.LoadSceneAsync(addScene, LoadSceneMode.Additive));
            }
            await UniTask.WaitUntil(() => handles.All(h => h.isDone));
        }
#endif

        SceneInit();
    }





#if UNITY_EDITOR
    static SceneDependencies _sceneDBCache = null;

    static SceneDependencies GetSceneDB()
    {
        if (_sceneDBCache == null)
        {
            _sceneDBCache = AssetDatabase.LoadAssetAtPath<SceneDependencies>(SceneDependencies.AssetPath);
        }
        return _sceneDBCache;
    }

    public static void SceneDBCacheClear()
    {
        _sceneDBCache = null;
    }

    public static bool ChangeEditorScene(string currentSceneName)
    {
        SceneDependencies sceneDB = GetSceneDB();
        var current = sceneDB.Get(currentSceneName);

        if(current == null)
        {
            Debug.LogWarning(currentSceneName + "のシーンはありません");
            return false;
        }

        //依存シーンがある場合はベースシーンを拾ってきて再生する
        GameSettings.SceneSetting setting = null;
        if (current.SceneType != SceneType.Normal && current.SceneType != SceneType.Ignore)
        {
            setting = GameSettings.GetSetting(current.SceneType.ToString());
        }

        //エディタ実行時の最初のシーンに取得したシーンを設定
        //@PlaySceneの場合はコールされたシーンをベースにする
        //通常再生の場合もここを通る
        SceneDependencies.Dependencies baseScene;
        if (setting == null || (setting != null && setting.BaseSceneName == "@PlayScene"))
        {
            baseScene = current;
        }
        else
        {
            baseScene = sceneDB.Get(setting.BaseSceneName);
        }
        if (baseScene == null)
        {
            Debug.LogWarning(current.Name + "の再生に必要なシーンがありません");
            return false;
        }

        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(baseScene.AssetPath);
        if (sceneAsset == null)
        {
            Debug.LogWarning(baseScene.Name + "というシーンアセットは存在しません");
            return false;
        }

        EditorSceneManager.playModeStartScene = sceneAsset;

        return true;
    }

    public static async UniTask<bool> LoadAdditionalSceneEditorPlaying(string currentSceneName)
    {
        SceneDependencies sceneDB = GetSceneDB();
        var current = sceneDB.Get(currentSceneName);

        //依存シーンがある場合はベースシーンを拾ってきて再生する
        GameSettings.SceneSetting setting = null;
        if (current.SceneType != SceneType.Normal && current.SceneType != SceneType.Ignore)
        {
            setting = GameSettings.GetSetting(current.SceneType.ToString());
        }

        if (setting != null)
        {
            //起動時のドライバ
            setting.StartDriver?.Invoke();

            //追加シーンの読み込みをする＆待つ
            List<AsyncOperation> handles = new List<AsyncOperation>();
            foreach (var addScene in setting.AdditiveSceneName)
            {
                handles.Add(EditorSceneManager.LoadSceneAsync(addScene, LoadSceneMode.Additive));
            }
            await UniTask.WaitUntil(() => handles.All(h => h.isDone));
        }

        SceneInit();

        return true;
    }
#endif
}
