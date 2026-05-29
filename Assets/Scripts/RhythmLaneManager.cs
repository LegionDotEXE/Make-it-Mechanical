using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The core rhythm-game overlay. Renders falling tiles in lanes that sync
/// to the CombatManager state machine. Think Piano Tiles with a Dark Souls skin.
///
/// Layout:
///   Left lane  (A key)  — dodge-left tiles
///   Right lane (D key)  — dodge-right tiles
///   Center lane (W key) — counter tiles (appear only on perfect dodge window)
///
/// Tiles spawn when the boss starts a Windup and fall toward a judgment line.
/// The tile reaches the line exactly when the attack becomes Active.
/// </summary>
public class RhythmLaneManager : MonoBehaviour
{
    public static RhythmLaneManager Instance { get; private set; }

    [Header("Lane Layout")]
    public float laneWidth       = 120f;
    public float laneSpacing     = 10f;
    public float judgmentLineY   = -380f;   // Y position of the hit zone (from center)
    public float spawnY          = 500f;    // where tiles appear at the top
    public float destroyY        = -500f;   // where missed tiles get cleaned up

    [Header("Tile Appearance")]
    public float tileWidth       = 110f;
    public float tileHeight      = 60f;
    public Color leftTileColor   = new Color(0.3f, 0.5f, 0.9f, 0.9f);   // cold blue
    public Color rightTileColor  = new Color(0.9f, 0.3f, 0.2f, 0.9f);   // blood red
    public Color counterTileColor= new Color(1f, 0.85f, 0.2f, 0.95f);   // gold
    public Color laneColor       = new Color(0.1f, 0.08f, 0.06f, 0.7f); // dark translucent
    public Color laneBorderColor = new Color(0.25f, 0.2f, 0.15f, 0.5f);
    public Color judgmentColor   = new Color(0.8f, 0.7f, 0.5f, 0.6f);   // faded gold line

    [Header("Feedback Colors")]
    public Color perfectFlash    = new Color(1f, 0.85f, 0.2f);
    public Color goodFlash       = new Color(0.4f, 0.7f, 1f);
    public Color missFlash       = new Color(0.9f, 0.15f, 0.1f);
    public Color counterFlash    = new Color(1f, 1f, 0.5f);

    // runtime
    private RectTransform canvasRect;
    private RectTransform lanesContainer;
    private RectTransform leftLane, rightLane, centerLane;
    private RectTransform judgmentLine;
    private Image judgmentLineImage;

    // active tiles
    private List<FallingTile> activeTiles = new List<FallingTile>();

    // tile pool
    private Queue<GameObject> tilePool = new Queue<GameObject>();

    // current attack tracking
    private bool tileSpawnedForCurrentAttack = false;
    private bool counterTileSpawned = false;

    // key labels
    private Text leftKeyLabel, rightKeyLabel, centerKeyLabel;

    // feedback
    private Text feedbackText;
    private float feedbackTimer;

    // judgment line glow
    private float judgmentGlowTimer;
    private Color judgmentGlowColor;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Call from UIManager.CreateUI() to build the lane visuals under the canvas.
    /// </summary>
    public void Initialize(RectTransform canvas)
    {
        canvasRect = canvas;
        BuildLanes();
        BuildJudgmentLine();
        BuildKeyLabels();
        BuildFeedbackText();
        SubscribeToEvents();
    }

