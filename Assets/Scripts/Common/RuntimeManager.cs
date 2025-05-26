using CRISound;
using September.Common;
using UnityEngine;

public static class RuntimeManager
{
    const string CharacterDataContainerAssetPath = "Assets/ScriptableObjects/CharacterDataContainer.asset";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RuntimeInitializeOnLoadMethod()
    {
        // ToDo : Addressableに変更
        GameObject prefab = Resources.Load<GameObject>("CRIObject");

        if(GameObject.Find(prefab.name))
            return;
        
        if (prefab != null)
        {
            CuePlayAtomExPlayer.Initialize();
        }
        else
            Debug.LogError($"{prefab.name} is not found in Resources");
        
        CharacterDataContainer.LoadAssetAsync(CharacterDataContainerAssetPath).Forget();
    }
}