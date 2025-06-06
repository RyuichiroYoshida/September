using UnityEngine;
using Cysharp.Threading.Tasks;

namespace CRISound
{
    /// <summary>3Dサウンド再生用クラス</summary>
    public class DebugPlaySound : MonoBehaviour
    {
        [Header("サウンド担当が更新する場所")] 
        [SerializeField] private string _cueSheetName;
        [SerializeField] private string _cueName;

        [Header("Debug用チェックリスト")] 
        public bool _isPlay;
        
        private CuePlayAtomExPlayer.SoundPlayer _soundPlayer;
        private bool _isPlaying;
        
        private void Start()
        {
            Initialize();
        }

        private async void Update()
        {
            if (_isPlay && !_isPlaying)
            {
                await Play(gameObject.transform.position,_cueSheetName, _cueName);
            }
        }

        private void Initialize() { }

        private async UniTask Play(Vector3 position, string cueSheet, string cueName)
        {
            _isPlaying = true;
            // 3D再生
            var playback = CuePlayAtomExPlayer.SE.Play3D(position, cueSheet, cueName);
            // 再生が終了するまで待機
            await UniTask.WaitUntil(() => !playback.IsBusy);
            _isPlaying = false;
        }
    }
}
