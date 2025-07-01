using System.Collections.Generic;
using UnityEngine;

public enum AttackType
{
    Punch,
    Sword
}

public class HitPointResolver : MonoBehaviour
{
    [SerializeField] private Transform punchStart;
    [SerializeField] private Transform punchEnd;
    [SerializeField] private Transform swordStart;
    [SerializeField] private Transform swordEnd;

    public List<Transform> GetPoints(AttackType type)
    {
        return type switch
        {
            AttackType.Punch => new() { punchStart, punchEnd },
            AttackType.Sword => new() { swordStart, swordEnd },
            _ => new()
        };
    }
}