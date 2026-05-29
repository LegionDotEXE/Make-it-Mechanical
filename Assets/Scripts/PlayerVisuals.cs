using UnityEngine;

/// <summary>
/// Handles all player visual feedback: sprite, dodge slide, hit flash, death fade.
/// Attach to the Player GameObject (GameBootstrap does this automatically).
/// </summary>
public class PlayerVisuals : MonoBehaviour
{
    [Header("References (auto-created if null)")]
    public SpriteRenderer bodyRenderer;
    public SpriteRenderer shieldRenderer;

    [Header("Colors (dimmed — backdrop behind rhythm lanes)")]
    public Color idleColor      = new Color(0.55f, 0.52f, 0.48f, 0.5f);  // ashen grey, dimmed
    public Color dodgeColor     = new Color(0.3f,  0.45f, 0.7f,  0.6f);  // spectral blue
    public Color perfectColor   = new Color(0.8f,  0.7f,  0.15f, 0.7f);  // gold flash
    public Color hitColor       = new Color(0.7f,  0.1f,  0.08f, 0.7f);  // blood red
    public Color shieldColor    = new Color(0.4f,  0.35f, 0.28f, 0.4f);  // dark iron

    [Header("Dodge Animation")]
    public float dodgeSlideDistance = 1.5f;
    public float dodgeSlideSpeed   = 12f;

    private Vector3 homePosition;
    private Vector3 targetPosition;
    private float flashTimer;
    private Color flashColor;
    private bool isDead;

    void Start()
    {
        if (bodyRenderer == null)
            CreatePlayerSprite();

        homePosition   = transform.position;
        targetPosition = homePosition;

        // subscribe to combat events
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

        // slide toward target position (dodge or return home)
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * dodgeSlideSpeed);

        // flash timer
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            float t = flashTimer / 0.25f;
            bodyRenderer.color = Color.Lerp(idleColor, flashColor, t);
        }
    }

    void CreatePlayerSprite()
    {
        // body - a tall rectangle representing the knight
        GameObject body = new GameObject("Body");
        body.transform.SetParent(transform, false);
        body.transform.localPosition = Vector3.zero;

        bodyRenderer = body.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = CreateRect(0.6f, 1.2f);
        bodyRenderer.color  = idleColor;
        bodyRenderer.sortingOrder = 1;

        // shield - small square on the side
        GameObject shield = new GameObject("Shield");
        shield.transform.SetParent(transform, false);
        shield.transform.localPosition = new Vector3(-0.25f, -0.1f, 0f);

        shieldRenderer = shield.AddComponent<SpriteRenderer>();
        shieldRenderer.sprite = CreateRect(0.15f, 0.4f);
        shieldRenderer.color  = shieldColor;
        shieldRenderer.sortingOrder = 2;

        // head - small circle on top
        GameObject head = new GameObject("Head");
        head.transform.SetParent(transform, false);
        head.transform.localPosition = new Vector3(0f, 0.75f, 0f);

        SpriteRenderer headRenderer = head.AddComponent<SpriteRenderer>();
        headRenderer.sprite = CreateCircle(0.18f);
        headRenderer.color  = idleColor;
        headRenderer.sortingOrder = 2;
    }

    void OnDodge()
    {
        if (isDead) return;
        DodgeDirection dir = CombatManager.Instance.CurrentAttack.requiredDodge;
        float sign = (dir == DodgeDirection.Left) ? -1f : 1f;
        // dodge in the required direction (away from attack)
        targetPosition = homePosition + Vector3.right * sign * dodgeSlideDistance;
        Flash(dodgeColor);

        // return home after a moment
        CancelInvoke(nameof(ReturnHome));
        Invoke(nameof(ReturnHome), 0.35f);
    }

    void OnPerfectDodge()
    {
        if (isDead) return;
        DodgeDirection dir = CombatManager.Instance.CurrentAttack.requiredDodge;
        float sign = (dir == DodgeDirection.Left) ? -1f : 1f;
        targetPosition = homePosition + Vector3.right * sign * dodgeSlideDistance;
        Flash(perfectColor);

        // hold position longer for counter window
        CancelInvoke(nameof(ReturnHome));
        Invoke(nameof(ReturnHome), 0.6f);
    }

    void OnCounter()
    {
        if (isDead) return;
        // quick lunge forward
        Flash(perfectColor);
        targetPosition = homePosition + Vector3.up * 0.5f;
        CancelInvoke(nameof(ReturnHome));
        Invoke(nameof(ReturnHome), 0.3f);
    }

    void OnHit()
    {
        if (isDead) return;
        Flash(hitColor);

        // screen shake effect via small position jitter
        StartCoroutine(HitShake());
    }

    System.Collections.IEnumerator HitShake()
    {
        Vector3 original = homePosition;
        for (int i = 0; i < 6; i++)
        {
            transform.position = original + (Vector3)Random.insideUnitCircle * 0.15f;
            yield return new WaitForSeconds(0.03f);
        }
        transform.position = original;
        targetPosition = homePosition;
    }

    void OnDeath()
    {
        isDead = true;
        bodyRenderer.color = new Color(0.3f, 0.05f, 0.05f, 0.5f);
        if (shieldRenderer != null)
            shieldRenderer.color = new Color(0.2f, 0.2f, 0.2f, 0.3f);

        // fall over
        StartCoroutine(DeathFall());
    }

    System.Collections.IEnumerator DeathFall()
    {
        float t = 0f;
        Quaternion start = transform.rotation;
        Quaternion end   = Quaternion.Euler(0, 0, -90f);
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            transform.rotation = Quaternion.Lerp(start, end, t);
            yield return null;
        }
    }

    void OnStateChanged(CombatState state)
    {
        // pulse the shield when in counter window
        if (shieldRenderer == null) return;

        if (state == CombatState.PerfectWindow)
            shieldRenderer.color = perfectColor;
        else
            shieldRenderer.color = shieldColor;
    }

    void ReturnHome()
    {
        targetPosition = homePosition;
    }

    void Flash(Color color)
    {
        flashColor = color;
        flashTimer = 0.25f;
        bodyRenderer.color = color;
    }

    // ---- sprite generation helpers ----

    public static Sprite CreateRect(float w, float h)
    {
        int pw = Mathf.Max(2, Mathf.RoundToInt(w * 64));
        int ph = Mathf.Max(2, Mathf.RoundToInt(h * 64));
        Texture2D tex = new Texture2D(pw, ph);
        tex.filterMode = FilterMode.Point;
        Color[] pixels = new Color[pw * ph];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
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
