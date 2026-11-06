using UnityEngine;

// All one-shot audio goes through a small fixed pool, so the sound count can never grow out of control on mobile.
public class HorrorAudio : MonoBehaviour
{
    public static HorrorAudio Instance { get; private set; }

    private const int PoolSize = 8;
    private const int Everything = ~0;

    [SerializeField] private PlayerController player;

    [Header("Ambience")]
    [SerializeField] private AudioClip windLoop;
    [SerializeField] private AudioClip droneLoop;
    [SerializeField] private float windVolume = 0.45f, droneVolume = 0.3f;

    [Header("World events")]
    [SerializeField] private AudioClip[] openAirEvents;   // whispers, wail: can come from anywhere
    [SerializeField] private AudioClip[] structureEvents; // creaks, door, debris: only played beside something built
    [SerializeField] private Vector2 eventInterval = new Vector2(18f, 42f);

    [Header("Suspense")]
    [SerializeField] private AudioClip swell;
    [SerializeField] private AudioClip[] stingers;
    [SerializeField] private float swellCooldown = 75f;

    [Header("Player footsteps")]
    [SerializeField] private AudioClip[] sandSteps;
    [SerializeField] private AudioClip[] stoneSteps;
    [SerializeField] private AudioClip[] woodSteps;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.6f; // mix level under the master volume slider

    [Header("Chudail")]
    public AudioClip breathLoop;
    public AudioClip chaseLoop;
    public AudioClip detect;
    public AudioClip attack;
    public AudioClip[] vocals; // whispers and laughter
    public AudioClip[] walkSteps;
    public AudioClip[] runSteps;

    private AudioSource[] pool;
    private AudioSource wind, drone, steps; // steps get their own source so a busy pool can't swallow them
    private AudioClip lastStep;
    private float eventTimer, swellTimer;
    private int lastPlant;
    private bool wasSheltered, wasGrounded = true;

    private void Awake()
    {
        Instance = this;
        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            pool[i] = NewSource("Pooled");
            pool[i].rolloffMode = AudioRolloffMode.Linear;
        }
        wind = StartLoop(windLoop);
        drone = StartLoop(droneLoop);
        steps = NewSource("Footsteps");
        eventTimer = Random.Range(eventInterval.x, eventInterval.y);
    }

    private AudioSource NewSource(string sourceName)
    {
        var s = new GameObject(sourceName).AddComponent<AudioSource>();
        s.transform.SetParent(transform);
        s.playOnAwake = false;
        s.dopplerLevel = 0f;
        return s;
    }

    private AudioSource StartLoop(AudioClip clip)
    {
        AudioSource s = NewSource(clip.name);
        s.clip = clip;
        s.loop = true;
        s.Play();
        return s;
    }

    private void Update()
    {
        // Slow drift, so the ambience breathes instead of sitting at one level.
        float t = Time.unscaledTime;
        wind.volume = windVolume * Mathf.Lerp(0.55f, 1f, Mathf.PerlinNoise(t * 0.07f, 0.3f));
        drone.volume = droneVolume * Mathf.Lerp(0.5f, 1f, Mathf.PerlinNoise(t * 0.05f, 7.1f));

        UpdateWorldEvents();
        UpdateSuspense();
        UpdateFootsteps();
    }

    // Returns false when every pool voice is busy: dropping a sound beats overlapping too many.
    public bool Play2D(AudioClip clip, float volume) => Play(clip, Vector3.zero, volume, 0f, 1f, 1f, 1f);

    public bool Play3D(AudioClip clip, Vector3 at, float volume, float maxDistance, float pitchRange = 0.05f) =>
        Play(clip, at, volume, 1f, 2f, maxDistance, Random.Range(1f - pitchRange, 1f + pitchRange));

    private bool Play(AudioClip clip, Vector3 at, float volume, float spatial, float minDistance, float maxDistance, float pitch)
    {
        foreach (AudioSource s in pool)
        {
            if (s.isPlaying) continue;
            s.transform.position = at;
            s.clip = clip;
            s.volume = volume;
            s.pitch = pitch;
            s.spatialBlend = spatial;
            s.minDistance = minDistance;
            s.maxDistance = maxDistance;
            s.Play();
            return true;
        }
        return false;
    }

    public void Sting(float volume) => Play2D(stingers[Random.Range(0, stingers.Length)], volume);

    public void Swell()
    {
        if (swellTimer > 0f) return;
        swellTimer = swellCooldown;
        Play2D(swell, 0.5f);
    }

    private void UpdateWorldEvents()
    {
        eventTimer -= Time.deltaTime;
        if (eventTimer > 0f) return;
        eventTimer = Random.Range(eventInterval.x, eventInterval.y);

        // Mostly from behind or the sides: the player has to turn to check, and sees nothing.
        Vector3 back = -player.transform.forward;
        Vector3 at = player.transform.position + Quaternion.Euler(0f, Random.Range(-100f, 100f), 0f) * back * Random.Range(12f, 30f);
        bool nearStructure = Physics.CheckSphere(at + Vector3.up * 5f, 4.5f, Everything, QueryTriggerInteraction.Ignore);
        AudioClip[] set = nearStructure && Random.value < 0.7f ? structureEvents : openAirEvents;
        Play3D(set[Random.Range(0, set.Length)], at, 0.8f, 55f, 0.1f);
    }

    private void UpdateSuspense()
    {
        swellTimer -= Time.deltaTime;
        // A roof overhead means the player just went inside a ruin.
        bool sheltered = Physics.Raycast(player.transform.position + Vector3.up, Vector3.up, 12f, Everything, QueryTriggerInteraction.Ignore);
        if (sheltered && !wasSheltered) Swell();
        wasSheltered = sheltered;
    }

    private void UpdateFootsteps()
    {
        int plants = player.FootPlants;
        bool landed = player.IsGrounded && !wasGrounded;
        wasGrounded = player.IsGrounded;

        if (landed) { PlayStep(0.75f, plants); return; }
        if (plants == lastPlant) return;
        lastPlant = plants;
        // Disabled means the Chudail has hold of the player.
        if (player.enabled && player.IsGrounded && player.IsMoving)
            PlayStep(Mathf.Lerp(0.38f, 0.6f, player.Speed01) * Random.Range(0.85f, 1f), plants);
    }

    private void PlayStep(float volume, int plant)
    {
        AudioClip[] set = StepSurface(player.transform.position);
        int pick = Random.Range(0, set.Length);
        if (set[pick] == lastStep) pick = (pick + 1) % set.Length; // the same clip twice is what gives footsteps away
        lastStep = set[pick];
        // Left and right feet land slightly differently.
        steps.pitch = (plant % 2 == 0 ? 1f : 0.96f) * Random.Range(0.97f, 1.03f);
        steps.PlayOneShot(lastStep, volume * footstepVolume);
    }

    private AudioClip[] StepSurface(Vector3 p)
    {
        if (!Physics.Raycast(p + Vector3.up, Vector3.down, out RaycastHit hit, 2.5f, Everything, QueryTriggerInteraction.Ignore)
            || hit.collider is TerrainCollider) return sandSteps;
        return hit.collider.name.ToLowerInvariant().Contains("wood") ? woodSteps : stoneSteps;
    }
}
