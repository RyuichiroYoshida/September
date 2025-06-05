using System;
using September.InGame.Effect;
using UnityEngine;

public class EffectTest : MonoBehaviour
{
    
    EffectSpawner _effectSpawner;

	int _spawn = 0;
	int _id = 0;

    private void Start()
    {
        _effectSpawner = GameObject.FindObjectOfType<EffectSpawner>();
        _effectSpawner.OnEffectPlayed += OnEffectPlayed;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            SpawnEffect();
        }

        if (Input.GetKeyDown(KeyCode.O))
        {
            StopSpawnEffect(_id);
        }
    }

    void OnEffectPlayed(int id)
    {
        Debug.Log($"再生されたエフェクトID: {id}");
        _id = id;
    }
    
    public void SpawnEffect()
    {
		if(_spawn % 2 == 0)
		{
            _effectSpawner.RequestPlayEffect(EffectType.Test1, new Vector3(0 + _spawn * 2,0.9f,0), new Quaternion(0,0,0,0));
		}
        else
        {
            _effectSpawner.RequestPlayEffect(EffectType.Test2, new Vector3(0 + _spawn * 2,0.9f,0), new Quaternion(0,0,0,0));
        }
        
        Debug.Log($"'{_id}' を生成");

        _spawn++;

    }

    public void StopSpawnEffect(int i)
    {
        _effectSpawner.StopEffect(i);
        Debug.Log($"'{i}' を削除");
    }
}
