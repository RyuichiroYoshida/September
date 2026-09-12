using UnityEngine;
using Fusion;
using InGame.Exhibit;
using InGame.Interact;
using September.Common;
using September.InGame.Common;
using September.InGame.Fields;

public class SharkInteractable : MountableExhibitBase
{
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");

    [Header("SharkMovementProcessing"), SerializeField] private SharkMovementProcessing _movementProcessing;
    [Header("攻撃のクールダウンタイム"), SerializeField] private float _cooldownTime;

    [Header("アニメーション")]
    [SerializeField] Animator _animator;
    [SerializeField] float _idleSpeedThreshold = 0.1f;

    [SerializeField] Transform _cameraTransform;

    /// <summary>
    /// インタラクション中か
    /// <para>true：インタラクション中　false：インタラクション中でない</para>
    /// </summary>
    [Networked, OnChangedRender(nameof(OnInteractingStateChanged))] public bool IsSharkInteracting { get; private set; }

    /// <summary>
    /// 攻撃中か
    /// <para>true：攻撃中　false：待機中</para>
    /// </summary>
    [Networked, OnChangedRender(nameof(OnAttackStateChanged))] private bool IsAttacking { get; set; }

    private InteractableBase _interactableBase;
    private float _cooldownTimer; // 攻撃のクールダウンタイマー
    private int _attackTickCount; // 攻撃開始時からの経過ティック数（持続時間の計算用）

    private void OnAttackStateChanged()
    {
        if (IsAttacking) _animator.SetTrigger(Attack);
    }

    private void OnInteractingStateChanged()
    {
        if (IsSharkInteracting)
        {
            _animator.enabled = true;
            return;
        }

        ResetAnimator();
    }

    /// <summary>
    /// アニメーターを待機ポーズへ戻してから停止する
    /// <para>停止するだけでは終了時点のポーズが次のインタラクトまで残り続けるため</para>
    /// </summary>
    private void ResetAnimator()
    {
        _animator.enabled = true;
        _animator.Rebind(); // ステートを既定へ、パラメータをコントローラの初期値へ戻す

        // 既定ステートがロコモーションのブレンドツリーでも待機側で評価されるよう、
        // コントローラの初期値任せにせず待機相当の値を明示する
        _animator.ResetTrigger(Attack);
        _animator.SetFloat(Speed, 0f);
        _animator.SetBool(IsMoving, false);

        _animator.Update(0f); // 待機ポーズを即時反映させてから停止する
        _animator.enabled = false;
    }

    public override void Render()
    {
        // インタラクト中以外はアニメーターを止めているため、パラメータも更新しない
        // (停止中に更新すると、次に有効化した瞬間に古い値でポーズが決まってしまう)
        if (!IsSharkInteracting) return;
        _animator.SetFloat(Speed, _movementProcessing.CurrentSpeedRatio);
        _animator.SetBool(IsMoving, _movementProcessing.CurrentSpeedRatio > _idleSpeedThreshold);
    }

    public override void Spawned()
    {
        base.Spawned();
        _interactableBase = GetComponent<InteractableBase>();
        ResetAnimator();
    }

    public override void GetOn(PlayerRef playerRef)
    {
        base.GetOn(playerRef);
        IsSharkInteracting = true;
        _interactableBase.ForceSetInteractable = false;
        // 攻撃状態の初期化
        _cooldownTimer = _cooldownTime;
        _attackTickCount = 0;
        _movementProcessing.OnInteractStart();
    }

    public override void GetOff(PlayerRef playerRef)
    {
        // baseでKinematicへ戻される前に、残留速度と速度カーブの蓄積時間をリセットする
        _movementProcessing.OnInteractEnd(Rigidbody);

        base.GetOff(playerRef);
        IsSharkInteracting = false;
        _interactableBase.ForceSetInteractable = true;

        // 攻撃状態を次のインタラクトへ持ち越さない
        IsAttacking = false;
        _attackTickCount = 0;
        _cooldownTimer = _cooldownTime;

        // インタラクトしていたプレイヤーを取得し、海に落ちる直前の位置に移動させる
        var obj = StaticServiceLocator.Instance.Get<InGameManager>()
            .PlayerDataDic[playerRef];
        obj.transform.position = _movementProcessing.PositionBeforeWaterFall;
    }

    public override void FixedUpdateNetwork()
    {
        // 解除後も移動処理が走ると、リセットした速度と蓄積時間が復活してしまう
        if (!IsSharkInteracting) return;

        if (OutOfFieldArea.I && OutOfFieldArea.I.IsOutOfField(Rigidbody.position))
        {
            GetOff(OwnerPlayerRef);
            return;
        }

        if (!GetInput<PlayerInput>(out var playerInput)) return;
        _movementProcessing.UpdateMovement(playerInput, Runner.DeltaTime, Rigidbody, playerInput.DesiredLookDirection);
    }

    public override void OnInteractFixedUpdate(PlayerInput playerInput, float deltaTime)
    {
        base.OnInteractFixedUpdate(playerInput, deltaTime);

        // ここから攻撃処理関連
        _cooldownTimer = Mathf.Min(_cooldownTime, _cooldownTimer + deltaTime);
        AttackStartTrigger(playerInput, OwnerPlayerRef);　// 攻撃開始
        OnAttackUpdate(deltaTime);　//Attack中にだけ発火するメソッド
    }

    /// <summary>
    /// 攻撃入力
    /// </summary>
    /// <param name="playerInput">プレイヤーの入力</param>
    /// <param name="playerRef">プレイヤー</param>
    private void AttackStartTrigger(PlayerInput playerInput, PlayerRef playerRef)
    {
        // 攻撃ボタンが押されていない場合、処理を行わない
        if (!playerInput.Buttons.IsSet(PlayerButtons.Attack)) return;
        AttackCoolDownCheck();
        CreateHitBox(playerRef);
    }

    /// <summary>
    /// クールダウンを確認し、攻撃開始フラグを立てる
    /// </summary>
    private void AttackCoolDownCheck()
    {
        if (_cooldownTimer < _cooldownTime) return;
        IsAttacking = true;
        _cooldownTimer = 0;
    }

    /// <summary>
    /// 攻撃アニメーションのフレームを更新
    /// </summary>
    /// <param name="deltaTime"></param>
    private void OnAttackUpdate(float deltaTime)
    {
        // 攻撃中のみ更新
        if (!IsAttacking) return;

        // フレームを進め、攻撃判定が有効なフレームだけヒット判定
        _attackTickCount++;
        if (_attackTickCount >= StartFrame && _attackTickCount <= EndFrame)
        {
            Executor?.Tick(deltaTime);
        }

        // 攻撃終了のフレームを超えたら、ヒットボックスを破棄する
        if (!(_attackTickCount >= EndFrame)) return;
        Executor?.Init();

        // 攻撃状態を初期化
        _attackTickCount = 0;
        IsAttacking = false;
        Executor = null;
    }
}
