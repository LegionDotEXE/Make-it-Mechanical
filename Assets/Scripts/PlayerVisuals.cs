using System.Collections;
using UnityEngine;

public class PlayerVisuals : MonoBehaviour
{
    [Header("References (auto-created if null)")]
    public SpriteRenderer bodyRenderer;
    public SpriteRenderer shieldRenderer;
    public SpriteRenderer headRenderer;

    [Header("Colors")]
    public Color idleColor    = new Color(0.55f, 0.52f, 0.48f, 0.85f);
    public Color dodgeColor   = new Color(0.3f,  0.5f,  0.9f,  1f);
    public Color perfectColor = new Color(1f,    0.85f, 0.15f, 1f);
    public Color hitColor     = new Color(0.9f,  0.1f,  0.08f, 1f);
    public Color shieldColor  = new Color(0.45f, 0.4f,  0.32f, 0.9f);
    public Color counterColor = new Color(1f,    0.9f,  0.3f,  1f);

    [Header("Dodge Animation")]
    public float dodgeSlideDistance = 1.8f;
    public float dodgeSlideSpeed    = 16f;

    // child renderers we track for multi-part flash
    private SpriteRenderer[] allRenderers;

    private Vector3 homePosition;
    private Vector3 targetPosition;
    private float flashTimer;
    private Color flashColor;
    private bool isDead;

    // ghost trail
    private float ghostTimer;
    private const int GHOST_COUNT = 3;
    private GameObject[] ghosts;

    // idle breathing
    private float breathTimer;

    // boss direction for counter lunge
    private Vector3 bossDirection = Vector3.up;

    void Start()
    {
        if (bodyRenderer == null)
            CreatePlayerSprite();

        homePosition   = transform.position;
        targetPosition = homePosition;

        // build ghost trail objects
        BuildGhosts();

        // find boss for lunge direction
        GameObject boss = GameObject.Find("Boss");
        if (boss != null)
            bossDirection = (boss.transform.position - transform.position).normalized;

        // cache all child renderers for full-body flash
        allRenderers = GetComponentsInChildren<SpriteRenderer>();

        CombatManager.Instance.OnPlayerDodgedSuccessfully += OnDodge;
        CombatManager.Instance.OnPlayerPerfectDodge       += OnPerfectDodge;
        CombatManager.Instance.OnPlayerHit                += OnHit;
        CombatManager.Instance.OnPlayerDeath              += OnDeath;
        CombatManager.Instance.OnCounterLanded            += OnCounter;
        CombatManager.Instance.OnStateChanged             += OnStateChanged;
    }

