
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

    void Update()
    {
        if (target == null)
            return;

        Flee(target.transform.position);
    }
}
