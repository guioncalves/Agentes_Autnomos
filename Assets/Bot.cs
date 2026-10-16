
using UnityEngine;
using UnityEngine.AI;

public class Bot : MonoBehaviour
{
    private NavMeshAgent agent;
    private Drive ds;

    [Header("Alvo a perseguir ou evitar")]
    public GameObject target;

    private Vector3 wanderTarget = Vector3.zero;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (agent == null)
        {
            Debug.LogError("NavMeshAgent nao encontrado em " + gameObject.name);
            enabled = false;
            return;
        }

        if (target != null)
        {
            ds = target.GetComponent<Drive>();
        }
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
        Vector3 fleeVector = location - transform.position;
        Seek(transform.position - fleeVector);
    }

    float GetTargetSpeed()
    {
        if (ds != null)
            return Mathf.Abs(ds.currentSpeed);

        NavMeshAgent targetAgent = target.GetComponent<NavMeshAgent>();

        if (targetAgent != null)
            return targetAgent.velocity.magnitude;

        return 0f;
    }

    void Pursue()
    {
        if (target == null)
            return;

        Vector3 targetDir = target.transform.position - transform.position;

        float relativeHeading =
            Vector3.Angle(transform.forward, target.transform.forward);

        float toTarget =
            Vector3.Angle(transform.forward, targetDir);

        float targetSpeed = GetTargetSpeed();

        if ((toTarget > 90f && relativeHeading < 20f) ||
            targetSpeed < 0.01f)
        {
            Seek(target.transform.position);
            return;
        }

        float lookAhead = targetDir.magnitude /
            Mathf.Max(0.01f, agent.speed + targetSpeed);

        Seek(
            target.transform.position +
            target.transform.forward * targetSpeed * lookAhead
        );
    }

    void Evade()
    {
        if (target == null)
            return;

        Vector3 targetDir = target.transform.position - transform.position;

        float targetSpeed = GetTargetSpeed();

        float lookAhead = targetDir.magnitude /
            Mathf.Max(0.01f, agent.speed + targetSpeed);

        Flee(
            target.transform.position +
            target.transform.forward * targetSpeed * lookAhead
        );
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

        wanderTarget = wanderTarget.normalized * wanderRadius;

        Vector3 targetLocal =
            wanderTarget + Vector3.forward * wanderDistance;

        Vector3 targetWorld =
            transform.TransformPoint(targetLocal);

        Seek(targetWorld);
    }

    void Hide()
    {
        if (target == null)
            return;

        GameObject[] hidingSpots = World.Instance.GetHidingSpots();

        if (hidingSpots == null || hidingSpots.Length == 0)
            return;

        float dist = Mathf.Infinity;
        Vector3 chosenSpot = Vector3.zero;

        foreach (GameObject spot in hidingSpots)
        {
            if (spot == null)
                continue;

            Vector3 hideDir =
                spot.transform.position - target.transform.position;

            if (hideDir.sqrMagnitude < 0.001f)
                continue;

            Vector3 hidePos =
                spot.transform.position + hideDir.normalized * 3f;

            float currentDistance =
                Vector3.Distance(transform.position, hidePos);

            if (currentDistance < dist)
            {
                chosenSpot = hidePos;
                dist = currentDistance;
            }
        }

        if (dist < Mathf.Infinity)
        {
            Debug.DrawLine(transform.position, chosenSpot, Color.green);
            Debug.DrawRay(chosenSpot, Vector3.up * 3f, Color.red);

            Seek(chosenSpot);
        }
    }




    void CleverHide()
    {
        if (target == null || agent == null || !agent.isOnNavMesh)
            return;

        GameObject[] hidingSpots = World.Instance.GetHidingSpots();

        if (hidingSpots == null || hidingSpots.Length == 0)
            return;

        float dist = Mathf.Infinity;
        Vector3 chosenSpot = Vector3.zero;
        Vector3 chosenDir = Vector3.zero;
        GameObject chosenGo = null;

        foreach (GameObject spot in hidingSpots)
        {
            if (spot == null)
                continue;

            // Direcao do policia para a arvore
            Vector3 hideDir =
                spot.transform.position - target.transform.position;

            hideDir.y = 0f;

            if (hideDir.sqrMagnitude < 0.001f)
                continue;

            // Ponto atras da arvore
            Vector3 hidePos =
                spot.transform.position + hideDir.normalized * 2f;

            float currentDistance =
                Vector3.Distance(transform.position, hidePos);

            if (currentDistance < dist)
            {
                chosenSpot = hidePos;
                chosenDir = hideDir;
                chosenGo = spot;
                dist = currentDistance;
            }
        }

        if (chosenGo == null)
            return;

        Collider hideCol =
            chosenGo.GetComponentInChildren<Collider>();

        if (hideCol == null)
        {
            Seek(chosenSpot);
            return;
        }

        // Raios apontados para a arvore
        Ray backRay = new Ray(
            chosenSpot,
            -chosenDir.normalized
        );

        RaycastHit info;
        float distance = 2f;

        if (hideCol.Raycast(backRay, out info, distance))
        {
            // Ponto junto ao tronco, do lado oposto ao policia
            Vector3 destination =
                info.point + chosenDir.normalized * 0.2f;

            Debug.DrawLine(
                transform.position,
                destination,
                Color.green
            );

            Debug.DrawRay(
                destination,
                Vector3.up * 3f,
                Color.red
            );

            Seek(destination);
        }
        else
        {
            Debug.DrawLine(
                transform.position,
                chosenSpot,
                Color.yellow
            );

            Seek(chosenSpot);
        }
    }

    bool CanSeeTarget()
    {
        RaycastHit raycastInfo;
        Vector3 rayToTarget = target.transform.position - this.transform.position;
        if(Physics.Raycast(this.transform.position, rayToTarget, out raycastInfo))
        {
            if (raycastInfo.transform.gameObject.tag == "cop")
                return true;
        }
        return false;
    }


    void Update()
    {
        if (target == null)
            return;

        CleverHide();
    }
}
