using UnityEngine;
using UnityEngine.UI;

// Drives the HUD minimap (player-centred, scrolls a baked top-down texture) and the full map markers.
public class MapUI : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Vector2 worldMin = new Vector2(230, 155); // world XZ of the map texture's bottom-left
    [SerializeField] private float worldSize = 400f;                  // world metres covered by the texture
    [SerializeField] private RawImage minimap;
    [SerializeField] private float minimapZoom = 0.2f;                // fraction of the map shown
    [SerializeField] private RectTransform minimapArrow;
    [SerializeField] private RectTransform mapArea, mapPlayer;        // full map
    [SerializeField] private Transform[] targets;
    [SerializeField] private RectTransform[] targetMarkers;

    private Vector2 ToUV(Vector3 w) => new Vector2((w.x - worldMin.x) / worldSize, (w.z - worldMin.y) / worldSize);

    private void LateUpdate()
    {
        Vector2 uv = ToUV(player.position);
        if (minimap && minimap.isActiveAndEnabled)
        {
            minimap.uvRect = new Rect(uv.x - minimapZoom / 2f, uv.y - minimapZoom / 2f, minimapZoom, minimapZoom);
            minimapArrow.localEulerAngles = new Vector3(0, 0, -player.eulerAngles.y);
        }
        if (mapArea && mapArea.gameObject.activeInHierarchy)
        {
            Place(mapPlayer, uv);
            mapPlayer.localEulerAngles = new Vector3(0, 0, -player.eulerAngles.y);
            for (int i = 0; i < targets.Length; i++)
            {
                bool live = targets[i] && targets[i].gameObject.activeInHierarchy;
                targetMarkers[i].gameObject.SetActive(live);
                if (live) Place(targetMarkers[i], ToUV(targets[i].position));
            }
        }
    }

    private void Place(RectTransform marker, Vector2 uv) =>
        marker.anchoredPosition = new Vector2((uv.x - 0.5f) * mapArea.rect.width, (uv.y - 0.5f) * mapArea.rect.height);
}
