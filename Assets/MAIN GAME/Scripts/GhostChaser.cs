using UnityEngine;

// Keep this object inactive in the scene. Ghost appears behind the player and floats straight at them (passes through walls) for chaseTime seconds.
public class GhostChaser : MonoBehaviour
{
    [SerializeField] private float speed = 5f;        // between walk (4.2) and sprint (6.6): sprinting escapes
    [SerializeField] private float chaseTime = 20f;
    [SerializeField] private float spawnDistance = 25f;
    [SerializeField] private float catchDistance = 1.6f;

    private Transform target;
    private float timeLeft;

    public void StartChase(Transform player)
    {
        target = player;
        timeLeft = chaseTime;
        transform.position = player.position - player.forward * spawnDistance;
        gameObject.SetActive(true);
    }

    public void Stop() => gameObject.SetActive(false);

    private void Update()
    {
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f) { Stop(); return; }

        Vector3 to = target.position - transform.position;
        to.y = 0f;
        if (to.magnitude < catchDistance) { GameManager.Instance.PlayerCaught(); return; }

        transform.rotation = Quaternion.LookRotation(to);
        Vector3 next = transform.position + to.normalized * speed * Time.deltaTime;
        next.y = target.position.y; // float at player height over dunes
        transform.position = next;
    }
}
