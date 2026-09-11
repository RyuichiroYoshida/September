using Fusion;
using September.Common;
using UnityEngine;

/// <summary>
/// サメの移動処理
/// </summary>
public class SharkMovementProcessing : NetworkBehaviour
{
    [Header("移動設定")]
    [SerializeField] private float _walkSpeed;
    [SerializeField] private float _dashSpeed;
    [SerializeField] private AnimationCurve _speedCurve;
    [SerializeField] private Vector3 _fallGravity;
    [SerializeField] private float _maxRotateValue;

    [Header("地面判定")]
    [SerializeField] private LayerMask _groundLayerMask;
    [SerializeField] private Vector3 _groundHalfExtents = new(0.5f, 0.1f, 0.6f);
    [SerializeField] private Vector3 _groundRayOffset = new(0, 0.5f, 0.875f);
    [SerializeField] private float _groundRayDistance = 2.0f;
    [SerializeField] private float _groundMaximumAngle = 90f;
    [SerializeField] private float _groundAdsorptionSpeed = 10f;

    [Header("地面浮遊")]
    [SerializeField] private float _heightAboveGround = 0.5f;
    [SerializeField] private float _floatingSpeed = 1f;

    [Header("正面衝突判定")]
    [SerializeField] float _forwardRayDistance = 1;
    [SerializeField] Vector3 _forwardRayOffset = new(0, 0.5f, 0);
    [SerializeField] LayerMask _wallLayerMask;
    [SerializeField, Range(0, 90)] float _wallAngle = 90;

    [Header("空中正面衝突判定")]
    [SerializeField] Vector3 _halfExtents = Vector3.one * 0.5f;
    [SerializeField] Vector3 _forwardRayOffsetAir = new(0, 0.5f, 0);

    /// <summary>
    /// 海に落ちる直前の位置
    /// </summary>
    public Vector3 PositionBeforeWaterFall { get; private set; }

    public float CurrentSpeedRatio { get; private set; }

    private Vector3 _currentGroundNormal; // 現在、接触している地面の法線
    private float _currentGroundHeight; // 接触している地面との距離
    private bool _isGrounded; // プレイヤーが地面に接地しているか

    /// <summary>
    /// 壁に当たらずに移動し続けている時間
    /// </summary>
    [Networked] private float KeepMovingTime { get; set; }

    [Networked] private float LastGroundedTime { get; set; }

    private Vector3 FallVelocity => !_isGrounded ? _fallGravity * (Runner.SimulationTime - LastGroundedTime) : Vector3.zero;

    /// <summary>
    /// 移動処理
    /// </summary>
    /// <param name="playerInput">プレイヤーの入力</param>
    /// <param name="deltaTime">フレーム時間</param>
    /// <param name="rb">プレイヤーのRigidbody</param>
    /// <param name="forward">正面方向</param>
    public void UpdateMovement(PlayerInput playerInput, float deltaTime, Rigidbody rb, Vector3 forward)
    {
        CheckGroundManual(rb);
        // 渡されたベクトルをxz平面に射影
        var moveDirection = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        Move(moveDirection, playerInput, rb, deltaTime);
        Rotate(deltaTime, moveDirection);
        AdsorptionOnGround(deltaTime, rb);
    }

    /// <summary>
    /// BoxCastで地面の法線を取得する
    /// </summary>
    /// <param name="rb">プレイヤーのRigidbody</param>
    private void CheckGroundManual(Rigidbody rb)
    {
        var ray = new Ray(rb.position + rb.rotation * _groundRayOffset, Vector3.down);

        if (Physics.BoxCast(ray.origin, _groundHalfExtents, ray.direction, out var hit, rb.rotation, _groundRayDistance, _groundLayerMask)
            && Vector3.Angle(hit.normal, Vector3.up) < _groundMaximumAngle)
        {
            _isGrounded = true;
            _currentGroundNormal = hit.normal; // 最初に見つかった地面の法線を保存
            _currentGroundHeight = transform.position.y - hit.point.y; // 地面との距離を保存
            PositionBeforeWaterFall = hit.point; // 最後に接していた地面の位置を保存
            return;
        }

        // 地面から離れた瞬間に、最後の接地時間を保存
        if (_isGrounded)
        {
            LastGroundedTime = Runner.SimulationTime;
        }

        // 地面が見つからなかった
        _isGrounded = false;
        _currentGroundNormal = Vector3.up;
    }

