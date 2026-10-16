
using UnityEngine;
using UnityEngine.AI;

public class Bot : MonoBehaviour
{
    private NavMeshAgent agent;

    [Header("Alvo a perseguir")]
    public GameObject target;
    private Drive ds;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (target != null)
        {
            ds = target.GetComponent<Drive>();
        }

    }

    void Seek(Vector3 location)
    {


        agent.SetDestination(location);

    }

    void Flee(Vector3 location)
    {
        Vector3 fleeVector = location - this.transform.position;
        agent.SetDestination(this.transform.position - fleeVector);
    }

    void Pursue()
    {
        Vector3 targetDir = target.transform.position - this.transform.position;
        float relativeHeading = Vector3.Angle(this.transform.forward, this.transform.TransformVector(target.transform.forward));
        float toTarget = Vector3.Angle(this.transform.forward, this.transform.TransformVector(targetDir));
        if ((toTarget > 90 && relativeHeading < 20 || target.GetComponent<Drive>().currentSpeed < 0.01f))
        {
            Seek(target.transform.position);
        }

        float lookAhead = targetDir.magnitude / (agent.speed + ds.currentSpeed);
        Seek(target.transform.position + target.transform.forward * lookAhead);
    }
    void Evade()
    {
        Vector3 targetDir = target.transform.position - this.transform.position;
        float lookAhead = targetDir.magnitude / (agent.speed + ds.currentSpeed);
        Flee(target.transform.position + target.transform.forward * lookAhead);

    }
    void Update()
    {
        if (target == null)
            return;

        Evade();
    }
}
