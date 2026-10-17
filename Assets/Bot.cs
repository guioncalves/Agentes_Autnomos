
using UnityEngine;
using UnityEngine.AI;

public class Bot : MonoBehaviour
{
    private NavMeshAgent agent;
    private Drive ds;

    [Header("Policia")]
    public GameObject target;

    [Header("Esconderijos")]
    public float hideMargin = 0.8f;
    public float navMeshSearchRadius = 3f;

    [Header("Detecao")]
    [Range(0f, 180f)]
    public float policeViewAngle = 60f;

    public float eyeHeight = 1f;
    public float detectionDistance = 30f;

    [Header("Comportamento")]
    public float cooldownDuration = 5f;
    public float approachDistance = 2f;
    public float fleeDistance = 8f;

    private Vector3 wanderTarget = Vector3.zero;

    private float hideUntil = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (agent == null)
        {
            Debug.LogError(
                "NavMeshAgent nao encontrado em " + gameObject.name
            );

            enabled = false;
            return;
        }

        if (target == null)
        {
            Debug.LogError("Policia nao atribuido no Inspector!");
            enabled = false;
            return;
        }

        ds = target.GetComponent<Drive>();
    }

    void Seek(Vector3 location)
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(location);
        }
    }

    void Flee(Vector3 location)
    {
        Vector3 fleeDirection =
            transform.position - location;

        fleeDirection.y = 0f;

        if (fleeDirection.sqrMagnitude < 0.001f)
            fleeDirection = -transform.forward;

        fleeDirection.Normalize();

        Vector3 desiredPosition =
            transform.position + fleeDirection * fleeDistance;

        NavMeshHit hit;

        if (NavMesh.SamplePosition(
            desiredPosition,
            out hit,
            navMeshSearchRadius,
            agent.areaMask))
        {
            Seek(hit.position);
        }
    }

    float GetTargetSpeed()
    {
        if (target == null)
            return 0f;

        if (ds != null)
            return Mathf.Abs(ds.currentSpeed);

        NavMeshAgent targetAgent =
            target.GetComponent<NavMeshAgent>();

        if (targetAgent != null)
            return targetAgent.velocity.magnitude;

        return 0f;
    }

    void Pursue()
    {
        if (target == null || agent == null)
            return;

        Vector3 direction =
            target.transform.position - transform.position;

        direction.y = 0f;

        float distance = direction.magnitude;

        if (distance <= approachDistance)
        {
            if (agent.isOnNavMesh)
                agent.ResetPath();

            return;
        }

        float targetSpeed = GetTargetSpeed();

        float lookAhead =
            distance / Mathf.Max(0.01f, agent.speed + targetSpeed);

        Vector3 predictedPosition =
            target.transform.position +
            target.transform.forward * targetSpeed * lookAhead;

        // Nao entrar diretamente na posicao do policia
        Vector3 approachDirection =
            transform.position - predictedPosition;

        approachDirection.y = 0f;

        if (approachDirection.sqrMagnitude > 0.001f)
        {
            predictedPosition +=
                approachDirection.normalized * approachDistance;
        }

        Seek(predictedPosition);
    }

    void Evade()
    {
        if (target == null)
            return;

        Vector3 direction =
            target.transform.position - transform.position;

        float targetSpeed = GetTargetSpeed();

        float lookAhead =
            direction.magnitude /
            Mathf.Max(0.01f, agent.speed + targetSpeed);

        Vector3 predictedPosition =
            target.transform.position +
            target.transform.forward * targetSpeed * lookAhead;

        Flee(predictedPosition);
    }

    void Wander()
    {
        float wanderRadius = 10f;
        float wanderDistance = 20f;
        float wanderJitter = 10f;

        wanderTarget += new Vector3(
            Random.Range(-1f, 1f) * wanderJitter,
            0f,
            Random.Range(-1f, 1f) * wanderJitter
        );

        wanderTarget =
            wanderTarget.normalized * wanderRadius;

        Vector3 targetLocal =
            wanderTarget + Vector3.forward * wanderDistance;

        Vector3 targetWorld =
            transform.TransformPoint(targetLocal);

        Seek(targetWorld);
    }

    void Hide()
    {
        CleverHide();
    }

    bool CleverHide()
    {
        if (target == null || agent == null || !agent.isOnNavMesh)
            return false;

        GameObject[] hidingSpots =
            GameObject.FindGameObjectsWithTag("hide");

        float bestDistance = Mathf.Infinity;
        Vector3 bestPosition = Vector3.zero;
        bool found = false;

        foreach (GameObject spot in hidingSpots)
        {
            if (spot == null)
                continue;

            Collider col =
                spot.GetComponentInChildren<Collider>();

            if (col == null)
                continue;

            Bounds bounds = col.bounds;

            Vector3 hideDir =
                bounds.center - target.transform.position;

            hideDir.y = 0f;

            if (hideDir.sqrMagnitude < 0.001f)
                continue;

            hideDir.Normalize();

            float extent =
                Mathf.Abs(hideDir.x) * bounds.extents.x +
                Mathf.Abs(hideDir.z) * bounds.extents.z;

            Vector3 desiredPosition =
                bounds.center +
                hideDir * (extent + agent.radius + hideMargin);

            desiredPosition.y = transform.position.y;

            NavMeshHit navHit;

            if (!NavMesh.SamplePosition(
                desiredPosition,
                out navHit,
                navMeshSearchRadius,
                agent.areaMask))
            {
                continue;
            }

            NavMeshPath path = new NavMeshPath();

            if (!agent.CalculatePath(navHit.position, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            // Verificar se o obstaculo bloqueia a visao
            Vector3 policeEye =
                target.transform.position + Vector3.up * eyeHeight;

            Vector3 hidingPoint =
                navHit.position + Vector3.up * eyeHeight;

            Vector3 rayDirection = hidingPoint - policeEye;
            float rayDistance = rayDirection.magnitude;

            RaycastHit hit;

            bool blocked = false;

            if (rayDistance > 0.01f &&
                Physics.Raycast(
                    policeEye,
                    rayDirection.normalized,
                    out hit,
                    rayDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                blocked =
                    hit.collider == col ||
                    hit.transform == spot.transform ||
                    hit.transform.IsChildOf(spot.transform);
            }

            if (!blocked)
                continue;

            float currentDistance =
                Vector3.Distance(transform.position, navHit.position);

            if (currentDistance < bestDistance)
            {
                bestDistance = currentDistance;
                bestPosition = navHit.position;
                found = true;
            }
        }

        if (found)
        {
            Debug.DrawLine(
                transform.position,
                bestPosition,
                Color.green
            );

            Debug.DrawRay(
                bestPosition,
                Vector3.up * 3f,
                Color.red
            );

            Seek(bestPosition);
            return true;
        }

        return false;
    }

    bool CanSeeTarget()
    {
        if (target == null)
            return false;

        Vector3 origin =
            transform.position + Vector3.up * eyeHeight;

        Vector3 destination =
            target.transform.position + Vector3.up * eyeHeight;

        Vector3 direction = destination - origin;
        float distance = direction.magnitude;

        if (distance < 0.01f)
            return true;

        RaycastHit hit;

        if (Physics.Raycast(
            origin,
            direction.normalized,
            out hit,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore))
        {
            return hit.transform == target.transform ||
                   hit.transform.IsChildOf(target.transform);
        }

        return false;
    }

    bool CanSeeMe()
    {
        if (target == null)
            return false;

        // Direcao do policia para o ladrao
        Vector3 direction =
            transform.position - target.transform.position;

        direction.y = 0f;

        float distance = direction.magnitude;

        if (distance > detectionDistance)
            return false;

        if (distance < 0.01f)
            return true;

        Vector3 policeForward = target.transform.forward;
        policeForward.y = 0f;

        float lookAngle =
            Vector3.Angle(policeForward, direction);

        // O ladrao esta fora do campo de visao
        if (lookAngle > policeViewAngle)
            return false;

        // Verificar se ha obstaculos entre os dois
        Vector3 origin =
            target.transform.position + Vector3.up * eyeHeight;

        Vector3 destination =
            transform.position + Vector3.up * eyeHeight;

        Vector3 rayDirection = destination - origin;

        RaycastHit hit;

        if (Physics.Raycast(
            origin,
            rayDirection.normalized,
            out hit,
            rayDirection.magnitude,
            ~0,
            QueryTriggerInteraction.Ignore))
        {
            return hit.transform == transform ||
                   hit.transform.IsChildOf(transform);
        }

        return false;
    }

    void Update()
    {
        if (target == null || agent == null || !agent.isOnNavMesh)
            return;

        // Se o policia estiver a ver o ladrao,
        // o ladrao procura um esconderijo.
        if (CanSeeMe())
        {
            hideUntil = Time.time + cooldownDuration;

            if (!CleverHide())
            {
                Evade();
            }

            return;
        }

        // Continuar escondido durante alguns segundos
        // depois de o policia deixar de o ver.
        if (Time.time < hideUntil)
        {
            if (!CleverHide())
            {
                Evade();
            }

            return;
        }

        // Quando o policia nao esta a olhar,
        // o ladrao aproxima-se dele.
        Pursue();
    }
}