    /// <summary>
    /// サメの移動処理
    /// </summary>
    /// <param name="moveDirection">プレイヤーの移動方向</param>
    /// <param name="playerInput">プレイヤーの入力</param>
    /// <param name="rb">プレイヤーのRigidbody</param>
    private void Move(Vector3 moveDirection, PlayerInput playerInput, Rigidbody rb, float deltaTime)
    {
        if (moveDirection == Vector3.zero) return;

        var moveVelocity = Vector3.ProjectOnPlane(moveDirection, _currentGroundNormal);　//坂に沿った動きに
        if (moveVelocity.y < 0) moveVelocity.y = 0; // 下りの場合は無視
        moveVelocity = moveVelocity.normalized;

        Debug.DrawRay(transform.position, _currentGroundNormal * 2, Color.yellow);
        Debug.DrawRay(transform.position, moveVelocity * 5f, Color.cyan);

        // 前方に壁があるか判定
        var rot = Quaternion.LookRotation(moveDirection);

        bool isHitWall = false;
        {
            if (_isGrounded)
            {
                var ray = new Ray(transform.position + rot * _forwardRayOffset, moveDirection);

                Debug.DrawRay(ray.origin, ray.direction * _forwardRayDistance, Color.yellow);

                // Rayを飛ばす
                if (Physics.Raycast(ray, out var hit, _forwardRayDistance, _wallLayerMask)
                    && Vector3.Dot(hit.normal, Vector3.up) <= Mathf.Cos(_wallAngle * Mathf.Deg2Rad))
                {
                    isHitWall = true;
                }
            }
            else
            {
                var ray = new Ray(transform.position + rot * _forwardRayOffsetAir, moveDirection);

                DebugDrawUtility.DrawOrientedWireBox(ray.origin + ray.direction * _forwardRayDistance, _halfExtents, transform.rotation, Color.yellow);
                Debug.DrawRay(ray.origin, ray.direction * _forwardRayDistance, Color.yellow);

                if (Physics.BoxCast(ray.origin, _halfExtents, ray.direction, out var hit, transform.rotation, _forwardRayDistance, _wallLayerMask)
                    && Vector3.Dot(hit.normal, Vector3.up) <= Mathf.Cos(_wallAngle * Mathf.Deg2Rad))
                {
                    isHitWall = true;
                }
            }
        }

        if (isHitWall)
        {
            // 坂などは判定しないように内積で壁判定
            KeepMovingTime = 0;
        }
        else
        {
            KeepMovingTime += deltaTime;
        }

        // アニメーションカーブで速度取得
        float t = _speedCurve.Evaluate(KeepMovingTime);
        float baseSpeed = playerInput.Buttons.IsSet(PlayerButtons.Dash) ? _dashSpeed : _walkSpeed;
        float speed = baseSpeed * t;

        rb.linearVelocity = moveVelocity * speed + FallVelocity;

        CurrentSpeedRatio = speed / _dashSpeed;
    }

    /// <summary>
    /// 移動方向へ滑らかに回転をする
    /// </summary>
    /// <param name="deltaTime"></param>
    /// <param name="moveDirection">プレイヤーの移動方向</param>
    private void Rotate(float deltaTime, Vector3 moveDirection)
    {
        if (moveDirection == Vector3.zero) return;
        var rot = Quaternion.LookRotation(moveDirection);
        var endRot = Quaternion.RotateTowards(transform.rotation, rot, _maxRotateValue * deltaTime);
        transform.rotation = endRot;
    }

    /// <summary>
    /// 地面へと吸着
    /// </summary>
    /// <param name="deltaTime"></param>
    /// <param name="rb">プレイヤーのRigidbody</param>
    private void AdsorptionOnGround(float deltaTime, Rigidbody rb)
    {
        if (_isGrounded)
        {
            var diff = _heightAboveGround - _currentGroundHeight;

            if (diff > 0)
            {
                rb.linearVelocity += Vector3.up * _floatingSpeed;
            }

            return;
        }

        var ray = Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit,
            1.5f, _groundLayerMask);
        if (ray && hit.distance > 0)
        {
            var targetPos = new Vector3(transform.position.x, hit.point.y, transform.position.z);
            rb.MovePosition(Vector3.Lerp(transform.position, targetPos, deltaTime * _groundAdsorptionSpeed));
        }
    }

    public void OnInteractStart()
    {
        ResetMovementState();
        LastGroundedTime = Runner.SimulationTime;
        PositionBeforeWaterFall = transform.position;
    }

    /// <summary>
    /// インタラクト終了時の移動状態リセット
    /// </summary>
    /// <param name="rb">サメのRigidbody</param>
    public void OnInteractEnd(Rigidbody rb)
    {
        ResetMovementState();

        if (rb == null || rb.isKinematic) return;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// 内部状態を初期値へ戻し、何回インタラクトしても同じ効果が得られるようにする
    /// </summary>
    private void ResetMovementState()
    {
        KeepMovingTime = 0;
        CurrentSpeedRatio = 0;
        _isGrounded = false;
        _currentGroundNormal = Vector3.up;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Matrix4x4 prevMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(_groundRayOffset, _groundHalfExtents * 2f);
        Gizmos.DrawWireCube(_groundRayOffset + Vector3.down * _groundRayDistance, _groundHalfExtents * 2f);
        Gizmos.DrawLine(_groundRayOffset, _groundRayOffset + Vector3.down * _groundRayDistance);

        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawRay(_forwardRayOffsetAir, Vector3.forward * _forwardRayDistance);
        Gizmos.DrawWireCube(_forwardRayOffsetAir + Vector3.forward * _forwardRayDistance, _halfExtents * 2f);
        Gizmos.matrix = prevMatrix;

        // 前方に壁があるか判定
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position + transform.rotation * _forwardRayOffset, transform.forward * _forwardRayDistance);


        // 地面から浮かせる距離
        Gizmos.color = Color.cyan;
        GizmosUtility.DrawHorizontalCross(transform.position, 1f);
        Gizmos.DrawRay(transform.position, Vector3.down * _heightAboveGround);
        GizmosUtility.DrawHorizontalCross(transform.position + Vector3.down * _heightAboveGround, 1f);
    }
}
