using UnityEngine;

// Relic: picked up on touch, bobs while waiting. Shrine: ends the game once all relics are collected.
[RequireComponent(typeof(Collider))]
public class ObjectiveTrigger : MonoBehaviour
{
    [SerializeField] private bool isShrine;
    [SerializeField] private Transform visual;

    private Vector3 basePos;

    private void Start()
    {
        GetComponent<Collider>().isTrigger = true;
        if (visual) basePos = visual.localPosition;
    }

    private void Update()
    {
        if (isShrine || !visual) return;
        visual.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.25f;
        visual.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (isShrine) { GameManager.Instance.ReachShrine(); return; }
        GameManager.Instance.CollectRelic(transform.position);
        gameObject.SetActive(false);
    }
}