    void OnDestroy()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnStateChanged             -= OnStateChanged;
            CombatManager.Instance.OnPlayerDodgedSuccessfully  -= OnGoodDodge;
            CombatManager.Instance.OnPlayerPerfectDodge        -= OnPerfectDodge;
            CombatManager.Instance.OnPlayerHit                 -= OnMiss;
            CombatManager.Instance.OnCounterLanded             -= OnCounter;
        }
    }

    void SubscribeToEvents()
    {
        CombatManager.Instance.OnStateChanged             += OnStateChanged;
        CombatManager.Instance.OnPlayerDodgedSuccessfully  += OnGoodDodge;
        CombatManager.Instance.OnPlayerPerfectDodge        += OnPerfectDodge;
        CombatManager.Instance.OnPlayerHit                 -= OnMiss; // ensure no double-sub
        CombatManager.Instance.OnPlayerHit                 += OnMiss;
        CombatManager.Instance.OnCounterLanded             += OnCounter;
    }

    void Update()
    {
        // move tiles downward
        float dt = Time.deltaTime;

        for (int i = activeTiles.Count - 1; i >= 0; i--)
        {
            FallingTile tile = activeTiles[i];
            if (tile.go == null) { activeTiles.RemoveAt(i); continue; }

            RectTransform rt = tile.rect;
            float newY = rt.anchoredPosition.y - tile.fallSpeed * dt;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, newY);

            // pulse effect as tile approaches judgment line
            float distToLine = Mathf.Abs(newY - judgmentLineY);
            if (distToLine < 80f)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 12f) * 0.15f;
                rt.localScale = Vector3.one * pulse;
            }
            else
            {
                rt.localScale = Vector3.one;
            }

            // cleanup if it falls past destroy line
            if (newY < destroyY)
            {
                RecycleTile(tile);
                activeTiles.RemoveAt(i);
            }
        }

        // feedback fade
        if (feedbackTimer > 0f && feedbackText != null)
        {
            feedbackTimer -= dt;
            Color c = feedbackText.color;
            c.a = Mathf.Clamp01(feedbackTimer / 0.4f);
            feedbackText.color = c;
            float scale = 1f + (1f - feedbackTimer / 0.8f) * 0.2f;
            feedbackText.transform.localScale = Vector3.one * Mathf.Min(scale, 1.3f);
        }

        // judgment line glow fade
        if (judgmentGlowTimer > 0f && judgmentLineImage != null)
        {
            judgmentGlowTimer -= dt;
            float t = judgmentGlowTimer / 0.3f;
            judgmentLineImage.color = Color.Lerp(judgmentColor, judgmentGlowColor, t);
        }
    }

    // ---- event handlers ----

    void OnStateChanged(CombatState state)
    {
        switch (state)
        {
            case CombatState.Windup:
                if (!tileSpawnedForCurrentAttack)
                {
                    SpawnDodgeTile();
                    tileSpawnedForCurrentAttack = true;
                }
                break;

            case CombatState.PerfectWindow:
                if (!counterTileSpawned)
                {
                    SpawnCounterTile();
                    counterTileSpawned = true;
                }
                break;

            case CombatState.Idle:
                tileSpawnedForCurrentAttack = false;
                counterTileSpawned = false;
                break;
        }
    }

    void OnGoodDodge()
    {
        ClearDodgeTiles();
        ShowFeedback("DODGE", goodFlash);
        FlashJudgmentLine(goodFlash);
    }

    void OnPerfectDodge()
    {
        ClearDodgeTiles();
        ShowFeedback("PERFECT", perfectFlash);
        FlashJudgmentLine(perfectFlash);
    }

    void OnMiss()
    {
        ClearDodgeTiles();
        ShowFeedback("HIT!", missFlash);
        FlashJudgmentLine(missFlash);
    }

    void OnCounter()
    {
        ClearCounterTiles();
        ShowFeedback("RIPOSTE!", counterFlash);
        FlashJudgmentLine(counterFlash);
    }

    // ---- tile spawning ----

    void SpawnDodgeTile()
    {
        AttackData atk = CombatManager.Instance.CurrentAttack;
        if (atk == null) return;

        // which lane does the tile fall in?
        // tile appears in the lane the player needs to press
        bool isLeft = (atk.requiredDodge == DodgeDirection.Left);
        RectTransform lane = isLeft ? leftLane : rightLane;
        Color color = isLeft ? leftTileColor : rightTileColor;

        // calculate fall speed so tile reaches judgment line in exactly telegraphDuration
        float fallDistance = spawnY - judgmentLineY;
        float fallSpeed = fallDistance / atk.telegraphDuration;

        GameObject tileGO = GetOrCreateTile();
        RectTransform rt = tileGO.GetComponent<RectTransform>();
        rt.SetParent(lanesContainer, false);

        // position at top of the correct lane
        float laneX = lane.anchoredPosition.x;
        rt.anchoredPosition = new Vector2(laneX, spawnY);
        rt.sizeDelta = new Vector2(tileWidth, tileHeight);

        Image img = tileGO.GetComponent<Image>();
        img.color = color;

        tileGO.SetActive(true);

        activeTiles.Add(new FallingTile
        {
            go = tileGO,
            rect = rt,
            image = img,
            fallSpeed = fallSpeed,
            isCounter = false
        });
    }

    void SpawnCounterTile()
    {
        // counter tile drops fast in the center lane
        GameObject tileGO = GetOrCreateTile();
        RectTransform rt = tileGO.GetComponent<RectTransform>();
        rt.SetParent(lanesContainer, false);

        float laneX = centerLane.anchoredPosition.x;
        // spawn closer to the line since the window is short
        float counterSpawnY = judgmentLineY + 200f;
        rt.anchoredPosition = new Vector2(laneX, counterSpawnY);
        rt.sizeDelta = new Vector2(tileWidth * 0.8f, tileHeight * 1.2f);

        Image img = tileGO.GetComponent<Image>();
        img.color = counterTileColor;

        tileGO.SetActive(true);

        // fall speed: reach judgment in ~0.25s
        float fallSpeed = 200f / 0.25f;

        activeTiles.Add(new FallingTile
        {
            go = tileGO,
            rect = rt,
            image = img,
            fallSpeed = fallSpeed,
            isCounter = true
        });
    }

    void ClearDodgeTiles()
    {
        for (int i = activeTiles.Count - 1; i >= 0; i--)
        {
            if (!activeTiles[i].isCounter)
            {
                RecycleTile(activeTiles[i]);
                activeTiles.RemoveAt(i);
            }
        }
    }

    void ClearCounterTiles()
    {
        for (int i = activeTiles.Count - 1; i >= 0; i--)
        {
            if (activeTiles[i].isCounter)
            {
                RecycleTile(activeTiles[i]);
                activeTiles.RemoveAt(i);
            }
        }
    }

    // ---- tile pooling ----

    GameObject GetOrCreateTile()
    {
        if (tilePool.Count > 0)
        {
            GameObject pooled = tilePool.Dequeue();
            if (pooled != null) return pooled;
        }

        GameObject go = new GameObject("Tile");
        go.AddComponent<RectTransform>();
        Image img = go.AddComponent<Image>();
        img.raycastTarget = false;

        // rounded look via slight transparency at edges (simple approach)
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.15f);
        outline.effectDistance = new Vector2(2, -2);

        return go;
    }

    void RecycleTile(FallingTile tile)
    {
        if (tile.go != null)
        {
            tile.go.SetActive(false);
            tilePool.Enqueue(tile.go);
        }
    }

    // ---- UI construction ----

    void BuildLanes()
    {
        // container for all lanes
        GameObject container = new GameObject("RhythmLanes");
        container.transform.SetParent(canvasRect, false);
        lanesContainer = container.AddComponent<RectTransform>();
        lanesContainer.anchorMin = new Vector2(0.5f, 0.5f);
        lanesContainer.anchorMax = new Vector2(0.5f, 0.5f);
        lanesContainer.sizeDelta = new Vector2(laneWidth * 3 + laneSpacing * 4, 1080);

        // left lane
        leftLane = CreateLaneBG("LeftLane", -(laneWidth + laneSpacing));
        // center lane
        centerLane = CreateLaneBG("CenterLane", 0f);
        // right lane
        rightLane = CreateLaneBG("RightLane", (laneWidth + laneSpacing));
    }

    RectTransform CreateLaneBG(string name, float xPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(lanesContainer, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(xPos, 0f);
        rt.sizeDelta = new Vector2(laneWidth, 0f);

        Image bg = go.AddComponent<Image>();
        bg.color = laneColor;
        bg.raycastTarget = false;

        // border (left)
        CreateBorder(rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-laneWidth / 2f, 0f));
        // border (right)
        CreateBorder(rt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(laneWidth / 2f, 0f));

        return rt;
    }

    void CreateBorder(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos)
    {
        GameObject border = new GameObject("Border");
        border.transform.SetParent(parent, false);

        RectTransform rt = border.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchorMin.x, 0f);
        rt.anchorMax = new Vector2(anchorMax.x, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(2f, 0f);

        Image img = border.AddComponent<Image>();
        img.color = laneBorderColor;
        img.raycastTarget = false;
    }

    void BuildJudgmentLine()
    {
        GameObject go = new GameObject("JudgmentLine");
        go.transform.SetParent(lanesContainer, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, judgmentLineY);
        rt.sizeDelta = new Vector2(0f, 4f);

        judgmentLineImage = go.AddComponent<Image>();
        judgmentLineImage.color = judgmentColor;
        judgmentLineImage.raycastTarget = false;

        // hit zone glow area (wider, subtler)
        GameObject glow = new GameObject("JudgmentGlow");
        glow.transform.SetParent(lanesContainer, false);

        RectTransform glowRt = glow.AddComponent<RectTransform>();
        glowRt.anchorMin = new Vector2(0f, 0.5f);
        glowRt.anchorMax = new Vector2(1f, 0.5f);
        glowRt.anchoredPosition = new Vector2(0f, judgmentLineY);
        glowRt.sizeDelta = new Vector2(0f, 30f);

        Image glowImg = glow.AddComponent<Image>();
        glowImg.color = new Color(judgmentColor.r, judgmentColor.g, judgmentColor.b, 0.1f);
        glowImg.raycastTarget = false;
    }

    void BuildKeyLabels()
    {
        float labelY = judgmentLineY - 40f;

        leftKeyLabel   = CreateKeyLabel("A", -(laneWidth + laneSpacing), labelY, leftTileColor);
        centerKeyLabel = CreateKeyLabel("W", 0f, labelY, counterTileColor);
        rightKeyLabel  = CreateKeyLabel("D", (laneWidth + laneSpacing), labelY, rightTileColor);
    }

    Text CreateKeyLabel(string key, float x, float y, Color color)
    {
        GameObject go = new GameObject($"Key_{key}");
        go.transform.SetParent(lanesContainer, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(60f, 40f);

        Text text = go.AddComponent<Text>();
        text.text = key;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.fontStyle = FontStyle.Bold;
        text.color = new Color(color.r, color.g, color.b, 0.6f);
        text.alignment = TextAnchor.MiddleCenter;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1, -1);

        return text;
    }

    void BuildFeedbackText()
    {
        GameObject go = new GameObject("RhythmFeedback");
        go.transform.SetParent(lanesContainer, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, judgmentLineY + 60f);
        rt.sizeDelta = new Vector2(400f, 60f);

        feedbackText = go.AddComponent<Text>();
        feedbackText.text = "";
        feedbackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        feedbackText.fontSize = 42;
        feedbackText.fontStyle = FontStyle.Bold;
        feedbackText.color = Color.clear;
        feedbackText.alignment = TextAnchor.MiddleCenter;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
    }

    // ---- feedback ----

    void ShowFeedback(string text, Color color)
    {
        if (feedbackText == null) return;
        feedbackText.text = text;
        feedbackText.color = color;
        feedbackTimer = 0.8f;
        feedbackText.transform.localScale = Vector3.one * 1.3f;
    }

    void FlashJudgmentLine(Color color)
    {
        judgmentGlowColor = color;
        judgmentGlowTimer = 0.3f;
    }

    // ---- data ----

    struct FallingTile
    {
        public GameObject go;
        public RectTransform rect;
        public Image image;
        public float fallSpeed;
        public bool isCounter;
    }
}
