using System.Numerics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public class AgentController : MonoBehaviour
{

    [HideInInspector]public NavMeshAgent agent;
    private NearestCarFinder nearestCarFinder;
    private TPPController player;
    private CarEnterSystem carEnterSystem;

    [HideInInspector]public float remainingDistance;

    [HideInInspector]public bool isAgentActive;
    private bool input;
 
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        nearestCarFinder = GetComponent<NearestCarFinder>();
        player = GetComponent<TPPController>();
        carEnterSystem = GetComponent<CarEnterSystem>();
        agent.enabled =  false;
        isAgentActive = false;
    }

    // Update is called once per frame
    void Update()
    {
        if(agent!= null && nearestCarFinder.NearestCar != null)
        {
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                if (input)
                {
                    input = false;
                    agent.enabled = false;
                    isAgentActive = false;
                    player.controller.enabled = true;
                }
                else
                {
                    input = true;
                    agent.enabled = true;
                    isAgentActive = true;
                    player.controller.enabled = false;
                }

            }
        }

        if(isAgentActive)
        {
            if(agent!=null)
            {
                agent.SetDestination(nearestCarFinder.NearestCar.GetComponent<CarComponents>().doorposition.position);
            }
        }

        if(agent.velocity.magnitude > 0.1f && agent.enabled)
        {
            player.animator.SetFloat("Speed", agent.velocity.magnitude);
        }
        if(input && remainingDistance<0.3f && isAgentActive)
        {
            carEnterSystem.EnterCar();
        }
       
    }

    void LateUpdate()
    {
        if(nearestCarFinder.NearestCar == null) return;
        
        remainingDistance = UnityEngine.Vector3.Distance(agent.transform.position, nearestCarFinder.NearestCar.GetComponent<CarComponents>().doorposition.transform.position);
        
    }
}
