using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using September.Common;
using UnityEngine;

public class ResultManager : MonoBehaviour
{
    [SerializeField] private int _reslutTime;
    private CancellationTokenSource _cts;
    private async void Start()
    {
        _cts = new CancellationTokenSource();
        await UniTask.Delay(TimeSpan.FromSeconds(_reslutTime), cancellationToken:_cts.Token);
        NetworkManager.Instance.QuitLobby();
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
