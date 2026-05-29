using System.Collections;
using UnityEngine;

public class BossVisuals : MonoBehaviour
{
    public SpriteRenderer bodyRenderer;
    public SpriteRenderer weaponRenderer;
    private SpriteRenderer[] eyeRenderers;
    private SpriteRenderer[] allRenderers;

    // colors
    private Color idleColor       = new Color(0.22f, 0.06f, 0.06f, 0.95f);
    private Color windupColor     = new Color(0.6f,  0.08f, 0.04f, 1f);
    private Color activeColor     = new Color(0.9f,  0.2f,  0.04f, 1f);
    private Color recoveryColor   = new Color(0.14f, 0.1f,  0.09f, 0.8f);
    private Color defeatedColor   = new Color(0.08f, 0.08f, 0.08f, 0.4f);
    private Color counterHitColor = new Color(1f,    0.95f, 0.3f,  1f);
    private Color staggerColor    = new Color(0.7f,  0.6f,  0.2f,  1f);
    private Color heavyColor      = new Color(0.5f,  0.03f, 0.03f, 1f);
    private Color feintColor      = new Color(0.4f,  0.1f,  0.5f,  1f); 

    // telegraph
    private GameObject telegraphLeft_GO;
    private GameObject telegraphRight_GO;
    private SpriteRenderer telegraphLeftSR;
    private SpriteRenderer telegraphRightSR;

    private GameObject dangerRing;
    private SpriteRenderer dangerRingSR;

    private GameObject attackTypeIndicator;
    private SpriteRenderer attackTypeIndicatorSR;

    private Vector3 homePosition;
    private Vector3 targetPosition;
    private float   lungeSpeed   = 14f;
    private CombatState currentState = CombatState.Idle;
    private bool isDefeated;
    private float swayTimer;

    void Start()
    {
        if (bodyRenderer == null) CreateBossSprite();

        homePosition   = transform.position;
        targetPosition = homePosition;
        allRenderers   = GetComponentsInChildren<SpriteRenderer>();

        CreateTelegraphIndicators();
        CreateDangerRing();
        CreateAttackTypeIndicator();

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

        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * lungeSpeed);

