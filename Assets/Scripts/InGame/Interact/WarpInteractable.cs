using System;
using System.Threading;
using CRISound;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Interact;
using InGame.Player;
using NaughtyAttributes;
using September.Common;
using September.InGame.Effect;
using UnityEngine;

public class WarpInteractable : InteractableBase
{
    [SerializeField, Label("ワープ先（Goal）")] private WarpObject _warpDestination;
    [SerializeField, Label("ワープ先の向き")] private float _warpRotation = 180f;
    [SerializeField,Label("Duration")] private float _warpDuration = 0.5f;
    
    private WarpObject _warpObject;
    private WarpInteractable _warpDestinationInteractable;
    private CancellationTokenSource _cts;
    private EffectSpawner _effectSpawner;

    private void Start()
    {
        _warpObject = GetComponent<WarpObject>();
        _warpDestinationInteractable = _warpDestination.GetComponent<WarpInteractable>();
        _cts = new CancellationTokenSource();
        _effectSpawner = GameObject.FindFirstObjectByType<EffectSpawner>();
    }

    protected override bool OnValidateInteraction(IInteractableContext context, CharacterType charaType)
    {
        return !_warpDestinationInteractable.IsInCooldown();
    }

    protected override void OnInteract(IInteractableContext context)
    {
        if (!Runner)
        {
            Debug.LogError("Runner is null");
            return;
        }

        if (!_warpDestination)
        {
            Debug.LogError("Warp先（_warpDestination）が設定されていません");
            return;
        }

        PlayerRef playerRef = PlayerRef.FromEncoded(context.Interactor);
        NetworkObject player = Runner.GetPlayerObject(playerRef);

        if (!player)
        {
            Debug.LogError("Player NetworkObject が見つかりません");
            return;
        }
        
        // プレイヤーをワープ
        HandleWarpAsync(player).Forget();
    }

    private async UniTaskVoid HandleWarpAsync(NetworkObject player)
    {
        // エフェクト再生
        ParticleSystem startEffect = _warpObject.GetWarpEffect();
        Vector3 effectPos = player.transform.position + Vector3.up * 1.0f;
        startEffect.transform.position = effectPos;
        _effectSpawner.RequestPlayOneShotEffect(EffectType.Warp, effectPos, Quaternion.identity);
        CRIAudio.PlaySE("Warp", _warpDestination.SoundName());

        // Playerを透明化
        SetPlayerVisible(player, false);
        Vector3 targetPos = _warpDestination.GetWarpPosition();
        Vector3 backward = _warpDestination.transform.forward;
        Quaternion targetRot = Quaternion.LookRotation(backward, Vector3.up);

        // Network経由で移動を指示
        PlayerManager playerManager = player.GetComponent<PlayerManager>();
        CameraController cam = player.GetComponent<CameraController>();
        if (playerManager != null)
        {
            playerManager.SetWarpTarget(targetPos,targetRot);
        }
        
        // 少し待ってから移動予約
        await UniTask.Delay(TimeSpan.FromSeconds(_warpDuration));

        // ゴール側エフェクト再生
        ParticleSystem goalEffect = _warpDestination.GetWarpEffect();
        goalEffect.transform.position = targetPos;
        _effectSpawner.RequestPlayOneShotEffect(EffectType.Warp, targetPos, Quaternion.identity);
        // 表示とSE
        SetPlayerVisible(player, true);
        CRIAudio.PlaySE("Warp", _warpDestination.SoundName());
    }

    private void SetPlayerVisible(NetworkObject player, bool isVisible)
    {
        // Playerクラスのどこかから拾ってくる
        foreach (var renderer in player.GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = isVisible;
        }
    }

    private void OnDestroy()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}