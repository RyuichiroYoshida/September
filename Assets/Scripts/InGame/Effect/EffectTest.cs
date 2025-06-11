using System;
using System.Threading.Tasks;
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



    public void SpawnEffect()
    {
        if (_spawn % 2 == 0)
        {
            _effectSpawner.RequestPlayEffect(EffectType.Test1, new Vector3(0 + _id * 2, 0.9f, 0),
                new Quaternion(0, 0, 0, 0),
                (effectId) =>
                {
                    Debug.Log($"エフェクトが生成されました。ID: {effectId}");
                    _id = effectId;
                });
        }
        else
        {
            _effectSpawner.RequestPlayEffect(EffectType.Test2, new Vector3(0 + _id * 2, 0.9f, 0),
                new Quaternion(0, 0, 0, 0),
                (effectId) =>
                {
                    Debug.Log($"エフェクトが生成されました。ID: {effectId}");
                    _id = effectId;
                });
        }

        _spawn++;

    }

    public void StopSpawnEffect(int i)
    {
        _effectSpawner.StopEffect(i);
        Debug.Log($"'{i}' を削除");
    }
}
