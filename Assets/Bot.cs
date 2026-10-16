
using UnityEngine;
using UnityEngine.AI;

public class Bot : MonoBehaviour
{
    private NavMeshAgent agent;

    [Header("Alvo a perseguir")]
    public GameObject target;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

     

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

        float lookAhead = targetDir.magnitude / (agent.speed + target.GetComponent<Drive>().currentSpeed);
        Seek(target.transform.position + target.transform.forward * lookAhead);
    }


    void Update()
    {
        if (target == null)
            return;

        Pursue();
    }
}
