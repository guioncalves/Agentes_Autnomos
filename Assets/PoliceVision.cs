
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PoliceVision : MonoBehaviour
{
    [Header("Campo de visao")]
    [Range(10f, 360f)]
    public float viewAngle = 350f;

    [Min(0.1f)]
    public float viewDistance = 50f;

    [Header("Visualizacao")]
    [Range(10, 120)]
    public int segments = 60;

    [Range(2, 40)]
    public int rayCount = 15;

    public bool showGizmos = true;

    [Range(0f, 3f)]
    public float gizmoHeight = 1f;

    // Verifica se um ponto esta dentro do campo de visao.
    public bool IsInView(Vector3 position)
    {
        Vector3 direction = position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude >
            viewDistance * viewDistance)
        {
            return false;
        }

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
            transform.position + Vector3.up * gizmoHeight;

        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        forward.Normalize();

        float distance = Mathf.Max(0.1f, viewDistance);
        float halfAngle = viewAngle * 0.5f;

        // Diagnostico: esfera rosa na origem
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(origin, 0.5f);

        // Diagnostico: linha verde vertical
        Gizmos.color = Color.green;
        Gizmos.DrawLine(
            origin,
            origin + Vector3.up * 5f
        );

        // Contorno amarelo
        Gizmos.color = Color.yellow;

        Vector3 previousPoint = Vector3.zero;
        int count = Mathf.Max(3, segments);

        for (int i = 0; i <= count; i++)
        {
            float angle =
                -halfAngle + viewAngle * i / count;

            Vector3 direction =
                Quaternion.Euler(0f, angle, 0f) * forward;

            Vector3 point =
                origin + direction * distance;

            if (i > 0)
                Gizmos.DrawLine(previousPoint, point);

            previousPoint = point;
        }

        // Limites do cone
        Vector3 left =
            Quaternion.Euler(0f, -halfAngle, 0f) * forward;

        Vector3 right =
            Quaternion.Euler(0f, halfAngle, 0f) * forward;

        Gizmos.DrawLine(origin, origin + left * distance);
        Gizmos.DrawLine(origin, origin + right * distance);

        // Raios vermelhos
        Gizmos.color = Color.red;

        int rays = Mathf.Max(2, rayCount);

        for (int i = 0; i < rays; i++)
        {
            float angle =
                -halfAngle + viewAngle * i / (rays - 1f);

            Vector3 direction =
                Quaternion.Euler(0f, angle, 0f) * forward;

            Gizmos.DrawLine(
                origin,
                origin + direction * distance
            );
        }

        // Direcao central
        Gizmos.color = Color.green;
        Gizmos.DrawLine(
            origin,
            origin + forward * distance
        );

#if UNITY_EDITOR
        // Cone amarelo preenchido e semitransparente
        Handles.color = new Color(1f, 0.85f, 0f, 0.18f);

        Handles.DrawSolidArc(
            origin,
            Vector3.up,
            Quaternion.Euler(0f, -halfAngle, 0f) * forward,
            viewAngle,
            distance
        );
#endif
    }
}
