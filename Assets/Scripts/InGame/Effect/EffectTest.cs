using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using September.InGame.Effect;
using UnityEngine;

public class EffectTest : MonoBehaviour
{

    EffectSpawner _effectSpawner;
    List<string> idList = new List<string>();

    int _spawn = 0;
    int _id = 0;

    private void Start()
    {
        _effectSpawner = GameObject.FindObjectOfType<EffectSpawner>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            //SpawnEffect();
            SpawnLoopEffect();
        }

        if (Input.GetKeyDown(KeyCode.O) && idList.Count > 0)
        {
            int i = idList.Count - 1;
            StopSpawnEffect(idList[i]);
        }
    }

    public void SpawnEffect()
    {
        _effectSpawner.RequestPlayOneShotEffect(EffectType.Test, new Vector3(0 + _spawn * 2, 0, 0), Quaternion.identity);
        _spawn++;
    }

    public void SpawnLoopEffect()
    {
        string id = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        _effectSpawner.RequestPlayLoopEffect(id, EffectType.Test, new Vector3(0 + _spawn * 2, 0, 0), Quaternion.identity);
        _spawn++;
        idList.Add(id);
    }

    public void StopSpawnEffect(string effectId)
    {
        _effectSpawner.StopEffect(effectId);
        idList.Remove(effectId);
        Debug.Log($"'{effectId}' を削除");
    }
}
