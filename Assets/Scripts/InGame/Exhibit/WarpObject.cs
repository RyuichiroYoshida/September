using NaughtyAttributes;
using UnityEngine;


    public class WarpObject : MonoBehaviour
    {
        [SerializeField, Label("ワープポジション")] private GameObject _warpPosition;
        [SerializeField, Label("音名")] private string _soundName;
        
        public Vector3 GetWarpPosition() => _warpPosition.transform.position;
        public string SoundName() => _soundName;
    }
