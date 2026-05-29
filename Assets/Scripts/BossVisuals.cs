using System.Collections;
using UnityEngine;

public class BossVisuals : MonoBehaviour
{
    [Header("References (auto-created if null)")]
    public SpriteRenderer bodyRenderer;
    public SpriteRenderer weaponRenderer;

    [Header("Colors")]
    public Color idleColor       = new Color(0.22f, 0.06f, 0.06f, 0.95f);
    public Color windupColor     = new Color(0.6f,  0.08f, 0.04f, 1f);
    public Color activeColor     = new Color(0.9f,  0.2f,  0.04f, 1f);
    public Color recoveryColor   = new Color(0.14f, 0.1f,  0.09f, 0.8f);
    public Color defeatedColor   = new Color(0.08f, 0.08f, 0.08f, 0.4f);
    public Color counterHitColor = new Color(1f,    0.95f, 0.3f,  1f);
    public Color staggerColor    = new Color(0.7f,  0.6f,  0.2f,  1f);

    [Header("Animation")]
    public float windupPulseSpeed    = 7f;
    public float attackLungeDistance = 1.1f;
    public float lungeSpeed          = 14f;

    // child references
    private SpriteRenderer[] eyeRenderers;
    private SpriteRenderer helmetRenderer;
    private SpriteRenderer[] allRenderers;

    // telegraph indicators
    private GameObject telegraphLeft_GO;
    private GameObject telegraphRight_GO;
    private SpriteRenderer telegraphLeftSR;
    private SpriteRenderer telegraphRightSR;

    // danger ring — grows around boss during windup
    private GameObject dangerRing;
    private SpriteRenderer dangerRingSR;

    private Vector3 homePosition;
    private Vector3 targetPosition;
    private CombatState currentState = CombatState.Idle;
    private bool isDefeated;

    // idle sway
    private float swayTimer;

    void Start()
    {
        if (bodyRenderer == null)
            CreateBossSprite();

        homePosition   = transform.position;
        targetPosition = homePosition;

        allRenderers = GetComponentsInChildren<SpriteRenderer>();

        CreateTelegraphIndicators();
        CreateDangerRing();

        CombatManager.Instance.OnStateChanged  += OnStateChanged;
        CombatManager.Instance.OnCounterLanded += OnCounterHit;
        CombatManager.Instance.OnBossDefeated  += OnDefeated;
    }

