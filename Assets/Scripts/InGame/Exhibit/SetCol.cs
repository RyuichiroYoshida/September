using DebugPlayer;
using UnityEngine;

namespace September.InGame
{
    public class SetCol : MonoBehaviour
    {
        [SerializeField] private FlightController _flightController;
        [SerializeField] private Move _move;

        private void OnTriggerEnter(Collider other)
        {
            _move = other.GetComponent<Move>();
            if (_move != null)
            {
                Debug.Log("Moveオブジェクトが範囲に入りました！");
                // 必要に応じてFlightControllerに通知
                _flightController.Set(true); 
            }
        }

        private void OnTriggerExit(Collider other)
        {
            Move move = other.GetComponent<Move>();
            if (move != null)
            {
                _flightController.Set(false);
                Debug.Log("Moveオブジェクトが範囲から出ました！");
            }
        }
    }
}