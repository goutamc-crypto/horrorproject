using UnityEngine;

public class CarEnterSystem : MonoBehaviour
{
   private AgentController _agentController;
   private TPPController Player;
   private NearestCarFinder _nearestCar;
    
    void Start()
    {
        _agentController = GetComponent<AgentController>();
        Player = GetComponent<TPPController>();
        _nearestCar = GetComponent<NearestCarFinder>();
    }

    // Update is called once per frame
   public void EnterCar()
    {
        _agentController.agent.enabled = false;
        _agentController.isAgentActive = false;

        transform.rotation = Quaternion.Euler(0, _nearestCar.NearestCar.GetComponent<CarComponents>().doorposition.transform.eulerAngles.y, 0)*Quaternion.Euler(0, 90, 0);
        Player.animator.applyRootMotion = true;
        Player.animator.Play("Entering Car");
    }

    void LateUpdate()
    {
        if(Player.animator.GetCurrentAnimatorStateInfo(0).IsName("Entering Car"))
        {
            Player.animator.MatchTarget(_nearestCar.NearestCar.GetComponent<CarComponents>().sittingposition.position, _nearestCar.NearestCar.GetComponent<CarComponents>().sittingposition.rotation, AvatarTarget.Body, new MatchTargetWeightMask(Vector3.one, 0), 0.27f, 0.52f);
        }
    }
}
