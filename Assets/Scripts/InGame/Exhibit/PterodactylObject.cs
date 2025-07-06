using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PterodactylObject : MonoBehaviour
{
    [Header("Flight Settings")] 
    public float _gravity = 9.8f;
    // 加速度
    public float _jerk = 1.5f;
    public float _maxSpeed = 10f;
    public float _takeOffSpeed = 3f;
    // 上下
    private float _pitchSpeed = 60f;
    // 傾き
    public float _rollSpeed = 80f;
    
    public float _moveSpeed = 5f;

    public float _accel;
    public Vector3 _velocity;
    // ロールに応じた旋回力
    public float _turnSpeed = 2f;
    
    // 最大ロール角
    public float _tiltAmount = 30f;
    public float _tiltLerpSpeed = 10f;

    private bool _isGrounded;
    private bool _isPlaying;
    // 現在のロール角
    private float _currentRoll = 0f;
    private Vector2  _moveInput;

    [HideInInspector] public float DebugYawSpeed;
    [Header("Debug Info")]
    [SerializeField] private float _debugYawSpeed;
    
    private void Awake()
    {
        Initialize();
    }
    
    private void Update()
    {
        if (!_isPlaying)
        {
            if(Input.GetMouseButtonDown(0))
                _isPlaying = true;
            return;
        }
        
        Move();
    }

    private void Initialize()
    {
        _isPlaying = false;
    }

    // 移動制御
    public void OnMove(InputValue value)
    {
        _moveInput = value.Get<Vector2>();
    }

    private void Move()
    {
        // 前進
        if (_moveInput.y > 0.1f)
        {
            transform.position += transform.forward * (_moveSpeed * Time.deltaTime);
        }
        
        // 傾き
        float targetRoll = _moveInput.x * _tiltAmount;
        _currentRoll = Mathf.Lerp(_currentRoll,targetRoll,Time.deltaTime * _tiltLerpSpeed);
        
        // Yaw補正(前進中)
        float yawAngle = 0f;
        if (_moveInput.y > 0.1f)
        {
            float yawFromRoll = _currentRoll / _tiltAmount;
            yawAngle = yawFromRoll * _turnSpeed * Time.deltaTime;
            _debugYawSpeed = yawAngle;
        }
        
        // YawとRollのQuaternion作成
        Quaternion yaw = Quaternion.AngleAxis(yawAngle, Vector3.up);
        Quaternion roll = Quaternion.AngleAxis(_currentRoll, Vector3.forward);
        
        // 回転を一括で合成して反映
        transform.rotation = yaw * transform.rotation;
        transform.rotation =
            Quaternion.Slerp(transform.rotation, transform.rotation * roll, Time.deltaTime * _tiltLerpSpeed);
    }

    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) 
            return;

        // 現在の前方向
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);

        // ロール角の表示
        Handles.Label(transform.position + Vector3.up * 1.5f,
            $"Roll: {_currentRoll:F1}°");

        // Yaw速度の表示
        Handles.Label(transform.position + Vector3.up * 2.0f,
            $"Yaw Speed: {DebugYawSpeed:F3} deg/frame");

        // 入力ベクトルの表示
        Gizmos.color = Color.cyan;
        Vector3 inputVec = new Vector3(_moveInput.x, 0f, _moveInput.y);
        Gizmos.DrawRay(transform.position, transform.TransformDirection(inputVec));
    }
    #endif

    // 接地判定
    private void OnCollisionStay(Collision collision)
    {
        _isGrounded = true;
    }

    private void OnCollisionExit(Collision collision)
    {
        _isGrounded = false;
    }
    
    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 300, 20), $"Roll: {_currentRoll:F1}°");
        GUI.Label(new Rect(10, 30, 300, 20), $"Yaw Speed: {_debugYawSpeed:F3}");
    }
}
