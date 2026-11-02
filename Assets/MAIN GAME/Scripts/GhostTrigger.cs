using UnityEngine;
using System.Collections;

public class GhostTrigger : MonoBehaviour
{
    [SerializeField] private GameObject ghost;
    [SerializeField] private PlayerController playerController; // Reference to the player controller script

    public float moveDistance = 5f;
    public float moveSpeed = 2f;

    private bool triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        if (other.CompareTag("Player"))
        {
            triggered = true;

            ghost.SetActive(true);

            StartCoroutine(MoveGhost());
        }
    }

    IEnumerator MoveGhost()
    {
        playerController.enabled = false; // Disable player control
        Vector3 startPos = ghost.transform.position;
        Vector3 targetPos = startPos + Vector3.forward * moveDistance;

        while (Vector3.Distance(ghost.transform.position, targetPos) > 0.05f)
        {
            ghost.transform.position = Vector3.MoveTowards(
                ghost.transform.position,
                targetPos,
                moveSpeed * Time.deltaTime);

            yield return null;
        }
        ghost.SetActive(false);
        playerController.enabled = true; // Re-enable player control
    }
}