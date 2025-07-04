using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class DebugNetworkBehaviour : NetworkBehaviour
{
    public override void Spawned()
    {
        Debug.Log($"Spawned: {name}");
    }

    public override void FixedUpdateNetwork()
    {
        Debug.Log($"FixedUpdateNetwork: {name}");
    }
}

