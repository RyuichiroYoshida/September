using System;
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
        private Camera _camera;
        private readonly ConnectionState _connectionState = ConnectionState.Remote;
        private GameObject _targetObject;

        private void Awake()
        {
            if (_connectionState == ConnectionState.Local)
            {
                // ローカル接続の初期化処理
            }
            else
            {
                _camera = Camera.main;
            }
        }
        
        public void SetActive(bool isShow, GameObject target = null)
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
            // インタラクトの進行状況を更新するメソッド
            // progressは0から1の範囲で、0が未開始、1が完了を示す
            if (_interactFillImage)
            {
                _interactFillImage.fillAmount = Mathf.Clamp01(progress);
            }
        }

        private void LateUpdate()
        {
            if (_targetObject)
            {
                CalculatePosition(_targetObject);
            }
        }

        private void CalculatePosition(GameObject targetObject)
        {
            // インタラクトUIの位置を計算するメソッド
            if (!_root || !targetObject) return;
            if (_camera)
            {
                var screenPosition = _camera.WorldToScreenPoint(targetObject.transform.position);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _root.parent as RectTransform, screenPosition, null, out var localPoint))
                {
                    _root.anchoredPosition = localPoint;
                }
            }
        }
    }
}
