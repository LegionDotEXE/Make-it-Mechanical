using UnityEngine;

/// <summary>
/// Handles boss visual feedback: sprite, telegraph indicator, attack animation, defeat.
/// Attach to the Boss GameObject (GameBootstrap does this automatically).
/// </summary>
public class BossVisuals : MonoBehaviour
{
    [Header("References (auto-created if null)")]
    public SpriteRenderer bodyRenderer;
    public SpriteRenderer weaponRenderer;
    public SpriteRenderer telegraphArrow;

    [Header("Colors (dimmed — backdrop behind rhythm lanes)")]
    public Color idleColor       = new Color(0.20f, 0.06f, 0.06f, 0.6f);  // dark crimson, dimmed
    public Color windupColor     = new Color(0.45f, 0.08f, 0.04f, 0.7f);  // smoldering red
    public Color activeColor     = new Color(0.7f,  0.15f, 0.04f, 0.8f);  // flame burst
    public Color recoveryColor   = new Color(0.15f, 0.12f, 0.10f, 0.5f);  // exhausted
    public Color defeatedColor   = new Color(0.1f,  0.1f,  0.1f,  0.3f);
    public Color telegraphLeft   = new Color(1f, 0.3f, 0.1f, 0.4f);
    public Color telegraphRight  = new Color(1f, 0.3f, 0.1f, 0.4f);
    public Color counterHitColor = new Color(1f, 1f, 0.3f, 0.7f);

    [Header("Animation")]
    public float windupPulseSpeed = 6f;
    public float attackLungeDistance = 0.8f;

    private Vector3 homePosition;
    private Vector3 targetPosition;
    private float lungeSpeed = 10f;
    private bool isDefeated;
    private CombatState currentState = CombatState.Idle;

    // telegraph
    private GameObject telegraphLeft_GO;
    private GameObject telegraphRight_GO;
    private SpriteRenderer telegraphLeftRenderer;
    private SpriteRenderer telegraphRightRenderer;

    void Start()
    {
        if (bodyRenderer == null)
            CreateBossSprite();

        homePosition   = transform.position;
        targetPosition = homePosition;

        CreateTelegraphIndicators();

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

        // lerp toward target
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * lungeSpeed);

        // pulsing glow during windup
        if (currentState == CombatState.Windup && bodyRenderer != null)
        {
            float pulse = (Mathf.Sin(Time.time * windupPulseSpeed) + 1f) / 2f;
            bodyRenderer.color = Color.Lerp(idleColor, windupColor, pulse);
        }

