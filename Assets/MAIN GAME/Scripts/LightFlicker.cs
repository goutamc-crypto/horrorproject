using UnityEngine;

// Noise-driven flicker; dips below dipThreshold read as the light nearly failing.
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    public float speed = 3f;
    public float minFactor = 0.55f;
    public float dipThreshold = 0.25f;

    private Light target;
    private float baseIntensity, seed;

    private void Awake()
    {
        target = GetComponent<Light>();
        baseIntensity = target.intensity;
        seed = Random.value * 100f;
    }

    private void Update()
    {
        float n = Mathf.PerlinNoise(Time.time * speed, seed);
        float factor = Mathf.Lerp(minFactor, 1f, n);
        if (n < dipThreshold) factor *= 0.15f;
        target.intensity = baseIntensity * factor;
    }
}
