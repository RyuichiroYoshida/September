using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// シーン再生をやりやすくするデバッグ
/// </summary>
[InitializeOnLoad]
public static class ScenePlayer
{
    /// <summary>
    /// コンストラクタ
    /// NOTE: InitializeOnLoad属性によりエディター起動時に呼び出される
    /// </summary>
    static ScenePlayer()
    {
        EditorApplication.playModeStateChanged += OnChangedPlayMode;
        EditorSceneManager.activeSceneChangedInEditMode += OnChangeScene;

        _currentSceneName = EditorSceneManager.GetActiveScene().name;
    }

    [UnityEngine.SerializeField] static string _currentSceneName = "";

    static void OnChangeScene(Scene currentScene, Scene nextScene)
    {
        _currentSceneName = nextScene.name;

        //シーンデータベースから現在のシーンが登録されているか確認してもらい、登録されていたらそのシーンから再生する
        SceneLoader.ChangeEditorScene(_currentSceneName);

        //UnityEngine.Debug.Log("Active Scene Changed:" + _currentSceneName);
    }

    private static void OnChangedPlayMode(PlayModeStateChange state)
    {
        switch(state)
        {
            case PlayModeStateChange.EnteredEditMode:
                break;

            case PlayModeStateChange.EnteredPlayMode:
                Play();
                break;

            case PlayModeStateChange.ExitingPlayMode:
                break;
        }
    }

    /// <summary>
    /// 現在のシーンチェックして、マージが必要なシーンを足す
    /// </summary>
    public static async void Play()
    {
        //シーンデータベースから現在のシーンが登録されているか確認してもらい、登録されていたらそのシーンで再生する
        //await SceneLoader.ChangeEditorScene(_currentSceneName);
        await SceneLoader.LoadAdditionalSceneEditorPlaying(_currentSceneName);

        EditorApplication.isPlaying = true;
    }
}