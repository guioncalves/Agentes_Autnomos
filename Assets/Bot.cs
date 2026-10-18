
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

    // Cada objeto hide pode ser reservado por um ladrao
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

    // VAGUEAR PELO CENARIO
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

            Vector3 difference = hit.position - transform.position;
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

    // VERIFICAR RESERVAS
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

    // PROCURAR ESCONDERIJO
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

        reservations[this] = bestSpot;

        wandering = false;

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

        // O campo de visao vem diretamente do PoliceVision
        bool policeLooking =
            policeVision.IsInView(transform.position);

        // 1. Se esta escondido, esperar ate o
        // policia deixar de olhar para ele.
        if (waitingAtHide)
        {
            if (policeLooking)
            {
                agent.ResetPath();
                return;
            }

            ReleaseHide();
        }

        // 2. Se o policia nao esta a olhar,
        // perseguir ou vaguear.
        if (!policeLooking)
        {
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

            return;
        }

        // 3. Continuar ate ao esconderijo.
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

                Debug.Log(name + " chegou ao esconderijo.");
                return;
            }
        }

        // 4. Dentro do cone: procurar esconderijo,
        // mesmo estando a mais de 10 unidades.
        wandering = false;

        if (CleverHide())
            return;

        // Sem esconderijo disponivel
        agent.ResetPath();
    }
}
