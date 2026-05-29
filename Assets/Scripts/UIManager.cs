using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Health Bars")]
    public Image bossHealthFill;
    public Image playerHealthFill;

    // ghost bars — white bar behind fill that lags behind for damage effect
    private Image bossGhostFill;
    private Image playerGhostFill;

    private float bossTargetFill   = 1f;
    private float playerTargetFill = 1f;
    private float bossGhostFill_val   = 1f;
    private float playerGhostFill_val = 1f;

    [Header("Screens")]
    public GameObject gameOverPanel;
    public GameObject victoryPanel;

    [Header("Colors")]
    public Color bossBarColor      = new Color(0.85f, 0.15f, 0.1f);
    public Color bossBarLowColor   = new Color(1f,    0.55f, 0.05f); // orange at low HP
    public Color bossBarRageColor  = new Color(1f,    0.1f,  0.6f);  // purple-pink rage
    public Color playerBarColor    = new Color(0.2f,  0.75f, 0.3f);
    public Color playerBarLowColor = new Color(0.9f,  0.2f,  0.1f);  // red when low
    public Color barBgColor        = new Color(0.12f, 0.1f,  0.08f);
    public Color ghostBarColor     = new Color(1f,    1f,    1f,    0.4f);

    // damage number pool
    private Transform dmgNumberParent;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        CombatManager.Instance.OnPlayerDeath  += () => StartCoroutine(ShowPanelDelayed(gameOverPanel, 1.2f));
        CombatManager.Instance.OnBossDefeated += () => StartCoroutine(ShowPanelDelayed(victoryPanel,  1.8f));

        // boss rage — change bar color
        BossController boss = FindAnyObjectByType<BossController>();
        if (boss != null)
            boss.OnRageEntered.AddListener(OnBossRage);

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel  != null) victoryPanel.SetActive(false);
    }

    void Update()
    {
        // smooth fill animation
        if (bossHealthFill != null)
        {
            bossHealthFill.fillAmount = Mathf.Lerp(
                bossHealthFill.fillAmount, bossTargetFill, Time.deltaTime * 8f);

            // ghost bar lags further behind — damage chunk visible
            bossGhostFill_val = Mathf.Lerp(bossGhostFill_val, bossTargetFill, Time.deltaTime * 2f);
            if (bossGhostFill != null) bossGhostFill.fillAmount = bossGhostFill_val;

            // low health color shift
            float bt = bossHealthFill.fillAmount;
            if (!isBossEnraged)
                bossHealthFill.color = Color.Lerp(bossBarLowColor, bossBarColor, Mathf.Clamp01((bt - 0.2f) / 0.4f));
        }

        if (playerHealthFill != null)
        {
            playerHealthFill.fillAmount = Mathf.Lerp(
                playerHealthFill.fillAmount, playerTargetFill, Time.deltaTime * 8f);

            playerGhostFill_val = Mathf.Lerp(playerGhostFill_val, playerTargetFill, Time.deltaTime * 2f);
            if (playerGhostFill != null) playerGhostFill.fillAmount = playerGhostFill_val;

            // pulse red when below 30%
            float pt = playerHealthFill.fillAmount;
            if (pt < 0.3f)
            {
                float pulse = (Mathf.Sin(Time.time * 4f) + 1f) / 2f;
                playerHealthFill.color = Color.Lerp(playerBarLowColor, new Color(1f, 0.5f, 0.4f), pulse);
            }
            else
            {
                playerHealthFill.color = Color.Lerp(playerBarLowColor, playerBarColor,
                    Mathf.Clamp01((pt - 0.2f) / 0.4f));
            }
        }
    }

    // ---- public API ----

    public void UpdateBossHealth(float normalized)
    {
        bossTargetFill = normalized;
    }

    public void UpdatePlayerHealth(float normalized)
    {
        playerTargetFill = normalized;
        // snap ghost immediately on first hit if full
        if (normalized >= 0.99f) playerGhostFill_val = 1f;
    }

    public void SpawnDamageNumber(float damage, Vector3 worldPos, bool isBoss)
    {
        if (dmgNumberParent == null) return;

        GameObject go = new GameObject("DmgNum");
        go.transform.SetParent(dmgNumberParent, false);

        // convert world to screen to canvas
        Camera cam = Camera.main;
        Vector2 screenPos = cam.WorldToScreenPoint(worldPos);
        RectTransform canvasRect = dmgNumberParent.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPos, null, out Vector2 localPos);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = localPos;
        rt.sizeDelta = new Vector2(120, 50);

        Text txt = go.AddComponent<Text>();
        txt.text      = isBoss ? $"-{(int)damage}" : $"-{(int)damage}";
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = isBoss ? 38 : 32;
        txt.fontStyle = FontStyle.Bold;
        txt.color     = isBoss
            ? new Color(1f, 0.85f, 0.2f)   // gold for boss damage
            : new Color(1f, 0.2f, 0.1f);   // red for player damage
        txt.alignment = TextAnchor.MiddleCenter;

        Outline ol = go.AddComponent<Outline>();
        ol.effectColor    = Color.black;
        ol.effectDistance = new Vector2(2, -2);

        StartCoroutine(AnimateDamageNumber(rt, txt));
    }

    IEnumerator AnimateDamageNumber(RectTransform rt, Text txt)
    {
        float t = 0f;
        Vector2 startPos = rt.anchoredPosition;
        while (t < 0.9f)
        {
            t += Time.deltaTime;
            // float upward
            rt.anchoredPosition = startPos + Vector2.up * t * 60f;
            // fade out in second half
            Color c = txt.color;
            c.a = Mathf.Clamp01(1f - (t - 0.4f) / 0.5f);
            txt.color = c;
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    // ---- rage ----

    private bool isBossEnraged = false;

    void OnBossRage()
    {
        isBossEnraged = true;
        if (bossHealthFill != null)
            bossHealthFill.color = bossBarRageColor;
        if (bossGhostFill != null)
            bossGhostFill.color = new Color(bossBarRageColor.r, bossBarRageColor.g, bossBarRageColor.b, 0.3f);

        // flash the boss bar
        StartCoroutine(RageBarFlash());
    }

    IEnumerator RageBarFlash()
    {
        for (int i = 0; i < 6; i++)
        {
            if (bossHealthFill != null)
                bossHealthFill.color = Color.white;
            yield return new WaitForSeconds(0.07f);
            if (bossHealthFill != null)
                bossHealthFill.color = bossBarRageColor;
            yield return new WaitForSeconds(0.07f);
        }
    }

    // ---- end screens ----

    IEnumerator ShowPanelDelayed(GameObject panel, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (panel == null) yield break;

        panel.SetActive(true);

        // fade in
        Image bg = panel.GetComponent<Image>();
        if (bg != null)
        {
            Color target = bg.color;
            bg.color = new Color(target.r, target.g, target.b, 0f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 1.5f;
                bg.color = new Color(target.r, target.g, target.b, Mathf.Clamp01(t) * target.a);
                yield return null;
            }
        }

        // text children pop in one by one
        Text[] texts = panel.GetComponentsInChildren<Text>();
        foreach (Text tx in texts)
        {
            Color tc = tx.color;
            tx.color = new Color(tc.r, tc.g, tc.b, 0f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 3f;
                tx.color = new Color(tc.r, tc.g, tc.b, Mathf.Clamp01(t));
                yield return null;
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    // ---- programmatic UI creation (called by GameBootstrap) ----

    public static UIManager CreateUI()
    {
        GameObject canvasGO = new GameObject("CombatCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();

        UIManager ui = canvasGO.AddComponent<UIManager>();
        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();

        // boss health bar (top)
        ui.bossHealthFill = CreateHealthBar(canvasRect, "BossHP",
            new Vector2(0f, 1f), new Vector2(0.5f, 1f),
            new Vector2(60, -50), new Vector2(-60, -20),
            ui.bossBarColor, ui.barBgColor, "BOSS",
            out ui.bossGhostFill);

        // player health bar (bottom)
        ui.playerHealthFill = CreateHealthBar(canvasRect, "PlayerHP",
            new Vector2(0f, 0f), new Vector2(0.5f, 0f),
            new Vector2(60, 20), new Vector2(-60, 55),
            ui.playerBarColor, ui.barBgColor, "YOU",
            out ui.playerGhostFill);

        // rhythm lanes
        RhythmLaneManager lanes = canvasGO.AddComponent<RhythmLaneManager>();
        lanes.Initialize(canvasRect);

        // damage number parent (above lanes, below panels)
        GameObject dmgParent = new GameObject("DamageNumbers");
        dmgParent.transform.SetParent(canvasGO.transform, false);
        RectTransform dmgRT = dmgParent.AddComponent<RectTransform>();
        dmgRT.anchorMin = Vector2.zero;
        dmgRT.anchorMax = Vector2.one;
        dmgRT.offsetMin = Vector2.zero;
        dmgRT.offsetMax = Vector2.zero;
        ui.dmgNumberParent = dmgParent.transform;

        // game over / victory panels
        ui.gameOverPanel = CreateOverlayPanel(canvasRect, "GameOverPanel",
            "YOU DIED", new Color(0.7f, 0.05f, 0.05f), new Color(0, 0, 0, 0.88f));

        ui.victoryPanel = CreateOverlayPanel(canvasRect, "VictoryPanel",
            "HEIR OF FIRE DESTROYED", new Color(1f, 0.85f, 0.2f), new Color(0, 0, 0, 0.88f));

        return ui;
    }

    // ---- helpers ----

    static Image CreateHealthBar(RectTransform parent, string name,
        Vector2 anchor, Vector2 pivot,
        Vector2 offsetMin, Vector2 offsetMax,
        Color fillColor, Color bgColor, string label,
        out Image ghostFill)
    {
        // background
        GameObject bg = new GameObject(name + "_BG");
        bg.transform.SetParent(parent, false);

        RectTransform bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, anchor.y);
        bgRect.anchorMax = new Vector2(1, anchor.y);
        bgRect.pivot     = pivot;
        bgRect.offsetMin = offsetMin;
        bgRect.offsetMax = offsetMax;

        Image bgImage = bg.AddComponent<Image>();
        bgImage.color = bgColor;

        // ghost fill (white, behind main fill — shows damage chunk)
        GameObject ghost = new GameObject(name + "_Ghost");
        ghost.transform.SetParent(bg.transform, false);
        RectTransform ghostRT = ghost.AddComponent<RectTransform>();
        ghostRT.anchorMin = Vector2.zero;
        ghostRT.anchorMax = Vector2.one;
        ghostRT.offsetMin = new Vector2(4, 4);
        ghostRT.offsetMax = new Vector2(-4, -4);
        ghostFill = ghost.AddComponent<Image>();
        ghostFill.color      = new Color(1f, 1f, 1f, 0.4f);
        ghostFill.type       = Image.Type.Filled;
        ghostFill.fillMethod = Image.FillMethod.Horizontal;
        ghostFill.fillAmount = 1f;

        // main fill (on top of ghost)
        GameObject fill = new GameObject(name + "_Fill");
        fill.transform.SetParent(bg.transform, false);
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(4, 4);
        fillRect.offsetMax = new Vector2(-4, -4);

        Image fillImage = fill.AddComponent<Image>();
        fillImage.color      = fillColor;
        fillImage.type       = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = 1f;

        // label
        GameObject labelGO = new GameObject(name + "_Label");
        labelGO.transform.SetParent(bg.transform, false);
        RectTransform labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10, 0);
        labelRect.offsetMax = new Vector2(-10, 0);

        Text labelText = labelGO.AddComponent<Text>();
        labelText.text      = label;
        labelText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize  = 18;
        labelText.fontStyle = FontStyle.Bold;
        labelText.color     = new Color(0.9f, 0.85f, 0.75f);
        labelText.alignment = TextAnchor.MiddleLeft;

        Outline outline = labelGO.AddComponent<Outline>();
        outline.effectColor    = Color.black;
        outline.effectDistance = new Vector2(1, -1);

        return fillImage;
    }

    static Text CreateText(Transform parent, string name, Vector2 anchorPos,
        int fontSize, FontStyle style, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorPos;
        rect.anchorMax = anchorPos;
        rect.pivot     = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(800, 80);

        Text text = go.AddComponent<Text>();
        text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize  = fontSize;
        text.fontStyle = style;
        text.color     = color;
        text.alignment = TextAnchor.MiddleCenter;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor    = Color.black;
        outline.effectDistance = new Vector2(2, -2);

        return text;
    }

    static GameObject CreateOverlayPanel(RectTransform parent, string name,
        string message, Color textColor, Color bgColor)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image bg = panel.AddComponent<Image>();
        bg.color = bgColor;

        Text text = CreateText(panel.transform, name + "_Text",
            new Vector2(0.5f, 0.55f), 72, FontStyle.Bold, textColor);
        text.text = message;

        Text sub = CreateText(panel.transform, name + "_Sub",
            new Vector2(0.5f, 0.4f), 28, FontStyle.Normal, new Color(0.7f, 0.65f, 0.55f));
        sub.text = "Press R to restart";

        panel.SetActive(false);
        return panel;
    }
}
