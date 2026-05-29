using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private Image playerFill;
    private Image bossFill;
    private Text  playerHPText;
    private Text  bossHPText;

    private float playerMax    = 100f;
    private float bossMax      = 200f;
    private float playerTarget = 1f;
    private float bossTarget   = 1f;

    private bool bossEnraged = false;

    private GameObject   gameOverPanel;
    private GameObject   victoryPanel;
    private RectTransform dmgParent;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) playerMax = pc.maxHealth;

        var bc = FindAnyObjectByType<BossController>();
        if (bc != null) bossMax = bc.maxHealth;

        CombatManager.Instance.OnPlayerDeath  += () => StartCoroutine(ShowPanel(gameOverPanel, 1.1f));
        CombatManager.Instance.OnBossDefeated += () => StartCoroutine(ShowPanel(victoryPanel,  1.8f));
    }

    void Update()
    {
        if (playerFill != null)
        {
            playerFill.fillAmount = Mathf.Lerp(
                playerFill.fillAmount, playerTarget, Time.deltaTime * 10f);

            float pct = playerFill.fillAmount;
            playerFill.color = (pct < 0.3f)
                ? Color.Lerp(
                    new Color(0.95f, 0.18f, 0.1f),
                    new Color(1f, 0.5f, 0.4f),
                    (Mathf.Sin(Time.time * 5f) + 1f) * 0.5f)
                : Color.Lerp(
                    new Color(0.95f, 0.18f, 0.1f),
                    new Color(0.18f, 0.78f, 0.28f),
                    Mathf.Clamp01((pct - 0.2f) / 0.5f));

            if (playerHPText != null)
                playerHPText.text =
                    $"{Mathf.CeilToInt(playerTarget * playerMax)}  /  {(int)playerMax}";
        }

        if (bossFill != null)
        {
            bossFill.fillAmount = Mathf.Lerp(
                bossFill.fillAmount, bossTarget, Time.deltaTime * 10f);

            if (!bossEnraged)
                bossFill.color = Color.Lerp(
                    new Color(1f, 0.45f, 0.05f),
                    new Color(0.85f, 0.14f, 0.1f),
                    Mathf.Clamp01((bossFill.fillAmount - 0.15f) / 0.35f));

            if (bossHPText != null)
                bossHPText.text =
                    $"{Mathf.CeilToInt(bossTarget * bossMax)}  /  {(int)bossMax}";
        }
    }


    public void UpdatePlayerHealth(float normalized)
    {
        playerTarget = Mathf.Clamp01(normalized);
    }

    public void UpdateBossHealth(float normalized)
    {
        bossTarget = Mathf.Clamp01(normalized);
    }

    public void TriggerBossRage()
    {
        bossEnraged = true;
        if (bossFill != null) bossFill.color = new Color(0.88f, 0.08f, 0.72f);
        StartCoroutine(RageFlash());
    }

    public void SpawnDamageNumber(float dmg, Vector3 worldPos, bool onBoss)
    {
        if (dmgParent == null) return;

        Vector2 screen = Camera.main.WorldToScreenPoint(worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dmgParent, screen, null, out Vector2 local);

        GameObject go = new GameObject("Dmg");
        go.transform.SetParent(dmgParent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = local;
        rt.sizeDelta = new Vector2(120, 50);

        Text t = go.AddComponent<Text>();
        t.text      = $"-{(int)dmg}";
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = onBoss ? 40 : 32;
        t.fontStyle = FontStyle.Bold;
        t.color     = onBoss
            ? new Color(1f, 0.88f, 0.2f)
            : new Color(1f, 0.2f, 0.1f);
        t.alignment = TextAnchor.MiddleCenter;
        go.AddComponent<Outline>().effectColor = Color.black;

        StartCoroutine(FloatNumber(rt, t));
    }

    IEnumerator FloatNumber(RectTransform rt, Text t)
    {
        Vector2 start = rt.anchoredPosition;
        float e = 0f;
        while (e < 0.9f)
        {
            e += Time.deltaTime;
            rt.anchoredPosition = start + Vector2.up * e * 65f;
            Color c = t.color;
            c.a = 1f - Mathf.Clamp01((e - 0.4f) / 0.5f);
            t.color = c;
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    IEnumerator RageFlash()
    {
        for (int i = 0; i < 6; i++)
        {
            if (bossFill) bossFill.color = Color.white;
            yield return new WaitForSeconds(0.07f);
            if (bossFill) bossFill.color = new Color(0.88f, 0.08f, 0.72f);
            yield return new WaitForSeconds(0.07f);
        }
    }

    IEnumerator ShowPanel(GameObject panel, float delay)
    {
        if (panel == null) yield break;
        yield return new WaitForSeconds(delay);
        panel.SetActive(true);

        // fade background in
        Image bg = panel.GetComponent<Image>();
        if (bg != null)
        {
            Color target = bg.color;
            bg.color = new Color(target.r, target.g, target.b, 0f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 2f;
                bg.color = new Color(target.r, target.g, target.b,
                    Mathf.Clamp01(t) * target.a);
                yield return null;
            }
        }

        foreach (Text tx in panel.GetComponentsInChildren<Text>())
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
            yield return new WaitForSeconds(0.08f);
        }
    }


    public static UIManager CreateUI()
    {
        GameObject cgo  = new GameObject("CombatCanvas");
        Canvas     cv   = cgo.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 100;

        CanvasScaler cs        = cgo.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cgo.AddComponent<GraphicRaycaster>();

        UIManager     ui   = cgo.AddComponent<UIManager>();
        RectTransform root = cgo.GetComponent<RectTransform>();

        BuildBar(root, "Player",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(24f, 24f), new Vector2(340f, 68f),
            new Color(0.18f, 0.78f, 0.28f), "YOU",
            out ui.playerFill, out ui.playerHPText);

        BuildBar(root, "Boss",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-24f, -24f), new Vector2(340f, 68f),
            new Color(0.85f, 0.14f, 0.1f), "BOSS",
            out ui.bossFill, out ui.bossHPText);

        cgo.AddComponent<RhythmLaneManager>().Initialize(root);

        GameObject dmgGO = new GameObject("DmgNumbers");
        dmgGO.transform.SetParent(cgo.transform, false);
        ui.dmgParent = dmgGO.AddComponent<RectTransform>();
        ui.dmgParent.anchorMin = Vector2.zero;
        ui.dmgParent.anchorMax = Vector2.one;
        ui.dmgParent.offsetMin = ui.dmgParent.offsetMax = Vector2.zero;

        ui.gameOverPanel = MakePanel(root,
            "YOU DIED",
            new Color(0.85f, 0.05f, 0.05f),
            new Color(0f, 0f, 0f, 0.9f),
            "Press  R  to restart");

        ui.victoryPanel = MakePanel(root,
            "HEIR OF FIRE DESTROYED",
            new Color(1f, 0.85f, 0.2f),
            new Color(0f, 0f, 0f, 0.9f),
            "Press  R  to restart");

        return ui;
    }


    static void BuildBar(
        RectTransform root, string id,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size,
        Color fillColor, string label,
        out Image fill, out Text hpText)
    {
        GameObject panel = new GameObject($"{id}HP");
        panel.transform.SetParent(root, false);

        RectTransform pRT    = panel.AddComponent<RectTransform>();
        pRT.anchorMin        = anchorMin;
        pRT.anchorMax        = anchorMax;
        pRT.pivot            = pivot;
        pRT.anchoredPosition = anchoredPos;
        pRT.sizeDelta        = size;
        panel.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.93f);

        hpText = null; 
        Text lt = MakeChildText(panel.transform, "Label", label, 18, FontStyle.Bold,
            new Color(0.88f, 0.82f, 0.72f),
            new Vector2(0f, 0.52f), new Vector2(0.55f, 1f),
            new Vector2(10f, 2f),   new Vector2(0f, -2f),
            TextAnchor.MiddleLeft);

        hpText = MakeChildText(panel.transform, "HPNum", "", 15, FontStyle.Normal,
            new Color(0.72f, 0.68f, 0.6f),
            new Vector2(0.45f, 0.52f), new Vector2(1f, 1f),
            new Vector2(0f, 2f),       new Vector2(-10f, -2f),
            TextAnchor.MiddleRight);

        GameObject track = new GameObject("Track");
        track.transform.SetParent(panel.transform, false);
        RectTransform tRT = track.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 0f);
        tRT.anchorMax = new Vector2(1f, 0.50f);
        tRT.offsetMin = new Vector2(8f, 5f);
        tRT.offsetMax = new Vector2(-8f, 0f);
        track.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.03f, 1f);

        GameObject fgo = new GameObject("Fill");
        fgo.transform.SetParent(track.transform, false);
        RectTransform fRT = fgo.AddComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero;
        fRT.anchorMax = Vector2.one;
        fRT.offsetMin = new Vector2(2f, 2f);
        fRT.offsetMax = new Vector2(-2f, -2f);
        fill = fgo.AddComponent<Image>();
        fill.color      = fillColor;
        fill.type       = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 1f;
    }

    static Text MakeChildText(Transform parent, string name, string text,
        int fontSize, FontStyle style, Color color,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        TextAnchor alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        Text t = go.AddComponent<Text>();
        t.text      = text;
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = fontSize;
        t.fontStyle = style;
        t.color     = color;
        t.alignment = alignment;
        go.AddComponent<Outline>().effectColor = Color.black;
        return t;
    }


    static GameObject MakePanel(RectTransform root,
        string mainMsg, Color mainColor, Color bgColor, string subMsg)
    {
        GameObject p = new GameObject(mainMsg.Replace(" ", "") + "Panel");
        p.transform.SetParent(root, false);

        RectTransform rt = p.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        p.AddComponent<Image>().color = bgColor;

        MakeCenteredText(p.transform, "Title",  mainMsg, 72, FontStyle.Bold,   mainColor,                        new Vector2(0f,  40f), new Vector2(1000f, 110f));
        MakeCenteredText(p.transform, "Sub",    subMsg,  30, FontStyle.Normal, new Color(0.72f, 0.67f, 0.57f),   new Vector2(0f, -35f), new Vector2(700f,  60f));

        p.SetActive(false);
        return p;
    }

    static void MakeCenteredText(Transform parent, string name, string msg,
        int size, FontStyle style, Color color, Vector2 offset, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta        = sizeDelta;

        Text t = go.AddComponent<Text>();
        t.text      = msg;
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = size;
        t.fontStyle = style;
        t.color     = color;
        t.alignment = TextAnchor.MiddleCenter;

        Outline ol = go.AddComponent<Outline>();
        ol.effectColor    = Color.black;
        ol.effectDistance = new Vector2(2, -2);
    }
}
