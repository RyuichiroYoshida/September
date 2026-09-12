using Cinemachine;
using Fusion;
using UnityEngine;

public class AimCameraController : NetworkBehaviour
{
    [Header("通常のカメラ"), SerializeField] private CinemachineVirtualCamera _normalCamera;
    [Header("AIM用のカメラ"), SerializeField] private CinemachineVirtualCamera _aimCamera;
    [Header("ULT用のカメラ"), SerializeField] private CinemachineVirtualCamera _ultCamera;
    [Header("CrosshairPrefab(照準のUI)")]
    [SerializeField] private GameObject _crosshairPrefab;
    [Header("回転のスムーズさ"), SerializeField] private float _rotationSpeed = 15f;
    private GameObject _crosshair;
    public Camera MainCamera { get; private set; }
    
    [Networked]public Vector3 AimOrigin { get; private set; }
    [Networked]public Vector3 AimDirection { get; private set; }

    /// <summary>
    /// true：構えている状態　false：構えていない状態
    /// </summary>
    public bool IsAim { get; private set; }

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            MainCamera = Camera.main;
            _crosshair = Instantiate(_crosshairPrefab);
            _crosshair.SetActive(false);
        }
    }
    
    public override void FixedUpdateNetwork()
    {
        if(!HasInputAuthority || MainCamera == null) return;
        
        if (IsAim)
        {
            AimOrigin = MainCamera.transform.position;
            AimDirection = MainCamera.transform.forward;
            
            var camForward = MainCamera.transform.forward;
            camForward.y = 0;

            // 構え中の前後左右移動時に発生するカクつきを軽減するため、回転を補間させる
            if (camForward.sqrMagnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(camForward.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Runner.DeltaTime * _rotationSpeed);
            }
        }
        RPC_SetAim(MainCamera.transform.position, MainCamera.transform.forward);
    }

    /// <summary>
    /// カメラの位置等をクライアントから送信しホスト側で変更
    /// </summary>
    /// <param name="aimOrigin">カメラ場所</param>
    /// <param name="aimDirection">カメラのForward</param>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetAim(Vector3 aimOrigin, Vector3 aimDirection)
    {
        AimOrigin = aimOrigin;
        AimDirection = aimDirection;
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
        _ultCamera.gameObject.SetActive(false);
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
            _ultCamera.gameObject.SetActive(false);
        }
        var camForward = AimDirection;
        camForward.y = 0;
        gameObject.transform.forward = camForward;
    }

    /// <summary>
    /// ULTカメラに変更する
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ULTCamera()
    {
        IsAim = true;
        if (HasInputAuthority)
        {
            _normalCamera.gameObject.SetActive(false);
            _aimCamera.gameObject.SetActive(false);
            _ultCamera.gameObject.SetActive(true);
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