    void OnDestroy()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnStateChanged  -= OnStateChanged;
            CombatManager.Instance.OnCounterLanded -= OnCounterHit;
            CombatManager.Instance.OnBossDefeated  -= OnDefeated;
        }
    }

    void Update()
    {
        if (isDefeated) return;

        // lerp toward lunge target
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * lungeSpeed);

        // idle sway — slow menacing rock
        swayTimer += Time.deltaTime * 0.8f;
        float sway = Mathf.Sin(swayTimer) * 0.008f;
        transform.rotation = Quaternion.Euler(0, 0, sway * Mathf.Rad2Deg);

        // windup pulse — body glows brighter as attack charges
        if (currentState == CombatState.Windup)
        {
            float pulse = (Mathf.Sin(Time.time * windupPulseSpeed) + 1f) / 2f;
            // pulse accelerates as windup progresses
            float timeInWindup = Time.time - GetStateEnterTime();
            float chargeT = Mathf.Clamp01(timeInWindup /
                (CombatManager.Instance.CurrentAttack?.telegraphDuration ?? 1.2f));
            float speed = Mathf.Lerp(windupPulseSpeed, windupPulseSpeed * 3f, chargeT);
            pulse = (Mathf.Sin(Time.time * speed) + 1f) / 2f;

            if (bodyRenderer != null)
                bodyRenderer.color = Color.Lerp(idleColor, windupColor, pulse);

            // eyes intensify
            if (eyeRenderers != null)
                foreach (var eye in eyeRenderers)
                    eye.color = Color.Lerp(
                        new Color(1f, 0.3f, 0.1f, 0.7f),
                        new Color(1f, 0.6f, 0.1f, 1f),
                        pulse);

            // danger ring grows
            UpdateDangerRing(chargeT);
        }

        UpdateTelegraph();
    }

    // ---- sprite creation ----

    void CreateBossSprite()
    {
        // main body — wide and imposing
        GameObject body = new GameObject("BossBody");
        body.transform.SetParent(transform, false);

        bodyRenderer = body.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = PlayerVisuals.CreateRect(1.5f, 2.2f);
        bodyRenderer.color  = idleColor;
        bodyRenderer.sortingOrder = 1;

        // shoulder pads — give width
        CreatePart("ShoulderL", new Vector3(-0.85f, 0.55f, 0f), new Vector3(0.5f, 0.3f, 1f),
            new Color(0.18f, 0.05f, 0.05f, 0.9f), 2);
        CreatePart("ShoulderR", new Vector3( 0.85f, 0.55f, 0f), new Vector3(0.5f, 0.3f, 1f),
            new Color(0.18f, 0.05f, 0.05f, 0.9f), 2);

        // helmet
        GameObject helmet = new GameObject("Helmet");
        helmet.transform.SetParent(transform, false);
        helmet.transform.localPosition = new Vector3(0f, 1.3f, 0f);

        helmetRenderer = helmet.AddComponent<SpriteRenderer>();
        helmetRenderer.sprite = PlayerVisuals.CreateRect(0.65f, 0.65f);
        helmetRenderer.color  = new Color(0.16f, 0.05f, 0.05f, 0.95f);
        helmetRenderer.sortingOrder = 3;

        // helmet horns — menacing
        CreatePart("HornL", new Vector3(-0.25f, 1.75f, 0f), new Vector3(0.12f, 0.5f, 1f),
            new Color(0.12f, 0.04f, 0.04f, 0.9f), 4);
        CreatePart("HornR", new Vector3( 0.25f, 1.75f, 0f), new Vector3(0.12f, 0.5f, 1f),
            new Color(0.12f, 0.04f, 0.04f, 0.9f), 4);

        // eyes — two glowing dots, stored for animation
        eyeRenderers = new SpriteRenderer[2];
        eyeRenderers[0] = CreateEye(new Vector3(-0.14f, 1.32f, 0f));
        eyeRenderers[1] = CreateEye(new Vector3( 0.14f, 1.32f, 0f));

        // weapon arm
        GameObject weapon = new GameObject("Weapon");
        weapon.transform.SetParent(transform, false);
        weapon.transform.localPosition = new Vector3(-0.6f, 0.2f, 0f);
        weapon.transform.localRotation = Quaternion.Euler(0, 0, 30f);

        weaponRenderer = weapon.AddComponent<SpriteRenderer>();
        weaponRenderer.sprite = PlayerVisuals.CreateRect(0.14f, 1.5f);
        weaponRenderer.color  = new Color(0.45f, 0.38f, 0.32f, 0.9f);
        weaponRenderer.sortingOrder = 4;

        // weapon glow tip
        CreatePart("WeaponTip", new Vector3(-0.6f, -0.6f, 0f),
            new Vector3(0.14f, 0.14f, 1f), new Color(0.8f, 0.3f, 0.1f, 0.6f), 5);
    }

    void CreatePart(string name, Vector3 pos, Vector3 scale, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PlayerVisuals.CreateRect(1f, 1f);
        sr.color  = color;
        sr.sortingOrder = order;
    }

    SpriteRenderer CreateEye(Vector3 localPos)
    {
        GameObject eye = new GameObject("Eye");
        eye.transform.SetParent(transform, false);
        eye.transform.localPosition = localPos;

        SpriteRenderer sr = eye.AddComponent<SpriteRenderer>();
        sr.sprite = PlayerVisuals.CreateCircle(0.065f);
        sr.color  = new Color(1f, 0.3f, 0.1f, 0.8f);
        sr.sortingOrder = 5;
        return sr;
    }

    void CreateTelegraphIndicators()
    {
        telegraphLeft_GO = BuildTelegraphArrow("TelegraphLeft",  new Vector3(-3.2f, -0.5f, 0f), true);
        telegraphRight_GO = BuildTelegraphArrow("TelegraphRight", new Vector3( 3.2f, -0.5f, 0f), false);

        telegraphLeftSR  = telegraphLeft_GO.GetComponent<SpriteRenderer>();
        telegraphRightSR = telegraphRight_GO.GetComponent<SpriteRenderer>();

        telegraphLeft_GO.SetActive(false);
        telegraphRight_GO.SetActive(false);
    }

    GameObject BuildTelegraphArrow(string name, Vector3 pos, bool pointLeft)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform.parent ?? transform, true);
        go.transform.position = pos;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateArrow(pointLeft);
        sr.color  = new Color(1f, 0.3f, 0.1f, 0.5f);
        sr.sortingOrder = 20;
        return go;
    }

    void CreateDangerRing()
    {
        // a hollow ring that expands as the boss charges
        dangerRing = new GameObject("DangerRing");
        dangerRing.transform.SetParent(transform, false);
        dangerRing.transform.localPosition = Vector3.zero;

        dangerRingSR = dangerRing.AddComponent<SpriteRenderer>();
        dangerRingSR.sprite = PlayerVisuals.CreateCircle(0.5f);
        dangerRingSR.color  = Color.clear;
        dangerRingSR.sortingOrder = 0; // behind boss
        dangerRing.SetActive(false);
    }

    void UpdateDangerRing(float chargeT)
    {
        dangerRing.SetActive(true);
        // ring grows outward as charge increases
        float scale = Mathf.Lerp(1.2f, 3.5f, chargeT);
        dangerRing.transform.localScale = Vector3.one * scale;

        float alpha = Mathf.Lerp(0.05f, 0.25f, chargeT);
        dangerRingSR.color = new Color(0.9f, 0.15f, 0.05f, alpha);
    }

    void UpdateTelegraph()
    {
        if (CombatManager.Instance.CurrentAttack == null) return;

        bool show = currentState == CombatState.Windup || currentState == CombatState.Active;
        DodgeDirection dir = CombatManager.Instance.CurrentAttack.requiredDodge;

        // arrow shows on the side the attack comes FROM
        telegraphLeft_GO.SetActive(show  && dir == DodgeDirection.Right);
        telegraphRight_GO.SetActive(show && dir == DodgeDirection.Left);

        if (!show) return;

        if (currentState == CombatState.Active)
        {
            // solid bright red when active — danger
            telegraphLeftSR.color  = new Color(1f, 0.05f, 0.05f, 1f);
            telegraphRightSR.color = new Color(1f, 0.05f, 0.05f, 1f);
            // scale arrows up when active
            telegraphLeft_GO.transform.localScale  = Vector3.one * 1.4f;
            telegraphRight_GO.transform.localScale = Vector3.one * 1.4f;
        }
        else
        {
            float pulse = (Mathf.Sin(Time.time * 8f) + 1f) / 2f;
            Color c = Color.Lerp(
                new Color(1f, 0.3f, 0.1f, 0.2f),
                new Color(1f, 0.5f, 0.1f, 0.9f), pulse);
            telegraphLeftSR.color  = c;
            telegraphRightSR.color = c;
            telegraphLeft_GO.transform.localScale  = Vector3.one;
            telegraphRight_GO.transform.localScale = Vector3.one;
        }
    }

    // ---- state handling ----

    void OnStateChanged(CombatState state)
    {
        currentState = state;
        if (isDefeated) return;

        switch (state)
        {
            case CombatState.Idle:
                if (bodyRenderer != null) bodyRenderer.color = idleColor;
                targetPosition = homePosition;
                dangerRing.SetActive(false);
                ResetWeapon();
                break;

            case CombatState.Windup:
                dangerRing.SetActive(true);
                // pull weapon back dramatically
                if (weaponRenderer != null)
                {
                    DodgeDirection dir = CombatManager.Instance.CurrentAttack.requiredDodge;
                    float sign = (dir == DodgeDirection.Left) ? 1f : -1f;
                    weaponRenderer.transform.localRotation = Quaternion.Euler(0, 0, sign * 75f);
                }
                break;

            case CombatState.Active:
                if (bodyRenderer != null) bodyRenderer.color = activeColor;
                dangerRing.SetActive(false);
                // slam lunge toward player
                DodgeDirection atkDir = CombatManager.Instance.CurrentAttack.requiredDodge;
                float atkSign = (atkDir == DodgeDirection.Left) ? 1f : -1f;
                targetPosition = homePosition + Vector3.down * attackLungeDistance;
                // weapon swings hard
                if (weaponRenderer != null)
                    weaponRenderer.transform.localRotation = Quaternion.Euler(0, 0, -atkSign * 50f);
                break;

            case CombatState.Recovery:
                if (bodyRenderer != null) bodyRenderer.color = recoveryColor;
                targetPosition = homePosition;
                ResetWeapon();
                break;

            case CombatState.PerfectWindow:
                // staggered — stumble back
                if (bodyRenderer != null) bodyRenderer.color = staggerColor;
                targetPosition = homePosition + Vector3.up * 0.4f;
                StartCoroutine(StaggerShake());
                break;

            case CombatState.Counter:
                // recoil hard from counter
                targetPosition = homePosition + Vector3.up * 0.7f;
                break;
        }
    }

    void OnCounterHit()
    {
        if (isDefeated) return;
        StartCoroutine(CounterFlash());
    }

    IEnumerator CounterFlash()
    {
        // multi-flash on counter hit — feels impactful
        for (int i = 0; i < 3; i++)
        {
            if (bodyRenderer != null) bodyRenderer.color = counterHitColor;
            // scale punch
            transform.localScale = Vector3.one * 1.15f;
            yield return new WaitForSeconds(0.06f);
            if (bodyRenderer != null) bodyRenderer.color = idleColor;
            transform.localScale = Vector3.one;
            yield return new WaitForSeconds(0.04f);
        }
    }

    IEnumerator StaggerShake()
    {
        Vector3 origin = targetPosition;
        for (int i = 0; i < 5; i++)
        {
            targetPosition = origin + (Vector3)Random.insideUnitCircle * 0.12f;
            yield return new WaitForSeconds(0.04f);
        }
        targetPosition = origin;
    }

    void OnDefeated()
    {
        isDefeated = true;
        telegraphLeft_GO.SetActive(false);
        telegraphRight_GO.SetActive(false);
        dangerRing.SetActive(false);
        StartCoroutine(DefeatShatter());
    }

    IEnumerator DefeatShatter()
    {
        // flash white first
        foreach (var sr in allRenderers)
            sr.color = Color.white;
        yield return new WaitForSeconds(0.1f);

        // break apart — each child flies in a random direction
        var children = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform)
            children.Add(child);

        var velocities = new Vector3[children.Count];
        for (int i = 0; i < children.Count; i++)
            velocities[i] = new Vector3(
                Random.Range(-2f, 2f),
                Random.Range(-1f, 3f), 0f);

        float t = 0f;
        while (t < 1.2f)
        {
            t += Time.deltaTime;
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] == null) continue;
                children[i].position += velocities[i] * Time.deltaTime;
                velocities[i] += Vector3.down * 4f * Time.deltaTime; // gravity

                // fade each piece
                SpriteRenderer sr = children[i].GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = Mathf.Lerp(1f, 0f, t / 1.2f);
                    sr.color = c;
                }
            }
            yield return null;
        }
    }

    void ResetWeapon()
    {
        if (weaponRenderer != null)
            weaponRenderer.transform.localRotation = Quaternion.Euler(0, 0, 30f);
    }

    // stateEnterTime isn't exposed by CombatManager so we track it locally
    private float localStateEnterTime;
    float GetStateEnterTime() => localStateEnterTime;

    // ---- arrow sprite ----

    static Sprite CreateArrow(bool pointLeft)
    {
        int w = 64, h = 40;
        Texture2D tex = new Texture2D(w, h);
        tex.filterMode = FilterMode.Point;
        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int ax = pointLeft ? x : (w - 1 - x);
                bool inShaft = ax >= h / 2 && y >= h / 4 && y < h * 3 / 4;
                float headX   = (float)ax / (h / 2f);
                float centerY = h / 2f;
                bool inHead   = ax < h / 2 && Mathf.Abs(y - centerY) < headX * centerY;
                pixels[y * w + x] = (inShaft || inHead) ? Color.white : Color.clear;
            }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
    }
}
