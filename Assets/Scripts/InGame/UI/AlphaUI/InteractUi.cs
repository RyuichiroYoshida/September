using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Interact;
using UnityEngine;
using UnityEngine.UI;

namespace September.InGame.UI
{
    /// <summary>
    /// インタラクトUIの管理クラス
    /// </summary>
    public class InteractUi : MonoBehaviour
    {
        enum ConnectionState
        {
            Local,
            Remote,
        }
        
        [SerializeField] private Image _interactFillImage; // インタラクトの進行状況を示すUIのイメージ
        [SerializeField] private RectTransform _root;
        [SerializeField] private RectTransform _rootParentRectTransform;
        private readonly ConnectionState _connectionState = ConnectionState.Remote;
        private InteractableBase _targetObject;
        CancellationTokenSource _cts;
        void OnEnable()
        {
            _cts = new CancellationTokenSource();
            FollowEndOfFrame(_cts.Token).Forget();
        }
        void OnDisable()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        async UniTaskVoid FollowEndOfFrame(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
                UpdateUiPosition();
            }
        }

        public void SetActive(bool isShow, InteractableBase target = null)
        {
            if (target)
            {
                _targetObject = target;
            }
            // インタラクトUIの表示/非表示を切り替えるメソッド
            if (_root)
            {
                _root.gameObject.SetActive(isShow);
            }
        }
        
        public void SetInteractProgress(float progress)
        {
            if (_interactFillImage)
            {
                _interactFillImage.fillAmount = Mathf.Clamp01(progress);
            }
        }

        private void UpdateUiPosition()
        {
            if (!_root || !_targetObject) return;

            var container = _root.parent as RectTransform;   // = InteractUI
            if (!container) return;

            var worldCam = Camera.main; // ← ここ超重要：実カメラ
            if (!worldCam) return;

            // ★ 頭上に出したいなら、ここでオフセット or バウンズ上端に差し替え
            Vector3 world = _targetObject.GetNearestPointOnInteractArea(transform.position); // or GetHeadTop(_target)

            // World -> Screen
            Vector3 sp = worldCam.WorldToScreenPoint(world);
            if (sp.z < 0f)
            {
                _root.gameObject.SetActive(false);
                return;
            } // カメラ背面なら非表示など
            if (!_root.gameObject.activeSelf) _root.gameObject.SetActive(true);

            // Screen -> Container(Local) （Overlayは cam=null）
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(container, sp, null, out var local))
            {
                _root.anchoredPosition = local; // 親pivot=0.5/子anchor=0.5なら補正不要
            }
        }
    }
}
