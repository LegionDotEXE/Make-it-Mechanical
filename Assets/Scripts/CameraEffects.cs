using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Camera shake and full-screen flash effects.
/// GameBootstrap adds this automatically — no manual setup needed.
/// </summary>
public class CameraEffects : MonoBehaviour
{
    public static CameraEffects Instance { get; private set; }

    // screen flash overlay
    private Image flashOverlay;
    private float flashTimer;
    private Color flashColor;

    private Camera cam;
    private Vector3 camHomePos;
    private bool isShaking;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        cam        = Camera.main;
        camHomePos = cam.transform.position;

        BuildFlashOverlay();

        CombatManager.Instance.OnPlayerHit          += () => Shake(0.18f, 0.22f);
        CombatManager.Instance.OnPlayerHit          += () => Flash(new Color(0.8f, 0.05f, 0.05f, 0.35f), 0.18f);
        CombatManager.Instance.OnCounterLanded      += () => Shake(0.28f, 0.28f);
        CombatManager.Instance.OnCounterLanded      += () => Flash(new Color(1f, 0.9f, 0.2f, 0.4f), 0.22f);
        CombatManager.Instance.OnPlayerPerfectDodge += () => Flash(new Color(0.3f, 0.6f, 1f, 0.25f), 0.15f);
        CombatManager.Instance.OnPlayerDeath        += () => Flash(new Color(0.6f, 0f, 0f, 0.6f), 0.6f);
        CombatManager.Instance.OnBossDefeated       += () => Flash(new Color(1f, 0.9f, 0.4f, 0.7f), 0.8f);
    }

    void OnDestroy()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnPlayerHit          -= () => Shake(0.18f, 0.22f);
            CombatManager.Instance.OnPlayerHit          -= () => Flash(new Color(0.8f, 0.05f, 0.05f, 0.35f), 0.18f);
            CombatManager.Instance.OnCounterLanded      -= () => Shake(0.28f, 0.28f);
            CombatManager.Instance.OnCounterLanded      -= () => Flash(new Color(1f, 0.9f, 0.2f, 0.4f), 0.22f);
            CombatManager.Instance.OnPlayerPerfectDodge -= () => Flash(new Color(0.3f, 0.6f, 1f, 0.25f), 0.15f);
            CombatManager.Instance.OnPlayerDeath        -= () => Flash(new Color(0.6f, 0f, 0f, 0.6f), 0.6f);
            CombatManager.Instance.OnBossDefeated       -= () => Flash(new Color(1f, 0.9f, 0.4f, 0.7f), 0.8f);
        }
    }

    void Update()
    {
        // flash fade
        if (flashTimer > 0f && flashOverlay != null)
        {
            flashTimer -= Time.deltaTime;
            Color c = flashColor;
            c.a = Mathf.Clamp01(flashTimer / 0.15f) * flashColor.a;
            flashOverlay.color = c;
        }
    }

    // ---- public API ----

    public void Shake(float magnitude, float duration)
    {
        if (!isShaking)
            StartCoroutine(DoShake(magnitude, duration));
    }

    public void Flash(Color color, float duration)
    {
        flashColor = color;
        flashTimer = duration;
        if (flashOverlay != null)
            flashOverlay.color = color;
    }

    // ---- internals ----

    IEnumerator DoShake(float magnitude, float duration)
    {
        isShaking = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float dampen = 1f - Mathf.Clamp01(elapsed / duration);
            float x = Random.Range(-1f, 1f) * magnitude * dampen;
            float y = Random.Range(-1f, 1f) * magnitude * dampen;
            cam.transform.position = camHomePos + new Vector3(x, y, 0f);
            yield return null;
        }

        cam.transform.position = camHomePos;
        isShaking = false;
    }

    void BuildFlashOverlay()
    {
        // find or create a canvas for the overlay
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject go = new GameObject("ScreenFlash");
        go.transform.SetParent(canvas.transform, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        flashOverlay = go.AddComponent<Image>();
        flashOverlay.color        = Color.clear;
        flashOverlay.raycastTarget = false;

        // push to very front
        Canvas c = go.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder    = 999;
    }
}
