using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// シーン依存関係の設定用データ
/// </summary>
public class SceneDependencies : ScriptableObject
{
    public const string ScenePath = "Scenes";                                  //シーンのパス(このフォルダ以下にあるシーンを全検索する)
    public const string AssetPath = "Assets/Scenes/SceneDependencies.asset";    //このファイルが置かれている場所

    [System.Serializable]
    public class Dependencies
    {
        public bool CanIDebugSelect = true;     //デバッグシーン選択に出てくるか
        public string Name;                     //シーン名
        public string AssetPath;                //シーンがあるアセットパス
        public SceneType SceneType;             //シーンの生成タイプ(GameSettingsを参照)
    }

    [SerializeField] List<Dependencies> _dependencies;  //シーン設定リスト

    /// <summary>
    /// シーン設定取得
    /// </summary>
    /// <param name="name">シーン名</param>
    /// <returns></returns>
    public Dependencies Get(string name)
    {
        return _dependencies.Where(d => d.Name == name).FirstOrDefault();
    }


    /// <summary>
    /// シーン設定取得
    /// </summary>
    public List<Dependencies> GetAll()
    {
        return _dependencies;
    }

#if UNITY_EDITOR
    public void Set(List<Dependencies> dp) { _dependencies = dp; }
#endif
}