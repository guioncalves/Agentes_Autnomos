
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class Bot : MonoBehaviour
{
    private NavMeshAgent agent;
    private Drive policeDrive;
    private PoliceVision policeVision;

    [Header("Policia")]
    public GameObject target;

    [Header("Esconderijos")]
    public float hideMargin = 0.8f;
    public float hideArrivalDistance = 0.6f;
    public float navMeshSearchRadius = 3f;

    [Header("Movimento")]
    public float wanderDistance = 10f;
    public float approachDistance = 2f;
    public float wanderRadius = 8f;
    public float wanderArrivalDistance = 1f;

    private Vector3 hidingDestination;
    private GameObject chosenHide;

    private bool goingToHide;
    private bool waitingAtHide;

    private Vector3 wanderDestination;
    private bool wandering;

    // Cada ladrao reserva um esconderijo diferente.
    private static readonly Dictionary<Bot, GameObject> reservations =
        new Dictionary<Bot, GameObject>();

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        if (target == null)
        {
            Debug.LogError(name + ": atribui o Cop ao Target.");
            enabled = false;
            return;
        }

        policeDrive = target.GetComponent<Drive>();
        policeVision = target.GetComponent<PoliceVision>();

        if (policeVision == null)
        {
            Debug.LogError(
                name + ": o Cop precisa do script PoliceVision."
            );

            enabled = false;
        }
    }

    void OnDisable()
    {
        ReleaseHide();
    }

    void ReleaseHide()
    {
        reservations.Remove(this);

        chosenHide = null;
        goingToHide = false;
        waitingAtHide = false;
    }

    void Seek(Vector3 position)
    {
        if (agent.isOnNavMesh)
            agent.SetDestination(position);
    }

    // VERIFICAR SE O POLICIA VE O LADRAO
    bool PoliceCanSeeMe()
    {
        if (!policeVision.IsInView(transform.position))
            return false;

        Vector3 policeEye =
            target.transform.position + Vector3.up * 0.5f;

        Vector3 robberEye =
            transform.position + Vector3.up * 0.5f;

        Vector3 direction = robberEye - policeEye;
        float distance = direction.magnitude;

        if (distance < 0.01f)
            return true;

        RaycastHit[] hits = Physics.RaycastAll(
            policeEye,
            direction.normalized,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        float nearestDistance = Mathf.Infinity;
        Collider nearestCollider = null;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(target.transform))
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestCollider = hit.collider;
            }
        }

        // O primeiro objeto atingido tem de ser o ladrao.
        // Caso contrario, existe um obstaculo.
        return nearestCollider != null &&
               nearestCollider.transform.IsChildOf(transform);
    }

    // VERIFICAR SE UM ESCONDERIJO ESTA A FRENTE DO POLICIA
    bool IsHideInFrontOfPolice(Vector3 position)
    {
        Vector3 policeForward = target.transform.forward;
        policeForward.y = 0f;

        if (policeForward.sqrMagnitude < 0.001f)
            return false;

        policeForward.Normalize();

        Vector3 direction =
            position - target.transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return false;

        direction.Normalize();

        // Positivo: a frente do policia.
        // Negativo: atras do policia.
        float dot = Vector3.Dot(
            policeForward,
            direction
        );

        return dot > 0f;
    }

    // VERIFICAR SE O OBSTACULO BLOQUEIA A VISAO
    bool IsReallyHidden(Vector3 position, GameObject spot)
    {
        Vector3 policeEye =
            target.transform.position + Vector3.up * 0.5f;

        Vector3 robberEye =
            position + Vector3.up * 0.5f;

        Vector3 direction = robberEye - policeEye;
        float distance = direction.magnitude;

        if (distance < 0.01f)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(
            policeEye,
            direction.normalized,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        float nearestDistance = Mathf.Infinity;
        Collider nearestCollider = null;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(target.transform))
                continue;

            // Ignorar o proprio ladrao e outros ladroes.
            if (hit.collider.GetComponentInParent<Bot>() != null)
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestCollider = hit.collider;
            }
        }

        // O obstaculo escolhido tem de ser o primeiro
        // a bloquear a linha de visao.
        return nearestCollider != null &&
               nearestCollider.transform.IsChildOf(spot.transform);
    }

    // PERSEGUIR O POLICIA
    void Pursue()
    {
        Vector3 direction =
            target.transform.position - transform.position;

        direction.y = 0f;

        float distance = direction.magnitude;

        if (distance <= approachDistance)
        {
            agent.ResetPath();
            return;
        }

        float speed = policeDrive != null
            ? Mathf.Abs(policeDrive.currentSpeed)
            : 0f;

        float predictionTime =
            distance / Mathf.Max(agent.speed + speed, 0.1f);

        predictionTime = Mathf.Min(predictionTime, 1f);

        Vector3 predictedPosition =
            target.transform.position +
            target.transform.forward * speed * predictionTime;

        Seek(predictedPosition);
    }

    // VAGUEAR PELO NAVMESH
    void Wander()
    {
        if (wandering)
        {
            Vector3 difference =
                wanderDestination - transform.position;

            difference.y = 0f;

            if (difference.magnitude > wanderArrivalDistance &&
                (agent.pathPending || agent.hasPath))
            {
                return;
            }

            wandering = false;
        }

        for (int i = 0; i < 12; i++)
        {
            Vector2 random =
                Random.insideUnitCircle * wanderRadius;

            Vector3 point =
                transform.position +
                new Vector3(random.x, 0f, random.y);

            NavMeshHit hit;

            if (!NavMesh.SamplePosition(
                point,
                out hit,
                navMeshSearchRadius,
                agent.areaMask))
            {
                continue;
            }

            NavMeshPath path = new NavMeshPath();

            if (!agent.CalculatePath(hit.position, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            Vector3 difference =
                hit.position - transform.position;

            difference.y = 0f;

            if (difference.magnitude <
                wanderArrivalDistance * 2f)
            {
                continue;
            }

            wanderDestination = hit.position;
            wandering = true;

            Seek(wanderDestination);
            return;
        }

        agent.ResetPath();
    }

    // VERIFICAR SE O ESCONDERIJO ESTA RESERVADO
    bool IsReserved(GameObject spot)
    {
        foreach (KeyValuePair<Bot, GameObject> entry in reservations)
        {
            if (entry.Key != null &&
                entry.Key != this &&
                entry.Value == spot)
            {
                return true;
            }
        }

        return false;
    }

    // ESCOLHER UM ESCONDERIJO VALIDO
    bool CleverHide()
    {
        GameObject[] spots =
            GameObject.FindGameObjectsWithTag("hide");

        GameObject bestSpot = null;
        Vector3 bestPosition = Vector3.zero;
        float bestDistance = Mathf.Infinity;

        foreach (GameObject spot in spots)
        {
            if (IsReserved(spot))
                continue;

            Collider[] colliders =
                spot.GetComponentsInChildren<Collider>();

            Collider obstacle = null;
            float smallestWidth = Mathf.Infinity;

            foreach (Collider col in colliders)
            {
                if (!col.enabled || col.isTrigger)
                    continue;

                float width = Mathf.Max(
                    col.bounds.size.x,
                    col.bounds.size.z
                );

                if (width < smallestWidth)
                {
                    smallestWidth = width;
                    obstacle = col;
                }
            }

            if (obstacle == null)
                continue;

            Bounds bounds = obstacle.bounds;

            // NOVA REGRA:
            // Nao escolher objetos atras do policia.
            if (!IsHideInFrontOfPolice(bounds.center))
                continue;

            // O destino deve ficar do lado oposto
            // ao policia, atras do obstaculo.
            Vector3 hideDirection =
                bounds.center - target.transform.position;

            hideDirection.y = 0f;

            if (hideDirection.sqrMagnitude < 0.001f)
                continue;

            hideDirection.Normalize();

            float extent =
                Mathf.Abs(hideDirection.x) * bounds.extents.x +
                Mathf.Abs(hideDirection.z) * bounds.extents.z;

            for (int i = 0; i < 10; i++)
            {
                float offset =
                    extent + agent.radius + hideMargin +
                    i * 0.2f;

                Vector3 desired =
                    bounds.center + hideDirection * offset;

                desired.y = transform.position.y;

                NavMeshHit hit;

                if (!NavMesh.SamplePosition(
                    desired,
                    out hit,
                    navMeshSearchRadius,
                    agent.areaMask))
                {
                    continue;
                }

                Vector2 difference = new Vector2(
                    hit.position.x - desired.x,
                    hit.position.z - desired.z
                );

                if (difference.magnitude > 0.6f)
                    continue;

                // O destino tambem tem de ficar
                // a frente do policia.
                if (!IsHideInFrontOfPolice(hit.position))
                    continue;

                // O obstaculo tem de bloquear
                // realmente a linha de visao.
                if (!IsReallyHidden(hit.position, spot))
                    continue;

                NavMeshPath path = new NavMeshPath();

                if (!agent.CalculatePath(hit.position, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                float distance = 0f;

                for (int j = 1; j < path.corners.Length; j++)
                {
                    distance += Vector3.Distance(
                        path.corners[j - 1],
                        path.corners[j]
                    );
                }

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPosition = hit.position;
                    bestSpot = spot;
                }
            }
        }

        if (bestSpot == null)
            return false;

        chosenHide = bestSpot;
        hidingDestination = bestPosition;

        goingToHide = true;
        waitingAtHide = false;
        wandering = false;

        reservations[this] = bestSpot;

        Debug.Log(name + " escolheu " + bestSpot.name);

        Seek(hidingDestination);
        return true;
    }

    // LOGICA PRINCIPAL
    void Update()
    {
        if (target == null ||
            policeVision == null ||
            !agent.isOnNavMesh)
        {
            return;
        }

        Vector3 difference =
            target.transform.position - transform.position;

        difference.y = 0f;

        float distance = difference.magnitude;

        bool insideCone =
            policeVision.IsInView(transform.position);

        bool policeCanSee = PoliceCanSeeMe();

        // 1. Se chegou ao esconderijo,
        // espera ate sair do cone de visao.
        if (waitingAtHide)
        {
            if (insideCone)
            {
                agent.ResetPath();
                return;
            }

            ReleaseHide();
        }

        // 2. Continuar ate ao esconderijo escolhido.
        if (goingToHide)
        {
            if (chosenHide == null ||
                !chosenHide.activeInHierarchy)
            {
                ReleaseHide();
            }
            else
            {
                Vector3 toHide =
                    hidingDestination - transform.position;

                toHide.y = 0f;

                if (toHide.magnitude > hideArrivalDistance)
                {
                    Seek(hidingDestination);
                    return;
                }

                agent.ResetPath();

                goingToHide = false;
                waitingAtHide = true;

                Debug.Log(
                    name + " chegou ao esconderijo."
                );

                return;
            }
        }

        // 3. Se o policia ve o ladrao,
        // procurar um esconderijo a frente dele.
        if (policeCanSee)
        {
            wandering = false;

            if (CleverHide())
                return;

            // Nenhum esconderijo valido.
            agent.ResetPath();
            return;
        }

        // 4. Se nao esta visivel,
        // perseguir ou vaguear.
        ReleaseHide();

        if (distance > wanderDistance)
        {
            Wander();
        }
        else
        {
            wandering = false;
            Pursue();
        }
    }
}
