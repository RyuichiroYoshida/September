using System.Collections.Generic;
using UnityEngine;

public class HitPointResolver : MonoBehaviour
{
    [SerializeField] private List<Transform> _hitPoints = new();
    [SerializeField] private int _startFrame = 0;
    [SerializeField] private int _endFrame = int.MaxValue;
    [SerializeField] private float _radius = 0.1f;

    [SerializeField] private bool _autoFind = false;
    [SerializeField] private List<string> _autoFindNameParts = new();

    /// <summary>
    /// AutoFindが有効な場合、指定された名前の一部を持つ子Transformを自動的に検索します。
    /// 重そうならEditorでの設定にして、実行時は無効にすることも検討
    /// </summary>
    private void Start()
    {
        if (_autoFind)
        {
            _hitPoints.Clear();

            var allTransforms = GetComponentsInChildren<Transform>(includeInactive: true);

            foreach (var namePart in _autoFindNameParts)
            {
                foreach (var t in allTransforms)
                {
                    if (t.name.Contains(namePart) && t != this.transform) // exclude self
                    {
                        _hitPoints.Add(t);
                    }
                }
            }

            if (_hitPoints.Count == 0)
            {
                Debug.LogWarning($"[HitPointResolver] No matching transforms found under '{transform.name}' with parts: {string.Join(", ", _autoFindNameParts)}");
            }
        }
    }
    public List<Transform> GetPoints() => _hitPoints;
    public int GetStartFrame() => _startFrame;
    public int GetEndFrame() => _endFrame;
    public float GetRadius() => _radius;
}