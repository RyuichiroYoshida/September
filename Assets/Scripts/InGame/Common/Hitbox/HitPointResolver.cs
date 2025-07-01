using System.Collections.Generic;
using UnityEngine;

public class HitPointResolver : MonoBehaviour
{
    [SerializeField] private List<Transform> _hitPoints = new();

    public List<Transform> GetPoints() => _hitPoints;
}