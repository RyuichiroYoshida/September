using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;

using static SceneDependencies;
using System.Linq;

/// <summary>
/// シーン依存系設定のエディタ拡張
/// </summary>
[CustomEditor(typeof(SceneDependencies), true, isFallback = true)]
public class SceneDependenciesEditor : Editor
{
    public const string TypeScriptPath = "Addressable/Scripts/SceneType.cs";

    //動的生成
    public static void CreateSceneDependencies(bool isForceRecreate = false)
    {
        string assetRoot = "Assets/"; //Application.dataPath;
        //既にアセットあるか
        var db = AssetDatabase.LoadAssetAtPath<SceneDependencies>(AssetPath);
        if(db == null)
        {
            db = ScriptableObject.CreateInstance<SceneDependencies>();
            AssetDatabase.CreateAsset(db, AssetPath);
            //TODO: Addressablesの自動設定(できれば)
        }

        //動的生成
        //NOTE: 参照的にはレイヤを超えているのでよくないが、エディタ拡張なのでしょうがない。
        using (FileStream fs = new FileStream(assetRoot + TypeScriptPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        {
            List<string> keys = new List<string>() { "Normal", "Ignore" };
            keys = keys.Concat(GameSettings.SceneTypeDic.Keys.ToArray()).ToList();
            byte[] bytes = Encoding.UTF8.GetBytes("public enum SceneType\n{\n\t"+ string.Join(",\n\t",keys) + "\n};");
            fs.Write(bytes, 0, bytes.Length);
        }

        DirectoryInfo di = new DirectoryInfo(assetRoot + ScenePath);
        IEnumerable<FileInfo> files = di.EnumerateFiles("*.unity", SearchOption.AllDirectories);
        List<Dependencies> dList = new List<Dependencies>();
        foreach (var f in files)
        {
            string name = f.Name.Replace(".unity", "");
            var d = db.Get(name);
            if (d != null && !isForceRecreate)
            {
                dList.Add(d);
                continue;
            }

            dList.Add(new Dependencies() { AssetPath = f.FullName.Replace("\\", "/").Replace(Application.dataPath+"/", assetRoot), Name = name, SceneType = 0 });
        }
        db.Set(dList);
        AssetDatabase.SaveAssets();

        //更新したのでキャッシュを消す
        SceneLoader.SceneDBCacheClear();

        //sceneDBをもとにエディタのビルド設定も変更する
        //NOTE: 既に設定があれば順番は変わらないようにする
        List< EditorBuildSettingsScene> buildSettings = EditorBuildSettings.scenes.ToList();
        foreach (var d in dList)
        {
            if (d.SceneType == SceneType.Ignore) continue;
            if (buildSettings.Where(bs => bs.path == d.AssetPath).Count() > 0) continue;
            buildSettings.Add(new EditorBuildSettingsScene(d.AssetPath, true));
        }
        EditorBuildSettings.scenes = buildSettings.ToArray();
    }

    /// <summary>
    /// インスペクタ上で設定
    /// </summary>
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("再生成"))
        {
            CreateSceneDependencies();
        }
    }

    /// <summary>
    /// メニューから生成する
    /// </summary>
    [MenuItem("VTNTools/SceneManagement/CreateSceneDependencies")]
    public static void Create()
    {
        CreateSceneDependencies();
    }

    /// <summary>
    /// メニューから生成する
    /// </summary>
    [MenuItem("VTNTools/SceneManagement/RecreateSceneDependencies")]
    public static void Recreate()
    {
        CreateSceneDependencies(true);
    }
}