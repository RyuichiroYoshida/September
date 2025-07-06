using System;
using UnityEngine;

public interface IHitboxExecutor
{
    bool IsFinished { get; }
    void Tick(float deltaTime);

#if UNITY_EDITOR
    void DebugGizmo(Vector3 start, Vector3 end);
#endif
}