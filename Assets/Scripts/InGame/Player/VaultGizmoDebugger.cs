using UnityEngine;

namespace InGame.Player
{
    public class VaultGizmoDebugger : MonoBehaviour
    {
        public VaultParameter parameter;

        private void OnDrawGizmos()
        {
            Vector3 moveDir3 =
                new Vector3(parameter.moveDirection.x, 0, parameter.moveDirection.z).normalized;

            Vector3 position = parameter.Position;
            const float castStartBackOffset = 0.05f;
            Vector3 castStartOffset = -moveDir3 * castStartBackOffset;
            float frontCastDistance = parameter.reachDistance + castStartBackOffset;

            //==================================================
            // STEP1
            // 前方壁検出
            //==================================================

            Vector3 point1 =
                position
                + Vector3.up * (parameter.maxLedgeHeight - parameter.capsuleRadius)
                + castStartOffset;

            Vector3 point2 =
                position
                + Vector3.up * (parameter.minLedgeHeight + parameter.capsuleRadius)
                + castStartOffset;

            Gizmos.color = Color.yellow;
            DrawCapsule(point1, point2, parameter.capsuleRadius);

            Gizmos.color = Color.cyan;
            DrawCapsule(
                point1 + moveDir3 * frontCastDistance,
                point2 + moveDir3 * frontCastDistance,
                parameter.capsuleRadius);

            if (!Physics.CapsuleCast(
                point1,
                point2,
                parameter.capsuleRadius + 0.01f,
                moveDir3,
                out var frontHitInfo,
                frontCastDistance,
                ~0))
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(point2 + moveDir3 * frontCastDistance, 0.15f);
                return;
            }

            float frontHitDistance = Mathf.Max(0f, frontHitInfo.distance - castStartBackOffset);

            bool walkable =
                Vector3.Angle(Vector3.up, frontHitInfo.normal)
                <= parameter.groundSlopeThreshold;

            if (walkable)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(frontHitInfo.point, 0.15f);
                return;
            }

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(frontHitInfo.point, 0.1f);

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(
                frontHitInfo.point,
                frontHitInfo.point + frontHitInfo.normal);

            //==================================================
            // STEP2
            // 上面探索
            //==================================================

            Vector3 origin =
                frontHitInfo.point
                - frontHitInfo.normal * 0.3f;

            origin.y =
                position.y
                + parameter.maxLedgeHeight
                + parameter.capsuleRadius;

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(origin, parameter.capsuleRadius);

            Gizmos.DrawLine(
                origin,
                origin + Vector3.down
                * (parameter.maxLedgeHeight - parameter.minLedgeHeight));

            if (!Physics.SphereCast(
                origin,
                parameter.capsuleRadius,
                Vector3.down,
                out var heightHitInfo,
                parameter.maxLedgeHeight - parameter.minLedgeHeight,
                parameter.groundLayer))
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(origin + Vector3.down * 1f, 0.15f);
                return;
            }

            float ledgeHeight =
                heightHitInfo.point.y - position.y;

            if (ledgeHeight > parameter.maxLedgeHeight
                || ledgeHeight < parameter.minLedgeHeight)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(heightHitInfo.point, 0.15f);
                return;
            }

            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(heightHitInfo.point, 0.1f);

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(
                heightHitInfo.point,
                heightHitInfo.point + heightHitInfo.normal);

            //==================================================
            // HEAD SPACE
            //==================================================

            Vector3 headCheckP1 = heightHitInfo.point + heightHitInfo.normal * (parameter.capsuleRadius - 0.02f);

            Vector3 headCheckP2 =
                heightHitInfo.point
                + Vector3.up * (parameter.capsuleHeight - parameter.capsuleRadius);

            Collider[] overlaps =
                Physics.OverlapCapsule(
                    headCheckP1,
                    headCheckP2,
                    parameter.capsuleRadius + 0.01f,
                    parameter.groundLayer);

            bool blocked = false;

            foreach (var col in overlaps)
            {
                if (col.gameObject == frontHitInfo.collider.gameObject)
                    continue;

                blocked = true;

                Bounds b = col.bounds;

                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(b.center, b.size);
            }

            Gizmos.color =
                blocked
                ? Color.red
                : Color.green;

            DrawCapsule(
                headCheckP1,
                headCheckP2,
                parameter.capsuleRadius + 0.01f);

            if (blocked)
            {
                return;
            }

            //==================================================
            // STEP3
            // 奥行きチェック
            //==================================================

            float halfHeight =
                parameter.capsuleHeight * 0.5f;

            Vector3 p1 = frontHitInfo.point;
            p1.y =
                position.y
                + parameter.capsuleRadius
                + parameter.capsuleHeight;

            Vector3 p2 = frontHitInfo.point;
            p2.y =
                position.y
                + parameter.capsuleRadius;

            Gizmos.color = Color.yellow;

            DrawCapsule(
                p1,
                p2,
                parameter.capsuleRadius);

            Gizmos.DrawLine(
                p1,
                p1 - frontHitInfo.normal
                * (parameter.maxLedgeDepth + frontHitDistance));

            if (Physics.CapsuleCast(
                p1,
                p2,
                parameter.capsuleRadius,
                -frontHitInfo.normal,
                out var secondHit,
                parameter.maxLedgeDepth + frontHitDistance,
                parameter.groundLayer))
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(secondHit.point, 0.15f);
                return;
            }

            //==================================================
            // Reverse Check
            //==================================================

            Vector3 reverseOrigin =
                p2
                + Vector3.up * halfHeight
                - frontHitInfo.normal
                * (parameter.maxLedgeDepth + frontHitDistance);

            Vector3 reverseP1 =
                reverseOrigin + Vector3.up * halfHeight;

            Vector3 reverseP2 =
                reverseOrigin + Vector3.down * halfHeight;

            Gizmos.color = Color.yellow;

            DrawCapsule(
                reverseP1,
                reverseP2,
                parameter.capsuleRadius);

            if (!Physics.CapsuleCast(
                reverseP1,
                reverseP2,
                parameter.capsuleRadius,
                frontHitInfo.normal,
                out var backHit,
                parameter.maxLedgeDepth + frontHitDistance,
                parameter.groundLayer))
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(reverseOrigin, 0.15f);
                return;
            }

            if (backHit.distance < frontHitDistance)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(backHit.point, 0.15f);
                return;
            }

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(backHit.point, 0.1f);

            //==================================================
            // 最終地点
            //==================================================

            Vector3 vaultEnd =
                reverseOrigin
                - frontHitInfo.normal * frontHitDistance
                + Vector3.down * (halfHeight + parameter.capsuleRadius);

            bool endBlocked =
                Physics.CheckCapsule(
                    vaultEnd + Vector3.up * parameter.capsuleRadius,
                    vaultEnd + Vector3.up * (parameter.capsuleRadius + parameter.capsuleHeight),
                    parameter.capsuleRadius - 0.01f,
                    parameter.groundLayer);

            Gizmos.color =
                endBlocked
                ? Color.red
                : Color.green;

            DrawCapsule(
                vaultEnd + Vector3.up * parameter.capsuleRadius,
                vaultEnd + Vector3.up * (parameter.capsuleRadius + parameter.capsuleHeight),
                parameter.capsuleRadius - 0.01f);

            Gizmos.DrawSphere(vaultEnd, 0.12f);
        }

        private void DrawCapsule(
            Vector3 p1,
            Vector3 p2,
            float radius)
        {
            Gizmos.DrawLine(p1, p2);

            Gizmos.DrawWireSphere(p1, radius);
            Gizmos.DrawWireSphere(p2, radius);
        }
    }
}
