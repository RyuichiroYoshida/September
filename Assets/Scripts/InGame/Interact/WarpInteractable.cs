using CRISound;
using Fusion;
using InGame.Interact;
using NaughtyAttributes;
using September.Common;
using UnityEngine;
using DG.Tweening;

public class WarpInteractable : InteractableBase
{
    [SerializeField, Label("ワープ先（Goal）")] private WarpObject _warpDestination;
    [SerializeField, Label("ワープ先の向き")] private float _warpRotation;
    
    private WarpObject _warpObject;

    private void Start()
    {
        _warpObject = GetComponent<WarpObject>();
    }

    protected override bool OnValidateInteraction(IInteractableContext context, CharacterType charaType)
    {
        return true;
    }

    protected override void OnInteract(IInteractableContext context)
    {
        if (Runner == null)
        {
            Debug.LogError("Runner is null");
            return;
        }

        if (_warpDestination == null)
        {
            Debug.LogError("Warp先（_warpDestination）が設定されていません");
            return;
        }

        PlayerRef playerRef = PlayerRef.FromEncoded(context.Interactor);
        NetworkObject player = Runner.GetPlayerObject(playerRef);

        if (player == null)
        {
            Debug.LogError("Player NetworkObject が見つかりません");
            return;
        }
        
        // プレイヤーをワープ

        InteractAnimation();
        player.transform.DOMove(_warpDestination.GetWarpPosition(),0.5f);
        //player.transform.position = _warpDestination.GetWarpPosition();
        // カメラの向きを絵画に背を向けた状態にしたい
        _warpDestination.GetWarpEffect().Play();
        CRIAudio.PlaySE("Warp",_warpDestination.SoundName());
    }

    private async void InteractAnimation()
    {
        CRIAudio.PlaySE("Warp",_warpObject.SoundName());
        _warpObject.GetWarpEffect().Play();
    }
}