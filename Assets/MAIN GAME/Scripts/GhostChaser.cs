using UnityEngine;

// The Chudail. Keep this object inactive in the scene. Hidden most of the time; shows herself at a distance (watching or
// wandering), vanishes once you look away, and hunts on detection or when a relic is taken. Passes through walls by design.
public class GhostChaser : MonoBehaviour
{
    private enum State { Hidden, Watch, Stalk, Notice, Chase, Attack }

    // Root speed of Walk.anim / Run.anim: animator speed scales from these so her feet don't slide.
    private const float WalkClipSpeed = 0.54f, RunClipSpeed = 2.54f;
    private const int Everything = ~0;

    [SerializeField] private float speed = 3.6f;      // between walk (2.6) and sprint (4.8): sprinting escapes
    [SerializeField] private float chaseTime = 20f;
    [SerializeField] private float spawnDistance = 25f;
    [SerializeField] private float catchDistance = 1.6f;

    [Header("Stalking")]
    [SerializeField] private float walkSpeed = 0.7f;
    [SerializeField] private float detectRange = 11f;
    [SerializeField] private float detectedChaseTime = 8f;
    [SerializeField] private Vector2 hiddenTime = new Vector2(25f, 55f);
    [SerializeField] private float attackTime = 1.1f;

    private State state;
    private Transform target, head;
    private Animator anim;
    private Renderer body;
    private Camera cam;
    private PlayerController heldPlayer;
    private Quaternion heldCamRotation;
    private bool stalking;
    private float timer, seenTime, pauseLeft, walkLeft, chaseClock, jerkTimer;
    private Vector3 waypoint, faceAxis;
    private AudioSource loop;
    private float stepDistance, vocalTimer;
    private Quaternion jerk = Quaternion.identity, jerkTarget = Quaternion.identity;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        body = GetComponentInChildren<Renderer>();
        head = anim.GetBoneTransform(HumanBodyBones.Head);
        faceAxis = Quaternion.Inverse(head.rotation) * transform.forward; // still in bind pose here
        loop = gameObject.AddComponent<AudioSource>();
        loop.loop = true;
        loop.playOnAwake = false;
        loop.spatialBlend = 1f;
        loop.dopplerLevel = 0f;
        loop.rolloffMode = AudioRolloffMode.Linear;
        loop.minDistance = 2f;
        loop.maxDistance = 45f;
    }

    // Quiet between-relic phase: occasional sightings, no threat unless she notices the player.
    public void StartStalk(Transform player)
    {
        Wake(player);
        stalking = true;
        Hide();
    }

    public void StartChase(Transform player)
    {
        Wake(player);
        transform.position = player.position - player.forward * spawnDistance;
        SnapToGround();
        HorrorAudio.Instance.Play3D(HorrorAudio.Instance.detect, transform.position, 1f, 60f);
        BeginChase(chaseTime);
    }

    public void Stop()
    {
        if (heldPlayer)
        {
            cam.transform.localRotation = heldCamRotation;
            heldPlayer.enabled = true;
            heldPlayer = null;
        }
        stalking = false;
        gameObject.SetActive(false);
    }

    private void Wake(Transform player)
    {
        gameObject.SetActive(true);
        target = player;
        cam = Camera.main;
    }

    private void Update()
    {
        if (state == State.Watch || state == State.Stalk || state == State.Chase) UpdateVocals();

        switch (state)
        {
            case State.Hidden:
                timer -= Time.deltaTime;
                if (timer <= 0f) Appear();
                break;

            case State.Watch:
                Face(target.position, 90f);
                timer -= Time.deltaTime;
                if (body.isVisible) seenTime += Time.deltaTime;
                // Vanishing the moment you look away is scarier than vanishing in plain sight.
                if (timer <= 0f || (seenTime > 1.2f && !body.isVisible)) Hide();
                else Detect();
                break;

            case State.Stalk:
                UpdateStalk();
                break;

            case State.Notice:
                Face(target.position, 240f);
                timer -= Time.deltaTime;
                if (timer <= 0f) BeginChase(detectedChaseTime);
                break;

            case State.Chase:
                UpdateChase();
                break;

            case State.Attack:
                UpdateAttack();
                break;
        }
    }

    private void Appear()
    {
        float roll = Random.value;
        bool flash = roll < 0.15f; // brief glimpse, closer than usual, harmless
        if (!FindSpot(flash ? 14f : 22f, flash ? 18f : 35f, out Vector3 spot))
        {
            timer = 5f;
            return;
        }
        transform.position = spot;
        transform.rotation = Quaternion.LookRotation(Flat(target.position - spot));
        body.enabled = true;
        seenTime = 0f;
        vocalTimer = Random.Range(2f, 6f);
        if (flash) HorrorAudio.Instance.Sting(0.45f);
        else
        {
            HorrorAudio.Instance.Swell();
            SetLoop(HorrorAudio.Instance.breathLoop, 0.8f);
        }

        if (roll < 0.6f)
        {
            state = State.Watch;
            timer = flash ? 0.6f : 12f;
            PlayIdle();
        }
        else
        {
            state = State.Stalk;
            timer = 16f;
            pauseLeft = 0f;
            walkLeft = Random.Range(3f, 7f);
            float side = Random.value < 0.5f ? -1f : 1f;
            waypoint = spot + transform.right * side * Random.Range(8f, 14f);
            Play("Walk", walkSpeed / WalkClipSpeed);
        }
    }

    private void Hide()
    {
        state = State.Hidden;
        timer = Random.Range(hiddenTime.x, hiddenTime.y);
        body.enabled = false;
        loop.Stop();
    }

    private void UpdateStalk()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f || Flat(waypoint - transform.position).magnitude < 0.5f) { Hide(); return; }
        if (Detect()) return;

        if (pauseLeft > 0f)
        {
            pauseLeft -= Time.deltaTime;
            Face(target.position, 60f); // stops and turns to look at you
            if (pauseLeft <= 0f)
            {
                walkLeft = Random.Range(3f, 7f);
                Play("Walk", walkSpeed / WalkClipSpeed);
            }
            return;
        }

        walkLeft -= Time.deltaTime;
        if (walkLeft <= 0f)
        {
            pauseLeft = Random.Range(1f, 3f);
            PlayIdle();
            return;
        }
        Face(waypoint, 90f);
        Move(transform.forward, walkSpeed);
    }

    private bool Detect()
    {
        if (Flat(target.position - transform.position).magnitude > detectRange || !HasLineOfSight(transform.position)) return false;
        state = State.Notice;
        timer = 1f;
        loop.Stop();
        HorrorAudio.Instance.Play3D(HorrorAudio.Instance.detect, transform.position, 1f, 60f);
        HorrorAudio.Instance.Sting(0.6f);
        Play("Turn", 1.4f);
        return true;
    }

    private void BeginChase(float duration)
    {
        state = State.Chase;
        timer = duration;
        chaseClock = Random.value * 10f;
        body.enabled = true;
        vocalTimer = Random.Range(3f, 7f);
        SetLoop(HorrorAudio.Instance.chaseLoop, 1f);
        Play("Run", speed / RunClipSpeed);
    }

    private void UpdateChase()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            if (stalking) Hide(); else Stop();
            return;
        }

        Vector3 to = Flat(target.position - transform.position);
        if (to.magnitude < catchDistance) { BeginAttack(); return; }

        // Lurching pace; peak stays under sprint speed so escaping remains possible.
        chaseClock += Time.deltaTime;
        float pace = speed * (1f + 0.15f * Mathf.Sin(chaseClock * 2.3f));
        anim.speed = pace / RunClipSpeed;
        Face(target.position, 240f);
        Move(to.normalized, pace);
    }

    private void BeginAttack()
    {
        state = State.Attack;
        timer = attackTime;
        loop.Stop();
        HorrorAudio.Instance.Play2D(HorrorAudio.Instance.attack, 1f);
        HorrorAudio.Instance.Sting(1f);
        heldPlayer = target.GetComponent<PlayerController>();
        heldPlayer.enabled = false;
        heldCamRotation = cam.transform.localRotation;
        anim.speed = 1.2f;
        anim.CrossFadeInFixedTime("Attack", 0.1f, 0, 0.3f); // ends before the clip's deep crouch, so she stays upright in your face
    }

    private void UpdateAttack()
    {
        timer -= Time.deltaTime;
        Face(target.position, 720f);
        Vector3 to = Flat(target.position - transform.position);
        if (to.magnitude > 1.1f) Move(to.normalized, 4f);
        Vector3 look = head.position - cam.transform.position;
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(look), 1f - Mathf.Exp(-14f * Time.deltaTime));
        if (timer <= 0f) GameManager.Instance.PlayerCaught(); // calls Stop, which gives the player back
    }

    private void LateUpdate()
    {
        if (state == State.Hidden) return;
        if (state == State.Attack) { head.rotation = HeadLook(1f) * head.rotation; return; } // her face fills the screen

        // Sudden, wrong-looking head snaps layered on top of the animation.
        jerkTimer -= Time.deltaTime;
        if (jerkTimer <= 0f)
        {
            jerkTimer = Random.Range(0.8f, 3.5f);
            jerkTarget = Random.value < 0.5f ? Quaternion.identity
                : Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(-30f, 30f), Random.Range(-25f, 25f));
        }
        jerk = Quaternion.Slerp(jerk, jerkTarget, 1f - Mathf.Exp(-12f * Time.deltaTime));

        Quaternion look = state == State.Watch || state == State.Notice ? HeadLook(0.7f) : Quaternion.identity;
        head.rotation = look * jerk * head.rotation;
    }

    // World-space turn that points her face at the player's camera.
    private Quaternion HeadLook(float weight) =>
        Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(head.rotation * faceAxis, cam.transform.position - head.position), weight);

    // A random whisper or laugh at spaced-out intervals, so her voice stays a surprise.
    private void UpdateVocals()
    {
        vocalTimer -= Time.deltaTime;
        if (vocalTimer > 0f) return;
        vocalTimer = Random.Range(6f, 14f);
        AudioClip[] vocals = HorrorAudio.Instance.vocals;
        HorrorAudio.Instance.Play3D(vocals[Random.Range(0, vocals.Length)], transform.position + Vector3.up * 1.4f, 1f, 45f);
    }

    private void SetLoop(AudioClip clip, float volume)
    {
        loop.clip = clip;
        loop.volume = volume;
        loop.Play();
    }

    private void Move(Vector3 direction, float metersPerSecond)
    {
        bool running = state == State.Chase;
        stepDistance += metersPerSecond * Time.deltaTime;
        if (stepDistance >= (running ? 1.35f : 0.6f))
        {
            stepDistance = 0f;
            AudioClip[] steps = running ? HorrorAudio.Instance.runSteps : HorrorAudio.Instance.walkSteps;
            HorrorAudio.Instance.Play3D(steps[Random.Range(0, steps.Length)], transform.position, running ? 1f : 0.7f, running ? 35f : 22f, 0.08f);
        }

        Vector3 next = transform.position + direction * (metersPerSecond * Time.deltaTime);
        next.y = Mathf.MoveTowards(transform.position.y, GroundY(next), 4f * Time.deltaTime);
        transform.position = next;
    }

    private void Face(Vector3 point, float degreesPerSecond)
    {
        Vector3 to = Flat(point - transform.position);
        if (to.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), degreesPerSecond * Time.deltaTime);
    }

    private void Play(string animState, float animSpeed)
    {
        anim.speed = animSpeed;
        anim.CrossFadeInFixedTime(animState, 0.3f);
    }

    private void PlayIdle()
    {
        float roll = Random.value;
        Play(roll < 0.4f ? "IdleA" : roll < 0.75f ? "IdleB" : "Agonize", 1f);
    }

    private void SnapToGround()
    {
        Vector3 p = transform.position;
        p.y = GroundY(p);
        transform.position = p;
    }

    // Ray starts near player height so roofs above don't count as ground; terrain covers misses.
    private float GroundY(Vector3 at)
    {
        Terrain t = Terrain.activeTerrain;
        float terrainY = t ? t.SampleHeight(at) + t.transform.position.y : target.position.y;
        Vector3 from = new Vector3(at.x, Mathf.Max(terrainY, target.position.y) + 1.5f, at.z);
        return Physics.Raycast(from, Vector3.down, out RaycastHit hit, 8f, Everything, QueryTriggerInteraction.Ignore) ? hit.point.y : terrainY;
    }

    // Ray runs eye -> her because it starts inside the player's capsule, which queries ignore.
    private bool HasLineOfSight(Vector3 feet) =>
        !Physics.Linecast(cam.transform.position, feet + Vector3.up * 1.5f, Everything, QueryTriggerInteraction.Ignore);

    // Somewhere the player can see, ahead of them; prefers beside cover so she seems to step out from behind it.
    private bool FindSpot(float minDistance, float maxDistance, out Vector3 spot)
    {
        Vector3 forward = Flat(cam.transform.forward).normalized;
        bool found = false;
        spot = Vector3.zero;
        for (int i = 0; i < 10; i++)
        {
            Vector3 p = target.position + Quaternion.Euler(0f, Random.Range(-55f, 55f), 0f) * forward * Random.Range(minDistance, maxDistance);
            p.y = GroundY(p);
            if (Mathf.Abs(p.y - target.position.y) > 4f) continue;
            if (Physics.CheckSphere(p + Vector3.up, 0.4f, Everything, QueryTriggerInteraction.Ignore)) continue; // inside geometry
            if (!HasLineOfSight(p)) continue;
            spot = p;
            found = true;
            if (Physics.CheckSphere(p + Vector3.up * 2.2f, 1.8f, Everything, QueryTriggerInteraction.Ignore)) return true;
        }
        return found;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
