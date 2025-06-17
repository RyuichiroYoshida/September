using System;
using Cysharp.Threading.Tasks;
using September.Common;
using UnityEngine;

public class ResultManager : MonoBehaviour
{
    [SerializeField] private int _reslutTime;
    private async void Start()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(_reslutTime));
        NetworkManager.Instance.QuitLobby();
    }
}
