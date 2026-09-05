using Fusion;
using September.Common;
using Unity.Cinemachine;
using UnityEngine;

public class AimCameraController : NetworkBehaviour
{
    [Header("通常のカメラ")]
    [SerializeField] private CinemachineVirtualCamera _normalCamera;
    [Header("AIM用のカメラ")]
    [SerializeField] private CinemachineVirtualCamera _aimCamera;
    
    [Header("CrosshairPrefab(照準のUI)")]
    [SerializeField] private GameObject _crosshairPrefab;
    private GameObject _crosshair;

    /// <summary> 照準の起点 (カメラ位置)。入力から毎ティック更新されるためホスト・クライアント双方で参照できる </summary>
    [Networked]public Vector3 AimOrigin { get; private set; }
    /// <summary> 照準の向き (カメラ前方) </summary>
    [Networked]public Vector3 AimDirection { get; private set; }

    /// <summary>
    /// true：構えている状態　false：構えていない状態
    /// </summary>
    public bool IsAim { get; private set; }

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            _crosshair = Instantiate(_crosshairPrefab);
            _crosshair.SetActive(false);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput<PlayerInput>(out var input)) return;

        // 入力に載ってきたカメラ姿勢をそのまま採用する。
        // 以前は Camera.main を毎ティック RPC で送っていたため、ホストへの到着が 1 ティック遅れる上に
        // CinemachineBrain 更新前 (1 フレーム前) の姿勢しか取れなかった。
        AimOrigin = input.CameraPosition;
        AimDirection = input.DesiredLookDirection;

        if (IsAim && HasInputAuthority)
        {
            var camForward = AimDirection;
            camForward.y = 0;
            transform.forward = camForward;
        }
    }

    /// <summary>
    /// 通常カメラに変更する
    /// ハタノAbilityが終了、AIM入力が辞めたとき
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_NormalCamera()
    {
        IsAim = false;
        _normalCamera.gameObject.SetActive(true);
        _aimCamera.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// AIMカメラに変更する
    /// ハタノAbilityを発動したとき
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_AimCamera()
    {
        IsAim = true;
        if (HasInputAuthority)
        {
            _normalCamera.gameObject.SetActive(false);
            _aimCamera.gameObject.SetActive(true);
        }
        var camForward = AimDirection;
        camForward.y = 0;
        gameObject.transform.forward = camForward;
    }

    /// <summary>
    /// 照準の表示を行う
    /// <param name="isFlag">true：表示　false：非表示</param>>
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_CrosshairToggleChange(bool isFlag)
    {
        if(!HasInputAuthority) return;
        if(_crosshair == null)
        {
            Debug.LogWarning("Crosshairが生成されてない");
            return;
        }
        _crosshair.SetActive(isFlag);
    }
}