    void OnDestroy()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnPlayerDodgedSuccessfully -= OnDodge;
            CombatManager.Instance.OnPlayerPerfectDodge       -= OnPerfectDodge;
            CombatManager.Instance.OnPlayerHit                -= OnHit;
            CombatManager.Instance.OnPlayerDeath              -= OnDeath;
            CombatManager.Instance.OnCounterLanded            -= OnCounter;
            CombatManager.Instance.OnStateChanged             -= OnStateChanged;
        }
    }

    void Update()
    {
        if (isDead) return;

        // smooth slide
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * dodgeSlideSpeed);

        // color flash lerp back to idle
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(flashTimer / 0.25f);
            Color c = Color.Lerp(idleColor, flashColor, t);
            if (bodyRenderer != null) bodyRenderer.color = c;
            if (headRenderer  != null) headRenderer.color  = c;
        }

        // idle breathing — subtle scale pulse so the character feels alive
        breathTimer += Time.deltaTime * 1.4f;
        float breath = 1f + Mathf.Sin(breathTimer) * 0.012f;
        transform.localScale = new Vector3(breath, 1f / breath, 1f); // squash-and-stretch

        // ghost trail update
        UpdateGhosts();
    }

    // ---- sprite creation ----

    void CreatePlayerSprite()
    {
        // body
        GameObject body = new GameObject("Body");
        body.transform.SetParent(transform, false);

        bodyRenderer = body.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = CreateRect(0.65f, 1.3f);
        bodyRenderer.color  = idleColor;
        bodyRenderer.sortingOrder = 5;

        // legs — two thin rectangles that give a stance feel
        CreateLimb("LegL", new Vector3(-0.18f, -0.75f, 0f), new Vector3(0.18f, 0.45f, 1f), idleColor, 4);
        CreateLimb("LegR", new Vector3( 0.18f, -0.75f, 0f), new Vector3(0.18f, 0.45f, 1f), idleColor, 4);

        // arm holding shield
        CreateLimb("ArmL", new Vector3(-0.38f, 0.1f, 0f), new Vector3(0.15f, 0.55f, 1f),
            new Color(0.5f, 0.45f, 0.38f, 0.9f), 6);

        // shield
        GameObject shield = new GameObject("Shield");
        shield.transform.SetParent(transform, false);
        shield.transform.localPosition = new Vector3(-0.52f, 0.05f, 0f);

        shieldRenderer = shield.AddComponent<SpriteRenderer>();
        shieldRenderer.sprite = CreateRect(0.18f, 0.55f);
        shieldRenderer.color  = shieldColor;
        shieldRenderer.sortingOrder = 7;

        // head
        GameObject head = new GameObject("Head");
        head.transform.SetParent(transform, false);
        head.transform.localPosition = new Vector3(0f, 0.85f, 0f);

        headRenderer = head.AddComponent<SpriteRenderer>();
        headRenderer.sprite = CreateCircle(0.22f);
        headRenderer.color  = new Color(0.5f, 0.47f, 0.42f, 0.9f);
        headRenderer.sortingOrder = 6;

        // visor slit — gives a knight feel
        GameObject visor = new GameObject("Visor");
        visor.transform.SetParent(transform, false);
        visor.transform.localPosition = new Vector3(0.06f, 0.88f, 0f);

        SpriteRenderer visorSR = visor.AddComponent<SpriteRenderer>();
        visorSR.sprite = CreateRect(0.18f, 0.06f);
        visorSR.color  = new Color(0.15f, 0.12f, 0.08f, 1f);
        visorSR.sortingOrder = 8;
    }

    void CreateLimb(string name, Vector3 pos, Vector3 scale, Color color, int sortOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateRect(1f, 1f);
        sr.color  = color;
        sr.sortingOrder = sortOrder;
    }

    // ---- ghost trail ----

    void BuildGhosts()
    {
        ghosts = new GameObject[GHOST_COUNT];
        for (int i = 0; i < GHOST_COUNT; i++)
        {
            GameObject g = new GameObject($"Ghost_{i}");
            g.transform.SetParent(transform.parent, false);

            SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = CreateRect(0.65f, 1.3f);
            sr.color  = Color.clear;
            sr.sortingOrder = 3;

            ghosts[i] = g;
            g.SetActive(false);
        }
    }

    void UpdateGhosts()
    {
        // only show ghosts while dodging (when we're away from home)
        float distFromHome = Vector3.Distance(transform.position, homePosition);
        bool isDodging = distFromHome > 0.15f;

        for (int i = 0; i < GHOST_COUNT; i++)
        {
            if (!isDodging) { ghosts[i].SetActive(false); continue; }

            ghosts[i].SetActive(true);
            // space ghosts behind the player in the direction of travel
            Vector3 dir = (homePosition - transform.position).normalized;
            ghosts[i].transform.position = transform.position + dir * (i + 1) * 0.22f;

            float alpha = (1f - (float)(i + 1) / (GHOST_COUNT + 1)) * 0.35f;
            SpriteRenderer sr = ghosts[i].GetComponent<SpriteRenderer>();
            Color gc = (CombatManager.Instance.CurrentState == CombatState.PerfectWindow)
                ? perfectColor : dodgeColor;
            sr.color = new Color(gc.r, gc.g, gc.b, alpha);
        }
    }

    // ---- event handlers ----

    void OnDodge()
    {
        if (isDead) return;
        DodgeDirection dir = CombatManager.Instance.CurrentAttack.requiredDodge;
        float sign = (dir == DodgeDirection.Left) ? -1f : 1f;
        targetPosition = homePosition + Vector3.right * sign * dodgeSlideDistance;
        FlashAll(dodgeColor);
        CancelInvoke(nameof(ReturnHome));
        Invoke(nameof(ReturnHome), 0.35f);
    }

    void OnPerfectDodge()
    {
        if (isDead) return;
        DodgeDirection dir = CombatManager.Instance.CurrentAttack.requiredDodge;
        float sign = (dir == DodgeDirection.Left) ? -1f : 1f;
        // snap further and faster on perfect
        targetPosition = homePosition + Vector3.right * sign * (dodgeSlideDistance * 1.5f);
        FlashAll(perfectColor);
        // squash on perfect dodge impact
        StartCoroutine(PerfectSquash());
        CancelInvoke(nameof(ReturnHome));
        Invoke(nameof(ReturnHome), 0.65f);
    }

    IEnumerator PerfectSquash()
    {
        // quick squash-stretch punch on perfect
        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            float s = 1f + Mathf.Sin(t / 0.12f * Mathf.PI) * 0.25f;
            transform.localScale = new Vector3(1f / s, s, 1f);
            yield return null;
        }
    }

    void OnCounter()
    {
        if (isDead) return;
        FlashAll(counterColor);
        targetPosition = homePosition + bossDirection * 0.7f;
        StartCoroutine(CounterBurst());
        CancelInvoke(nameof(ReturnHome));
        Invoke(nameof(ReturnHome), 0.22f);
    }

    IEnumerator CounterBurst()
    {
        // quick scale punch on counter hit
        float t = 0f;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            float s = 1f + Mathf.Sin(t / 0.15f * Mathf.PI) * 0.3f;
            transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    void OnHit()
    {
        if (isDead) return;
        FlashAll(hitColor);
        StartCoroutine(HitShake());
    }

    IEnumerator HitShake()
    {
        // shake and red flash together
        Vector3 original = homePosition;
        for (int i = 0; i < 8; i++)
        {
            float intensity = Mathf.Lerp(0.25f, 0.05f, (float)i / 8f);
            transform.position = original + (Vector3)Random.insideUnitCircle * intensity;
            yield return new WaitForSeconds(0.025f);
        }
        transform.position = original;
        targetPosition = homePosition;
    }

    void OnDeath()
    {
        isDead = true;

        // hide ghosts
        foreach (var g in ghosts) if (g != null) g.SetActive(false);

        // darken all parts
        foreach (var sr in allRenderers)
            sr.color = new Color(0.25f, 0.04f, 0.04f, 0.8f);

        StartCoroutine(DeathSequence());
    }

    IEnumerator DeathSequence()
    {
        // fall and fade
        float t = 0f;
        Quaternion startRot = transform.rotation;
        Quaternion endRot   = Quaternion.Euler(0, 0, -90f);
        Vector3 startPos    = transform.position;

        while (t < 1.2f)
        {
            t += Time.deltaTime * 0.9f;
            float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // ease-out cubic
            transform.rotation = Quaternion.Lerp(startRot, endRot, ease);
            // sink slightly as falling
            transform.position = startPos + Vector3.down * ease * 0.3f;
            // fade out
            float alpha = Mathf.Lerp(0.8f, 0f, Mathf.Clamp01(t - 0.5f));
            foreach (var sr in allRenderers)
            {
                Color c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
            yield return null;
        }
    }

    void OnStateChanged(CombatState state)
    {
        if (shieldRenderer == null) return;
        // gold shield pulse when counter window is open
        shieldRenderer.color = (state == CombatState.PerfectWindow) ? perfectColor : shieldColor;
    }

    void ReturnHome() => targetPosition = homePosition;

    void FlashAll(Color color)
    {
        flashColor = color;
        flashTimer = 0.25f;
        if (bodyRenderer != null) bodyRenderer.color = color;
        if (headRenderer  != null) headRenderer.color  = color;
    }

    // ---- sprite helpers ----

    public static Sprite CreateRect(float w, float h)
    {
        int pw = Mathf.Max(2, Mathf.RoundToInt(w * 64));
        int ph = Mathf.Max(2, Mathf.RoundToInt(h * 64));
        Texture2D tex = new Texture2D(pw, ph);
        tex.filterMode = FilterMode.Point;
        Color[] pixels = new Color[pw * ph];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, pw, ph), new Vector2(0.5f, 0.5f), 64f);
    }

    public static Sprite CreateCircle(float radius)
    {
        int size = Mathf.Max(4, Mathf.RoundToInt(radius * 128));
        Texture2D tex = new Texture2D(size, size);
        tex.filterMode = FilterMode.Point;
        Color[] pixels = new Color[size * size];
        float center = size / 2f;
        float r2 = (size / 2f) * (size / 2f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                pixels[y * size + x] = (dx * dx + dy * dy <= r2) ? Color.white : Color.clear;
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
    }
}
