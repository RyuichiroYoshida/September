// DebugDrawHelper.cs
using UnityEngine;
using System.Collections.Generic;

namespace September.InGame.Common
{
    public class DebugDrawHelper : MonoBehaviour
    {
        private struct SphereDrawInfo
        {
            public Vector3 Position;
            public float Radius;
            public Color Color;
        }

        private static readonly List<SphereDrawInfo> _drawQueue = new();

        //Todo: 球形以外にも対応するように拡張する
        public static void RegisterAttackPosition(Vector3 pos, float radius, Color color)
        {
            _drawQueue.Add(new SphereDrawInfo { Position = pos, Radius = radius, Color = color });
        }

        private void OnDrawGizmos()
        {
            foreach (var info in _drawQueue)
            {
                Gizmos.color = info.Color;
                Gizmos.DrawWireSphere(info.Position, info.Radius);
            }
            _drawQueue.Clear();
        }
    }
}
