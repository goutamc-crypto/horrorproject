using Unity.VisualScripting;
using UnityEngine;

public class NearestCarFinder : MonoBehaviour
{
   public GameObject[] SceneVehicles;
   public Transform NearestCar;

   void Start()
   {
      SceneVehicles = GameObject.FindGameObjectsWithTag("Vehicle");
      NearestCar = null;
   }
   void LateUpdate()
   {
      float nearestDistance = Mathf.Infinity;
      Vector3 currentPosition = transform.position;

      foreach (var vehicle in SceneVehicles)
      {
         float distanceToVehicle = Vector3.Distance(vehicle.transform.position, currentPosition);
         if (distanceToVehicle < nearestDistance)
         {
            nearestDistance = distanceToVehicle;
            NearestCar = vehicle.transform;
         }
      }
   }

   private void OnDrawGizmos()
   {
      if (NearestCar != null)
      {
         Gizmos.color = Color.red;
         Gizmos.DrawLine(transform.position, NearestCar.GetComponent<CarComponents>().doorhandleposition.position);
      }
   }

}
