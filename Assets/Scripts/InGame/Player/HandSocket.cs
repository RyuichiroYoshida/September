using System.Collections.Generic;
using UnityEngine;

namespace InGame.Player
{
    public class HandSocket : MonoBehaviour
    {
        [SerializeField] private bool _autoFind = true;

        [Tooltip("部分一致でマッチするTransform名（例: \"R_Hand\", \"Palm\" など）")]
        [SerializeField] private List<string> _namePartsToMatch = new();

        [Tooltip("自動取得されたSocketのTransform一覧")]
        [SerializeField] private List<Transform> _sockets = new();

        public List<Transform> Sockets => _sockets;

        private void Awake()
        {
            if (_autoFind)
            {
                AutoFindSockets();
            }
        }

        private void AutoFindSockets()
        {
            _sockets.Clear();

            var allChildren = GetComponentsInChildren<Transform>(includeInactive: true);
            foreach (var t in allChildren)
            {
                if (t == this.transform) continue; // 自分自身は除外

                foreach (var keyword in _namePartsToMatch)
                {
                    if (!string.IsNullOrEmpty(keyword) && t.name.Contains(keyword))
                    {
                        _sockets.Add(t);
                        break; // 一度でもマッチすれば追加
                    }
                }
            }

            if (_sockets.Count == 0)
            {
                Debug.LogWarning($"[HandSocket] No matching Transforms found on '{name}' using keywords: {string.Join(", ", _namePartsToMatch)}");
            }
        }
    }
}
