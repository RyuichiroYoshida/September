using UnityEngine;

public static class DebugDrawUtility
{
    public static void DrawWireSphere(Vector3 center, float radius, Color color, float duration = 0f)
    {
        int segments = 16;
        float angle = 360f / segments;

        // XY 平面
        for (int i = 0; i < segments; i++)
        {
            float theta1 = Mathf.Deg2Rad * angle * i;
            float theta2 = Mathf.Deg2Rad * angle * (i + 1);
            var p1 = center + new Vector3(Mathf.Cos(theta1), Mathf.Sin(theta1), 0) * radius;
            var p2 = center + new Vector3(Mathf.Cos(theta2), Mathf.Sin(theta2), 0) * radius;
            Debug.DrawLine(p1, p2, color, duration);
        }

        // YZ 平面
        for (int i = 0; i < segments; i++)
        {
            float theta1 = Mathf.Deg2Rad * angle * i;
            float theta2 = Mathf.Deg2Rad * angle * (i + 1);
            var p1 = center + new Vector3(0, Mathf.Cos(theta1), Mathf.Sin(theta1)) * radius;
            var p2 = center + new Vector3(0, Mathf.Cos(theta2), Mathf.Sin(theta2)) * radius;
            Debug.DrawLine(p1, p2, color, duration);
        }

        // XZ 平面
        for (int i = 0; i < segments; i++)
        {
            float theta1 = Mathf.Deg2Rad * angle * i;
            float theta2 = Mathf.Deg2Rad * angle * (i + 1);
            var p1 = center + new Vector3(Mathf.Cos(theta1), 0, Mathf.Sin(theta1)) * radius;
            var p2 = center + new Vector3(Mathf.Cos(theta2), 0, Mathf.Sin(theta2)) * radius;
            Debug.DrawLine(p1, p2, color, duration);
        }
    }
    
    public static void DrawWireCapsule(Vector3 p0, Vector3 p1, float radius, Color color, float duration = 0f)
    {
        // 中心位置を使って、球体を描画
        DrawWireSphere(p0, radius, color, duration);
        DrawWireSphere(p1, radius, color, duration);

        // 4方向をつなぐ
        Vector3[] directions = {
            Vector3.right, Vector3.back, Vector3.forward, Vector3.left
        };

        foreach (var dir in directions)
        {
            var offset = dir * radius;
            Debug.DrawLine(p0 + offset, p1 + offset, color, duration);
        }
    }

}