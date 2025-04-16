using CRISound;
using UnityEngine;

public static class RuntimeManager
{
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
    }
}