        swayTimer += Time.deltaTime * 0.8f;
        transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(swayTimer) * 0.45f);

        if (currentState == CombatState.Windup && bodyRenderer != null)
        {
            AttackData atk = CombatManager.Instance.CurrentAttack;
            float baseSpeed = (atk?.attackType == AttackType.Heavy) ? 3f : 7f;
            float pulse = (Mathf.Sin(Time.time * baseSpeed) + 1f) / 2f;
            Color target = (atk?.attackType == AttackType.Heavy) ? heavyColor :
                           (atk?.attackType == AttackType.Feint)  ? feintColor : windupColor;
            bodyRenderer.color = Color.Lerp(idleColor, target, pulse);

            if (eyeRenderers != null)
                foreach (var eye in eyeRenderers)
                    eye.color = Color.Lerp(
                        new Color(1f, 0.3f, 0.1f, 0.6f),
                        new Color(1f, 0.7f, 0.1f, 1f), pulse);

            UpdateDangerRing(atk);
        }

        UpdateTelegraph();
    }

    void CreateBossSprite()
    {
        GameObject body = new GameObject("BossBody");
        body.transform.SetParent(transform, false);
        bodyRenderer = body.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = PlayerVisuals.CreateRect(1.5f, 2.2f);
        bodyRenderer.color  = idleColor;
        bodyRenderer.sortingOrder = 1;

        CreatePart("ShoulderL", new Vector3(-0.85f, 0.55f, 0f), new Vector3(0.5f, 0.3f, 1f),
            new Color(0.18f, 0.05f, 0.05f, 0.9f), 2);
        CreatePart("ShoulderR", new Vector3( 0.85f, 0.55f, 0f), new Vector3(0.5f, 0.3f, 1f),
            new Color(0.18f, 0.05f, 0.05f, 0.9f), 2);

        GameObject helmet = new GameObject("Helmet");
        helmet.transform.SetParent(transform, false);
        helmet.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        SpriteRenderer helmetSR = helmet.AddComponent<SpriteRenderer>();
        helmetSR.sprite = PlayerVisuals.CreateRect(0.65f, 0.65f);
        helmetSR.color  = new Color(0.16f, 0.05f, 0.05f, 0.95f);
        helmetSR.sortingOrder = 3;

        CreatePart("HornL", new Vector3(-0.25f, 1.75f, 0f), new Vector3(0.12f, 0.5f, 1f),
            new Color(0.12f, 0.04f, 0.04f, 0.9f), 4);
        CreatePart("HornR", new Vector3( 0.25f, 1.75f, 0f), new Vector3(0.12f, 0.5f, 1f),
            new Color(0.12f, 0.04f, 0.04f, 0.9f), 4);

        eyeRenderers = new SpriteRenderer[2];
        eyeRenderers[0] = CreateEye(new Vector3(-0.14f, 1.32f, 0f));
        eyeRenderers[1] = CreateEye(new Vector3( 0.14f, 1.32f, 0f));

        GameObject weapon = new GameObject("Weapon");
        weapon.transform.SetParent(transform, false);
        weapon.transform.localPosition = new Vector3(-0.6f, 0.2f, 0f);
        weapon.transform.localRotation = Quaternion.Euler(0, 0, 30f);
        weaponRenderer = weapon.AddComponent<SpriteRenderer>();
        weaponRenderer.sprite = PlayerVisuals.CreateRect(0.14f, 1.5f);
        weaponRenderer.color  = new Color(0.45f, 0.38f, 0.32f, 0.9f);
        weaponRenderer.sortingOrder = 4;

        CreatePart("Weapon2", new Vector3(0.6f, 0.2f, 0f), new Vector3(0.14f, 1.5f, 1f),
            new Color(0.45f, 0.38f, 0.32f, 0f), 4); // starts invisible
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
        telegraphLeft_GO = BuildArrow("TelegraphLeft",  new Vector3(-3.2f, -0.5f, 0f), true);
        telegraphRight_GO = BuildArrow("TelegraphRight", new Vector3( 3.2f, -0.5f, 0f), false);
        telegraphLeftSR   = telegraphLeft_GO.GetComponent<SpriteRenderer>();
        telegraphRightSR  = telegraphRight_GO.GetComponent<SpriteRenderer>();
        telegraphLeft_GO.SetActive(false);
        telegraphRight_GO.SetActive(false);
    }

    GameObject BuildArrow(string name, Vector3 pos, bool pointLeft)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform.parent ?? transform, true);
        go.transform.position = pos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateArrowSprite(pointLeft);
        sr.color  = new Color(1f, 0.3f, 0.1f, 0.5f);
        sr.sortingOrder = 20;
        return go;
    }

    void CreateDangerRing()
    {
        dangerRing = new GameObject("DangerRing");
        dangerRing.transform.SetParent(transform, false);
        dangerRingSR = dangerRing.AddComponent<SpriteRenderer>();
        dangerRingSR.sprite = PlayerVisuals.CreateCircle(0.5f);
        dangerRingSR.color  = Color.clear;
        dangerRingSR.sortingOrder = 0;
        dangerRing.SetActive(false);
    }

    void CreateAttackTypeIndicator()
    {
        attackTypeIndicator = new GameObject("AttackTypeIndicator");
        attackTypeIndicator.transform.SetParent(transform, false);
        attackTypeIndicator.transform.localPosition = new Vector3(0f, 2.4f, 0f);
        attackTypeIndicatorSR = attackTypeIndicator.AddComponent<SpriteRenderer>();
        attackTypeIndicatorSR.sprite = PlayerVisuals.CreateCircle(0.1f);
        attackTypeIndicatorSR.color  = Color.clear;
        attackTypeIndicatorSR.sortingOrder = 10;
    }

    void UpdateDangerRing(AttackData atk)
    {
        if (atk == null) return;
        dangerRing.SetActive(true);

        float baseScale = (atk.attackType == AttackType.Heavy) ? 2.0f : 1.2f;
        float maxScale  = (atk.attackType == AttackType.Heavy) ? 5.0f : 3.5f;
        float elapsed   = Time.time - GetWindupStart();
        float chargeT   = Mathf.Clamp01(elapsed / atk.telegraphDuration);

        dangerRing.transform.localScale = Vector3.one * Mathf.Lerp(baseScale, maxScale, chargeT);

        Color ringColor = (atk.attackType == AttackType.Heavy)
            ? new Color(0.9f, 0.05f, 0.05f, Mathf.Lerp(0.08f, 0.3f, chargeT))
            : new Color(0.85f, 0.15f, 0.05f, Mathf.Lerp(0.04f, 0.22f, chargeT));
        dangerRingSR.color = ringColor;
    }

    void UpdateTelegraph()
    {
        if (CombatManager.Instance.CurrentAttack == null) return;

        bool show = currentState == CombatState.Windup || currentState == CombatState.Active;
        DodgeDirection dir = CombatManager.Instance.CurrentAttack.requiredDodge;
        AttackType type    = CombatManager.Instance.CurrentAttack.attackType;

        DodgeDirection shownDir = dir;
        if (type == AttackType.Feint && currentState == CombatState.Windup)
            shownDir = (dir == DodgeDirection.Left) ? DodgeDirection.Right : DodgeDirection.Left;

        telegraphLeft_GO.SetActive(show  && shownDir == DodgeDirection.Right);
        telegraphRight_GO.SetActive(show && shownDir == DodgeDirection.Left);

        if (!show) return;

        if (currentState == CombatState.Active)
        {
            Color ac = (type == AttackType.Heavy)
                ? new Color(1f, 0.3f, 0.05f, 1f)   
                : new Color(1f, 0.05f, 0.05f, 1f);  
            telegraphLeftSR.color  = ac;
            telegraphRightSR.color = ac;
            telegraphLeft_GO.transform.localScale  = Vector3.one * 1.5f;
            telegraphRight_GO.transform.localScale = Vector3.one * 1.5f;
        }
        else
        {
            float pulse = (Mathf.Sin(Time.time * 8f) + 1f) / 2f;
            Color pc = type == AttackType.Feint
                ? Color.Lerp(new Color(0.6f, 0.1f, 0.8f, 0.2f), new Color(0.8f, 0.2f, 1f, 0.9f), pulse)
                : Color.Lerp(new Color(1f, 0.3f, 0.1f, 0.2f),   new Color(1f, 0.5f, 0.1f, 0.9f), pulse);
            telegraphLeftSR.color  = pc;
            telegraphRightSR.color = pc;
            telegraphLeft_GO.transform.localScale  = Vector3.one;
            telegraphRight_GO.transform.localScale = Vector3.one;
        }
    }

    void OnStateChanged(CombatState state)
    {
        currentState = state;
        if (isDefeated) return;

        AttackData atk = CombatManager.Instance.CurrentAttack;

        switch (state)
        {
            case CombatState.Idle:
                if (bodyRenderer) bodyRenderer.color = idleColor;
                targetPosition = homePosition;
                dangerRing.SetActive(false);
                attackTypeIndicatorSR.color = Color.clear;
                ResetWeapon();
                break;

            case CombatState.Windup:
                dangerRing.SetActive(true);
                windupStartTime = Time.time;

                if (atk != null)
                {
                    attackTypeIndicatorSR.color = atk.attackType switch
                    {
                        AttackType.Heavy  => new Color(1f, 0.3f, 0.05f, 0.9f), 
                        AttackType.Feint  => new Color(0.7f, 0.1f, 0.9f, 0.9f), 
                        AttackType.Double => new Color(0.2f, 0.8f, 1f,  0.9f),  
                        _                 => new Color(1f, 0.2f, 0.1f,  0.9f), 
                    };
                }

                if (weaponRenderer != null && atk != null)
                {
                    float sign = (atk.requiredDodge == DodgeDirection.Left) ? 1f : -1f;
                    float pullAngle = (atk.attackType == AttackType.Heavy) ? 90f : 75f;
                    weaponRenderer.transform.localRotation = Quaternion.Euler(0, 0, sign * pullAngle);
                }

                if (atk?.attackType == AttackType.Feint)
                    StartCoroutine(FeintEyeFlash());
                break;

            case CombatState.Active:
                if (bodyRenderer) bodyRenderer.color = activeColor;
                dangerRing.SetActive(false);
                attackTypeIndicatorSR.color = Color.clear;

                if (atk != null)
                {
                    float atkSign = (atk.requiredDodge == DodgeDirection.Left) ? 1f : -1f;
                    float lungeAmt = (atk.attackType == AttackType.Heavy) ? 1.6f : 1.1f;
                    targetPosition = homePosition + Vector3.down * lungeAmt;
                    if (weaponRenderer)
                        weaponRenderer.transform.localRotation = Quaternion.Euler(0, 0, -atkSign * 55f);
                }
                break;

            case CombatState.Recovery:
                if (bodyRenderer) bodyRenderer.color = recoveryColor;
                targetPosition = homePosition;
                ResetWeapon();
                break;

            case CombatState.PerfectWindow:
                if (bodyRenderer) bodyRenderer.color = staggerColor;
                targetPosition = homePosition + Vector3.up * 0.45f;
                StartCoroutine(StaggerShake());
                break;

            case CombatState.Counter:
                targetPosition = homePosition + Vector3.up * 0.8f;
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
        for (int i = 0; i < 3; i++)
        {
            if (bodyRenderer) bodyRenderer.color = counterHitColor;
            transform.localScale = Vector3.one * 1.18f;
            yield return new WaitForSeconds(0.06f);
            if (bodyRenderer) bodyRenderer.color = idleColor;
            transform.localScale = Vector3.one;
            yield return new WaitForSeconds(0.04f);
        }
    }

    IEnumerator StaggerShake()
    {
        Vector3 origin = targetPosition;
        for (int i = 0; i < 6; i++)
        {
            targetPosition = origin + (Vector3)Random.insideUnitCircle * 0.14f;
            yield return new WaitForSeconds(0.04f);
        }
        targetPosition = origin;
    }

    IEnumerator FeintEyeFlash()
    {
        if (eyeRenderers == null) yield break;
        yield return new WaitForSeconds(0.2f);
        for (int i = 0; i < 3; i++)
        {
            foreach (var e in eyeRenderers)
                e.color = new Color(0.8f, 0.2f, 1f, 1f);
            yield return new WaitForSeconds(0.08f);
            foreach (var e in eyeRenderers)
                e.color = new Color(1f, 0.3f, 0.1f, 0.8f);
            yield return new WaitForSeconds(0.08f);
        }
    }

    void OnDefeated()
    {
        isDefeated = true;
        if (telegraphLeft_GO)  telegraphLeft_GO.SetActive(false);
        if (telegraphRight_GO) telegraphRight_GO.SetActive(false);
        dangerRing.SetActive(false);
        attackTypeIndicatorSR.color = Color.clear;
        StartCoroutine(DefeatShatter());
    }

    IEnumerator DefeatShatter()
    {
        foreach (var sr in allRenderers) sr.color = Color.white;
        yield return new WaitForSeconds(0.1f);

        var children = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform) children.Add(child);

        var velocities = new Vector3[children.Count];
        for (int i = 0; i < children.Count; i++)
            velocities[i] = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(-0.5f, 3.5f), 0f);

        float t = 0f;
        while (t < 1.3f)
        {
            t += Time.deltaTime;
            for (int i = 0; i < children.Count; i++)
            {
                if (!children[i]) continue;
                children[i].position += velocities[i] * Time.deltaTime;
                velocities[i] += Vector3.down * 5f * Time.deltaTime;
                SpriteRenderer sr = children[i].GetComponent<SpriteRenderer>();
                if (sr) { Color c = sr.color; c.a = Mathf.Lerp(1f, 0f, t / 1.3f); sr.color = c; }
            }
            yield return null;
        }
    }

    void ResetWeapon()
    {
        if (weaponRenderer)
            weaponRenderer.transform.localRotation = Quaternion.Euler(0, 0, 30f);
    }

    private float windupStartTime;
    float GetWindupStart() => windupStartTime;

    static Sprite CreateArrowSprite(bool pointLeft)
    {
        int w = 64, h = 40;
        Texture2D tex = new Texture2D(w, h);
        tex.filterMode = FilterMode.Point;
        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int ax = pointLeft ? x : (w - 1 - x);
                bool shaft = ax >= h / 2 && y >= h / 4 && y < h * 3 / 4;
                float hx = (float)ax / (h / 2f);
                bool head = ax < h / 2 && Mathf.Abs(y - h / 2f) < hx * (h / 2f);
                pixels[y * w + x] = (shaft || head) ? Color.white : Color.clear;
            }
        tex.SetPixels(pixels); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
    }
}
