using September.Common;
using UnityEngine;

namespace InGame.Player
{
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] float _moveSpeed = 5f;

        private Rigidbody _rb;
        Camera _mainCamera;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            // inputそのままの速度
            Vector2 inputVec = InputProvider.Instance.GetPlayerInput.Player.Move.ReadValue<Vector2>();
            Vector3 inputCameraDir = _mainCamera.transform.TransformDirection(new Vector3(inputVec.x, 0f, inputVec.y));
            Vector3 velocity = new Vector3(inputCameraDir.x, 0, inputCameraDir.z) * _moveSpeed;
            velocity.y = _rb.linearVelocity.y;
            _rb.linearVelocity = velocity;
        
            // 進行を向く
            if (inputVec != Vector2.zero)
            {
                transform.localRotation = Quaternion.LookRotation(new Vector3(inputCameraDir.x, 0, inputCameraDir.z));  
            }
        }
    }
}
