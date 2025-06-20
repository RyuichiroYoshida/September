using System;
using UnityEngine;

[Serializable]
public class EffectSpqwnSettings
{
    public bool Loop;

    public static EffectSpqwnSettings Default => new EffectSpqwnSettings
    {
        Loop = false
    };
}
