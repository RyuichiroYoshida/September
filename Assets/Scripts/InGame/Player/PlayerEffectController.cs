using System;
using Fusion;
using September.Common;
using September.InGame.Effect;
using UniRx;
using UnityEngine;

public class PlayerEffectController : NetworkBehaviour
{
    [SerializeField] private ParticleSystem _punchEffect;
    [SerializeField] private Vector3 _stunEffectPositionOffset;
    private EffectID _generateStunEffectId;
    private IDisposable _punchFollow;

    public void PlayPunchEffect()
    {
        if (!HasStateAuthority) return; 
        PlayPunchEffectLocal();
        RPC_PlayPunchEffect();
    }

    // 全クライアントで実行されるRPC（送信者にも届く）
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayPunchEffect()
    {
        PlayPunchEffectLocal();
    }

    // ここに実際の見た目処理を集約（従来の中身）
    private void PlayPunchEffectLocal()
    {
        if (_punchEffect)
        {
            _punchFollow?.Dispose();
            _punchEffect.Play();

            _punchFollow = Observable.EveryLateUpdate()
                .TakeWhile(_ => _punchEffect && _punchEffect.IsAlive(true) && _punchEffect.isPlaying)
                .Subscribe(_ =>
                {
                    var baseRot = Quaternion.LookRotation(transform.forward, Vector3.up);
                    _punchEffect.transform.rotation = baseRot;
                })
                .AddTo(this);
        }
    }

    public void PlayStunEffect()
    {
        var effectSpawner = StaticServiceLocator.Instance.Get<EffectSpawner>();
        if (effectSpawner)
        {
            _generateStunEffectId = effectSpawner.RequestPlayLoopEffect(EffectType.StunNormal,
                transform.position + _stunEffectPositionOffset, Quaternion.identity, transform);
        }
    }

    public void StopStunEffect()
    {
        var effectSpawner = StaticServiceLocator.Instance.Get<EffectSpawner>();
        if (effectSpawner && _generateStunEffectId.IsValid)
        {
            effectSpawner.StopEffect(_generateStunEffectId);
            _generateStunEffectId = default;
        }
    }
}
