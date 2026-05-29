using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Combat UI rebuilt around the rhythm lane overlay.
/// Health bars live at the edges, the RhythmLaneManager owns the center.
/// Game over / victory screens overlay everything.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Health Bars")]
    public Image bossHealthFill;
    public Image playerHealthFill;

    [Header("Screens")]
    public GameObject gameOverPanel;
    public GameObject victoryPanel;

    [Header("Colors")]
    public Color bossBarColor   = new Color(0.8f, 0.15f, 0.1f);
    public Color playerBarColor = new Color(0.2f, 0.7f, 0.3f);
    public Color barBgColor     = new Color(0.15f, 0.12f, 0.1f);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        CombatManager.Instance.OnPlayerDeath  += ShowGameOver;
        CombatManager.Instance.OnBossDefeated += ShowVictory;

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel  != null) victoryPanel.SetActive(false);
    }

    // --- public API ---

    public void UpdateBossHealth(float normalized)
    {
        if (bossHealthFill != null)
            bossHealthFill.fillAmount = normalized;
    }

    public void UpdatePlayerHealth(float normalized)
    {
        if (playerHealthFill != null)
            playerHealthFill.fillAmount = normalized;
    }

    // --- end screens ---

    void ShowGameOver()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    void ShowVictory()
    {
        if (victoryPanel != null) victoryPanel.SetActive(true);
    }

    // ----- programmatic UI creation (called by GameBootstrap) -----

    public static UIManager CreateUI()
    {
        // Canvas
        GameObject canvasGO = new GameObject("CombatCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();

        UIManager ui = canvasGO.AddComponent<UIManager>();
        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();

        // ----- BOSS HEALTH BAR (top) -----
        ui.bossHealthFill = CreateHealthBar(canvasRect, "BossHP",
            new Vector2(0f, 1f), new Vector2(0.5f, 1f),
            new Vector2(60, -40), new Vector2(-60, -20),
            ui.bossBarColor, ui.barBgColor, "BOSS");

        // ----- PLAYER HEALTH BAR (bottom) -----
        ui.playerHealthFill = CreateHealthBar(canvasRect, "PlayerHP",
            new Vector2(0f, 0f), new Vector2(0.5f, 0f),
            new Vector2(60, 30), new Vector2(-60, 50),
            ui.playerBarColor, ui.barBgColor, "YOU");

        // ----- RHYTHM LANES (center — the main event) -----
        RhythmLaneManager lanes = canvasGO.AddComponent<RhythmLaneManager>();
        lanes.Initialize(canvasRect);

        // ----- GAME OVER PANEL -----
        ui.gameOverPanel = CreateOverlayPanel(canvasRect, "GameOverPanel",
            "YOU DIED", new Color(0.6f, 0.05f, 0.05f), new Color(0, 0, 0, 0.85f));

        // ----- VICTORY PANEL -----
        ui.victoryPanel = CreateOverlayPanel(canvasRect, "VictoryPanel",
            "HEIR OF FIRE DESTROYED", new Color(1f, 0.85f, 0.2f), new Color(0, 0, 0, 0.85f));

        return ui;
    }

    // ----- helpers -----

    static Image CreateHealthBar(RectTransform parent, string name,
        Vector2 anchor, Vector2 pivot,
        Vector2 offsetMin, Vector2 offsetMax,
        Color fillColor, Color bgColor, string label)
    {
        // background
        GameObject bg = new GameObject(name + "_BG");
        bg.transform.SetParent(parent, false);

        RectTransform bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, anchor.y);
        bgRect.anchorMax = new Vector2(1, anchor.y);
        bgRect.pivot     = pivot;
        bgRect.offsetMin = new Vector2(offsetMin.x, offsetMin.y);
        bgRect.offsetMax = new Vector2(offsetMax.x, offsetMax.y);

        Image bgImage = bg.AddComponent<Image>();
        bgImage.color = bgColor;

        // fill
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
        outline.effectColor   = Color.black;
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
        outline.effectColor   = Color.black;
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

        // main text
        Text text = CreateText(panel.transform, name + "_Text",
            new Vector2(0.5f, 0.55f), 72, FontStyle.Bold, textColor);
        text.text = message;

        // subtitle
        Text sub = CreateText(panel.transform, name + "_Sub",
            new Vector2(0.5f, 0.4f), 28, FontStyle.Normal, new Color(0.7f, 0.65f, 0.55f));
        sub.text = "Press R to restart";

        panel.SetActive(false);
        return panel;
    }
}
