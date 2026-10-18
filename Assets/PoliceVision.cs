
using UnityEngine;

public class PoliceVision : MonoBehaviour
{
    [Header("Campo de visao")]
    [Range(10f, 360f)]
    public float viewAngle = 350f;

    public float viewDistance = 50f;

    [Header("Visualizacao")]
    [Range(10, 120)]
    public int segments = 60;

    [Range(2, 40)]
    public int rayCount = 15;

    public bool showGizmos = true;

    public bool IsInView(Vector3 position)
    {
        Vector3 direction = position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > viewDistance * viewDistance)
            return false;

        if (direction.sqrMagnitude < 0.001f)
            return true;

        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            return false;

        float angle = Vector3.Angle(forward, direction);

        return angle <= viewAngle * 0.5f;
    }

    void OnDrawGizmos()
    {
        if (!showGizmos)
            return;

        Vector3 origin =
            transform.position + Vector3.up * 0.5f;

        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            return;

        forward.Normalize();

        float halfAngle = viewAngle * 0.5f;

        // Contorno amarelo
        Gizmos.color = Color.yellow;

        Vector3 previousPoint = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            float angle =
                -halfAngle + viewAngle * i / segments;

            Vector3 direction =
                Quaternion.Euler(0f, angle, 0f) * forward;

            Vector3 point =
                origin + direction * viewDistance;

            if (i > 0)
                Gizmos.DrawLine(previousPoint, point);

            previousPoint = point;
        }

        // Limites do cone
        Vector3 left =
            Quaternion.Euler(0f, -halfAngle, 0f) * forward;

        Vector3 right =
            Quaternion.Euler(0f, halfAngle, 0f) * forward;

        Gizmos.DrawLine(origin, origin + left * viewDistance);
        Gizmos.DrawLine(origin, origin + right * viewDistance);

        // Raios vermelhos apenas visuais
        Gizmos.color = Color.red;

        int count = Mathf.Max(2, rayCount);

        for (int i = 0; i < count; i++)
        {
            float angle =
                -halfAngle + viewAngle * i / (count - 1f);

            Vector3 direction =
                Quaternion.Euler(0f, angle, 0f) * forward;

            Gizmos.DrawLine(
                origin,
                origin + direction * viewDistance
            );
        }
    }
}