        // telegraph arrow pulsing
        UpdateTelegraph();
    }

    void CreateBossSprite()
    {
        // body - large imposing rectangle
        GameObject body = new GameObject("BossBody");
        body.transform.SetParent(transform, false);
        body.transform.localPosition = Vector3.zero;

        bodyRenderer = body.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = PlayerVisuals.CreateRect(1.4f, 2.0f);
        bodyRenderer.color  = idleColor;
        bodyRenderer.sortingOrder = 1;

        // "helmet" - menacing shape on top
        GameObject helmet = new GameObject("Helmet");
        helmet.transform.SetParent(transform, false);
        helmet.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        SpriteRenderer helmetRenderer = helmet.AddComponent<SpriteRenderer>();
        helmetRenderer.sprite = PlayerVisuals.CreateRect(0.5f, 0.5f);
        helmetRenderer.color  = new Color(0.15f, 0.05f, 0.05f, 0.6f);
        helmetRenderer.sortingOrder = 2;

        // eyes - two small glowing dots
        CreateEye(new Vector3(-0.12f, 1.25f, 0f));
        CreateEye(new Vector3(0.12f,  1.25f, 0f));

        // weapon arm
        GameObject weapon = new GameObject("Weapon");
        weapon.transform.SetParent(transform, false);
        weapon.transform.localPosition = new Vector3(0.5f, 0.3f, 0f);
        weapon.transform.localRotation = Quaternion.Euler(0, 0, -30f);

        weaponRenderer = weapon.AddComponent<SpriteRenderer>();
        weaponRenderer.sprite = PlayerVisuals.CreateRect(0.12f, 1.2f);
        weaponRenderer.color  = new Color(0.4f, 0.35f, 0.3f, 0.5f);
        weaponRenderer.sortingOrder = 3;
    }

    void CreateEye(Vector3 localPos)
    {
        GameObject eye = new GameObject("Eye");
        eye.transform.SetParent(transform, false);
        eye.transform.localPosition = localPos;

        SpriteRenderer eyeRenderer = eye.AddComponent<SpriteRenderer>();
        eyeRenderer.sprite = PlayerVisuals.CreateCircle(0.05f);
        eyeRenderer.color  = new Color(1f, 0.3f, 0.1f, 0.7f); // ember glow, dimmed
        eyeRenderer.sortingOrder = 3;
    }

    void CreateTelegraphIndicators()
    {
        // left arrow
        telegraphLeft_GO = new GameObject("TelegraphLeft");
        telegraphLeft_GO.transform.SetParent(transform.parent ?? transform, true);
        telegraphLeft_GO.transform.position = new Vector3(-3f, -1f, 0f);

        telegraphLeftRenderer = telegraphLeft_GO.AddComponent<SpriteRenderer>();
        telegraphLeftRenderer.sprite = CreateArrow(true);
        telegraphLeftRenderer.color  = telegraphLeft;
        telegraphLeftRenderer.sortingOrder = 20;
        telegraphLeft_GO.SetActive(false);

        // right arrow
        telegraphRight_GO = new GameObject("TelegraphRight");
        telegraphRight_GO.transform.SetParent(transform.parent ?? transform, true);
        telegraphRight_GO.transform.position = new Vector3(3f, -1f, 0f);

        telegraphRightRenderer = telegraphRight_GO.AddComponent<SpriteRenderer>();
        telegraphRightRenderer.sprite = CreateArrow(false);
        telegraphRightRenderer.color  = telegraphRight;
        telegraphRightRenderer.sortingOrder = 20;
        telegraphRight_GO.SetActive(false);
    }

    void UpdateTelegraph()
    {
        if (CombatManager.Instance.CurrentAttack == null) return;

        bool showTelegraph = (currentState == CombatState.Windup || currentState == CombatState.Active);
        DodgeDirection dir = CombatManager.Instance.CurrentAttack.requiredDodge;

        // show the arrow on the side the attack comes FROM (opposite of dodge direction)
        // so if requiredDodge is Left, the attack comes from the Right
        telegraphLeft_GO.SetActive(showTelegraph && dir == DodgeDirection.Right);
        telegraphRight_GO.SetActive(showTelegraph && dir == DodgeDirection.Left);

        if (showTelegraph)
        {
            float pulse = (Mathf.Sin(Time.time * 10f) + 1f) / 2f;
            Color c = Color.Lerp(new Color(1f, 0.3f, 0.1f, 0.3f), new Color(1f, 0.3f, 0.1f, 1f), pulse);

            if (currentState == CombatState.Active)
                c = new Color(1f, 0.05f, 0.05f, 1f); // solid red when active

            telegraphLeftRenderer.color  = c;
            telegraphRightRenderer.color = c;
        }
    }

    void OnStateChanged(CombatState state)
    {
        currentState = state;
        if (isDefeated) return;

        switch (state)
        {
            case CombatState.Idle:
                bodyRenderer.color = idleColor;
                targetPosition = homePosition;
                ResetWeapon();
                break;

            case CombatState.Windup:
                // pull weapon back
                if (weaponRenderer != null)
                {
                    DodgeDirection dir = CombatManager.Instance.CurrentAttack.requiredDodge;
                    float sign = (dir == DodgeDirection.Left) ? 1f : -1f;
                    weaponRenderer.transform.localRotation = Quaternion.Euler(0, 0, sign * 60f);
                }
                break;

            case CombatState.Active:
                bodyRenderer.color = activeColor;
                // lunge forward
                DodgeDirection atkDir = CombatManager.Instance.CurrentAttack.requiredDodge;
                float atkSign = (atkDir == DodgeDirection.Left) ? 1f : -1f;
                targetPosition = homePosition + Vector3.right * atkSign * attackLungeDistance;
                // swing weapon
                if (weaponRenderer != null)
                    weaponRenderer.transform.localRotation = Quaternion.Euler(0, 0, -atkSign * 45f);
                break;

            case CombatState.Recovery:
                bodyRenderer.color = recoveryColor;
                targetPosition = homePosition;
                ResetWeapon();
                break;

            case CombatState.PerfectWindow:
                // boss is staggered — flash
                bodyRenderer.color = new Color(0.6f, 0.5f, 0.2f);
                targetPosition = homePosition;
                break;

            case CombatState.Counter:
                targetPosition = homePosition + Vector3.down * 0.3f;
                break;
        }
    }

    void OnCounterHit()
    {
        if (isDefeated) return;
        StartCoroutine(CounterFlash());
    }

    System.Collections.IEnumerator CounterFlash()
    {
        bodyRenderer.color = counterHitColor;
        yield return new WaitForSeconds(0.15f);
        if (!isDefeated)
            bodyRenderer.color = idleColor;
    }

    void OnDefeated()
    {
        isDefeated = true;
        bodyRenderer.color = defeatedColor;

        // hide telegraph
        telegraphLeft_GO.SetActive(false);
        telegraphRight_GO.SetActive(false);

        StartCoroutine(DefeatAnimation());
    }

    System.Collections.IEnumerator DefeatAnimation()
    {
        float t = 0f;
        Vector3 startScale = transform.localScale;
        while (t < 1f)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, startScale * 0.3f, t);
            float a = Mathf.Lerp(1f, 0f, t);
            bodyRenderer.color = new Color(defeatedColor.r, defeatedColor.g, defeatedColor.b, a);
            yield return null;
        }
    }

    void ResetWeapon()
    {
        if (weaponRenderer != null)
            weaponRenderer.transform.localRotation = Quaternion.Euler(0, 0, -30f);
    }

    // ---- arrow sprite helper ----

    static Sprite CreateArrow(bool pointLeft)
    {
        int w = 48, h = 32;
        Texture2D tex = new Texture2D(w, h);
        tex.filterMode = FilterMode.Point;
        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int ax = pointLeft ? x : (w - 1 - x);
                // arrow shape: triangle on left, rectangle on right
                bool inShaft = (ax >= h / 2 && y >= h / 4 && y < h * 3 / 4);
                float headX = (float)ax / (h / 2);
                float centerY = h / 2f;
                bool inHead = (ax < h / 2) && (Mathf.Abs(y - centerY) < headX * centerY);

                pixels[y * w + x] = (inShaft || inHead) ? Color.white : Color.clear;
            }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
    }
}
