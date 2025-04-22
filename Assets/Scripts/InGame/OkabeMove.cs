using Cinemachine;
using September.InGame;
using UnityEngine;
using NaughtyAttributes;

public class OkabeMove : MonoBehaviour
{
    Rigidbody _rigidbody;
    Animator _animator;
    [SerializeField,Label("カメラ設定")] private GameObject _lockAtTarget;
    [SerializeField] private float _speed = 0;
    private CinemachineFreeLook _freeLook;

    private FlightController _flightController;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _flightController = FindObjectOfType<FlightController>();
        _flightController.FindOkabeMove(this);
        _freeLook = FindObjectOfType<CinemachineFreeLook>();
    }

    private void FixedUpdate()
    {
        Moving();
    }

    public void HidePlayer()
    {
        gameObject.SetActive(false);
    }

    public void AppearPlayer(Transform appearTransform)
    {
        _freeLook.Follow = _lockAtTarget.transform;
        _freeLook.LookAt =_lockAtTarget.transform;
        gameObject.transform.position = appearTransform.position;
        gameObject.SetActive(true);
    }

    void Moving()
    {
        var velo = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")).normalized;
        Vector3 cameraForward = Camera.main.transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();
        Vector3 cameraRight = Camera.main.transform.right;
        cameraRight.y = 0;
        cameraRight.Normalize();
        Vector3 moveDirection = cameraForward * velo.z + cameraRight * velo.x;
        _rigidbody.linearVelocity = moveDirection * _speed;

        if (velo.magnitude > 0)
        {
            var rot = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(moveDirection), 10f);
            transform.rotation = rot;
        }

        _animator.SetFloat("Speed", _rigidbody.linearVelocity.magnitude);
    }

    public void OnFootstep()
    {
        //animationEvent用のメソッド
    }
}