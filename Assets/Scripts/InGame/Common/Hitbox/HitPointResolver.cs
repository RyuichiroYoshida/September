using System.Collections.Generic;
using UnityEngine;

public class HitPointResolver : MonoBehaviour
{
    [SerializeField] private List<Transform> _hitPoints = new();
    [SerializeField] private int _startFrame = 0;
    [SerializeField] private int _endFrame = int.MaxValue;
    [SerializeField] private float _radius = 0.1f;

    public List<Transform> GetPoints() => _hitPoints;
    public int GetStartFrame() => _startFrame;
    public int GetEndFrame() => _endFrame;
    public float GetRadius() => _radius;
}