using System.Collections;
using UnityEngine;
using UnityEngine.Events;

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
    private Color unblockableColor = new Color(0.85f, 0.85f, 0.85f, 1f);

    // telegraph
    private GameObject telegraphLeft_GO;
    private GameObject telegraphRight_GO;
    private SpriteRenderer telegraphLeftSR;
    private SpriteRenderer telegraphRightSR;

    private GameObject dangerRing;
    private SpriteRenderer dangerRingSR;

    private GameObject attackTypeIndicator;
    private SpriteRenderer attackTypeIndicatorSR;

    // aura + slash fx
    private SpriteRenderer auraSR;
    private GameObject slashGO;
    private SpriteRenderer slashSR;

    // ---- layered motion state ----
    private Vector3 homePosition;
    private Vector3 smoothPos;          // critically-damped base position
    private Vector3 posVel;
    private Vector3 lungeOffset;        // where the body wants to be relative to home
    private Vector3 shakeOffset;        // additive impact shake, decays to zero
    private float   posSmoothTime = 0.09f;
    private const float idleSmoothTime = 0.09f;

    private float swayTimer;
    private float breathTimer;
    private float scalePunch = 1f;      // coroutine-driven scale pop, multiplied onto breathing

    // weapon swing (smoothly arced, not snapped)
    private float weaponAngle = 30f;
    private float weaponTargetAngle = 30f;
    private float weaponTurnSpeed = 200f;

    private CombatState currentState = CombatState.Idle;
    private bool isDefeated;

    // phase escalation
    private bool isEnraged;
    private bool isFinal;
    private BossController boss;

    private float windupStartTime;
    float GetWindupStart() => windupStartTime;

    void Start()
    {
        if (bodyRenderer == null) CreateBossSprite();

        homePosition   = transform.position;
        smoothPos      = homePosition;
        allRenderers   = GetComponentsInChildren<SpriteRenderer>();

        CreateAura();
        CreateSlash();
        CreateTelegraphIndicators();
        CreateDangerRing();
        CreateAttackTypeIndicator();

        CombatManager.Instance.OnStateChanged  += OnStateChanged;
        CombatManager.Instance.OnCounterLanded += OnCounterHit;
        CombatManager.Instance.OnBossDefeated  += OnDefeated;
        CombatManager.Instance.OnFeintSwitch   += OnFeintSwitch;

        boss = GetComponent<BossController>();
        if (boss != null)
        {
            boss.OnRageEntered.AddListener(OnRage);
            boss.OnFinalPhaseEntered.AddListener(OnFinal);
        }
    }

    void OnDestroy()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnStateChanged  -= OnStateChanged;
            CombatManager.Instance.OnCounterLanded -= OnCounterHit;
            CombatManager.Instance.OnBossDefeated  -= OnDefeated;
            CombatManager.Instance.OnFeintSwitch   -= OnFeintSwitch;
        }
        if (boss != null)
        {
            boss.OnRageEntered.RemoveListener(OnRage);
            boss.OnFinalPhaseEntered.RemoveListener(OnFinal);
        }
    }

    void Update()
    {
        if (isDefeated) return;
        float dt = Time.deltaTime;

        // ---- idle life: sway + breathing, faster/wilder as the fight escalates ----
        float swaySpeed   = isFinal ? 1.9f : isEnraged ? 1.4f : 0.8f;
        float breathSpeed = isFinal ? 2.4f : isEnraged ? 1.8f : 1.3f;
        float swayAmp     = isFinal ? 1.3f : isEnraged ? 0.85f : 0.45f;
        swayTimer   += dt * swaySpeed;
        breathTimer += dt * breathSpeed;

        // ---- windup buildup (anticipation, body swell, charging eyes) ----
        if (currentState == CombatState.Windup) UpdateWindupVisuals();
        else                                    UpdateIdleEyes();

        // ---- weapon swing: arc toward its target angle instead of snapping ----
        weaponAngle = Mathf.MoveTowardsAngle(weaponAngle, weaponTargetAngle, weaponTurnSpeed * dt);
        if (weaponRenderer)
            weaponRenderer.transform.localRotation = Quaternion.Euler(0, 0, weaponAngle);

        // ---- position: smoothed base + decaying shake + breathing bob ----
        posSmoothTime = Mathf.MoveTowards(posSmoothTime, idleSmoothTime, dt * 0.4f);
        smoothPos     = Vector3.SmoothDamp(smoothPos, homePosition + lungeOffset, ref posVel, posSmoothTime);
        shakeOffset   = Vector3.Lerp(shakeOffset, Vector3.zero, dt * 12f);
        float bob     = Mathf.Sin(breathTimer) * 0.05f;
        transform.position = smoothPos + shakeOffset + new Vector3(0f, bob, 0f);

        // ---- breathing squash-and-stretch (times any active scale pop) ----
        float breath = 1f + Mathf.Sin(breathTimer) * 0.025f;
        transform.localScale = new Vector3(breath * scalePunch, (1f / breath) * scalePunch, 1f);
        transform.rotation   = Quaternion.Euler(0f, 0f, Mathf.Sin(swayTimer) * swayAmp);

        UpdateAura();
        UpdateTelegraph();
    }

    void UpdateWindupVisuals()
    {
        AttackData atk = CombatManager.Instance.CurrentAttack;
        if (atk == null) return;

        float windupDur = CombatManager.Instance.CurrentWindupDuration;
        float charge    = Mathf.Clamp01((Time.time - windupStartTime) / Mathf.Max(0.0001f, windupDur));
        float eased     = charge * charge;

        float pulseSpeed = (atk.attackType == AttackType.Heavy) ? 4f : 8f;
        float pulse      = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;

        Color target = (atk.attackType == AttackType.Heavy)       ? heavyColor :
                       (atk.attackType == AttackType.Feint)       ? feintColor :
                       (atk.attackType == AttackType.Unblockable) ? unblockableColor : windupColor;

        if (bodyRenderer != null)
            bodyRenderer.color = Color.Lerp(IdleTint(), target, Mathf.Max(pulse * 0.7f, eased));

        if (eyeRenderers != null)
        {
            Color dim    = new Color(1f, 0.3f, 0.1f, 0.6f);
            Color bright = new Color(1f, 0.8f, 0.15f, 1f);
            float t = Mathf.Max(pulse, eased);
            foreach (var eye in eyeRenderers)
            {
                eye.color = Color.Lerp(dim, bright, t);
                eye.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.6f, eased);
            }
        }

        // anticipation: pull up/back over the last ~45% of the windup before striking
        float anticip = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, charge));
        lungeOffset = Vector3.up * anticip * 0.3f;

        UpdateDangerRing(atk);
    }

    void UpdateIdleEyes()
    {
        if (eyeRenderers == null) return;
        // between attacks the eyes smoulder; brighter and pulsing once enraged
        float p = (Mathf.Sin(Time.time * (isFinal ? 7f : 4f)) + 1f) / 2f;
        Color lo, hi;
        if (isFinal)      { lo = new Color(1f, 0.5f, 0.15f, 0.8f); hi = new Color(1f, 0.9f, 0.4f, 1f); }
        else if (isEnraged){ lo = new Color(1f, 0.3f, 0.1f, 0.7f); hi = new Color(1f, 0.55f, 0.15f, 1f); }
        else              { lo = new Color(1f, 0.3f, 0.1f, 0.7f); hi = new Color(1f, 0.3f, 0.1f, 0.85f); }
        foreach (var eye in eyeRenderers)
        {
            eye.color = Color.Lerp(lo, hi, (isEnraged || isFinal) ? p : 0.4f);
            eye.transform.localScale = Vector3.one;
        }
    }

    Color IdleTint()
        => isFinal   ? new Color(0.42f, 0.05f, 0.04f, 0.97f)
         : isEnraged ? new Color(0.32f, 0.05f, 0.05f, 0.96f)
         : idleColor;

    // ======================================================================
    // sprite + fx creation
    // ======================================================================

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

    void CreateAura()
    {
        GameObject aura = new GameObject("Aura");
        aura.transform.SetParent(transform, false);
        aura.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        auraSR = aura.AddComponent<SpriteRenderer>();
        auraSR.sprite = PlayerVisuals.CreateCircle(1.7f);
        auraSR.color  = Color.clear;
        auraSR.sortingOrder = -2;
    }

    void CreateSlash()
    {
        slashGO = new GameObject("Slash");
        slashGO.transform.SetParent(transform, false);
        slashGO.transform.localPosition = new Vector3(0f, -0.6f, 0f);
        slashSR = slashGO.AddComponent<SpriteRenderer>();
        slashSR.sprite = PlayerVisuals.CreateRect(2.4f, 0.18f);
        slashSR.color  = Color.clear;
        slashSR.sortingOrder = 8;
        slashGO.SetActive(false);
    }

    void CreateTelegraphIndicators()
    {
        telegraphLeft_GO  = BuildArrow("TelegraphLeft",  new Vector3(-3.2f, -0.5f, 0f), true);
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

    // ======================================================================
    // per-frame fx
    // ======================================================================

    void UpdateAura()
    {
        if (auraSR == null) return;

        float baseA = isFinal ? 0.30f : isEnraged ? 0.18f : 0.06f;
        float speed = isFinal ? 6f : isEnraged ? 4f : 2f;
        float p     = (Mathf.Sin(Time.time * speed) + 1f) / 2f;
        float a     = baseA * Mathf.Lerp(0.5f, 1f, p);
        if (currentState == CombatState.Windup || currentState == CombatState.Active) a *= 1.7f;

        Color c = isFinal   ? new Color(1f, 0.45f, 0.1f)
                : isEnraged ? new Color(0.95f, 0.12f, 0.06f)
                            : new Color(0.5f, 0.06f, 0.06f);
        c.a = a;
        auraSR.color = c;

        float scl = (isFinal ? 1.5f : isEnraged ? 1.25f : 1f) * Mathf.Lerp(0.95f, 1.08f, p);
        auraSR.transform.localScale = Vector3.one * scl;
    }

    void UpdateDangerRing(AttackData atk)
    {
        if (atk == null) return;
        dangerRing.SetActive(true);

        float baseScale = (atk.attackType == AttackType.Heavy) ? 2.0f : 1.2f;
        float maxScale  = (atk.attackType == AttackType.Heavy) ? 5.0f : 3.5f;
        float elapsed   = Time.time - GetWindupStart();
        float windupDur = CombatManager.Instance.CurrentWindupDuration;
        float chargeT   = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, windupDur));
        float eased     = 1f - Mathf.Pow(1f - chargeT, 2f);   // ease-out so it slams shut

        dangerRing.transform.localScale = Vector3.one * Mathf.Lerp(baseScale, maxScale, eased);
        dangerRing.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * -90f);

        // sharpen and flicker as the window closes (the "it's coming" read)
        float lockPulse = chargeT > 0.7f ? (Mathf.Sin(Time.time * 30f) + 1f) / 2f * (chargeT - 0.7f) / 0.3f : 0f;
        float alpha = Mathf.Lerp(0.05f, 0.28f, eased) + lockPulse * 0.25f;

        Color ringColor = (atk.attackType == AttackType.Heavy)
            ? new Color(0.95f, 0.05f, 0.05f, alpha)
            : new Color(0.9f,  0.15f, 0.05f, alpha);
        dangerRingSR.color = ringColor;
    }

    void UpdateTelegraph()
    {
        if (CombatManager.Instance.CurrentAttack == null) return;

        bool show = currentState == CombatState.Windup || currentState == CombatState.Active;
        AttackType type = CombatManager.Instance.CurrentAttack.attackType;
        DodgeDirection shownDir = CombatManager.Instance.CurrentTelegraphDirection;

        bool showArrows = show && type != AttackType.Unblockable;
        telegraphLeft_GO.SetActive(showArrows  && shownDir == DodgeDirection.Left);
        telegraphRight_GO.SetActive(showArrows && shownDir == DodgeDirection.Right);

        if (!show) return;

        if (currentState == CombatState.Active)
        {
            Color ac = (type == AttackType.Heavy)
                ? new Color(1f, 0.3f, 0.05f, 1f)
                : new Color(1f, 0.05f, 0.05f, 1f);
            telegraphLeftSR.color  = ac;
            telegraphRightSR.color = ac;
            telegraphLeft_GO.transform.localScale  = Vector3.one * 1.8f;
            telegraphRight_GO.transform.localScale = Vector3.one * 1.8f;
        }
        else
        {
            // shake harder and grow as the windup charges, so the read pops
            float windupDur = CombatManager.Instance.CurrentWindupDuration;
            float charge    = Mathf.Clamp01((Time.time - windupStartTime) / Mathf.Max(0.0001f, windupDur));
            float pulse     = (Mathf.Sin(Time.time * (8f + charge * 8f)) + 1f) / 2f;

            Color pc = type == AttackType.Feint
                ? Color.Lerp(new Color(0.6f, 0.1f, 0.8f, 0.25f), new Color(0.85f, 0.25f, 1f, 1f), pulse)
                : Color.Lerp(new Color(1f, 0.3f, 0.1f, 0.25f),   new Color(1f, 0.55f, 0.1f, 1f), pulse);
            telegraphLeftSR.color  = pc;
            telegraphRightSR.color = pc;

            float s = Mathf.Lerp(0.95f, 1.35f, pulse) * Mathf.Lerp(1f, 1.25f, charge);
            telegraphLeft_GO.transform.localScale  = Vector3.one * s;
            telegraphRight_GO.transform.localScale = Vector3.one * s;
        }
    }

    // ======================================================================
    // state reactions
    // ======================================================================

    void OnStateChanged(CombatState state)
    {
        currentState = state;
        if (isDefeated) return;

        AttackData atk = CombatManager.Instance.CurrentAttack;

        switch (state)
        {
            case CombatState.Idle:
                if (bodyRenderer) bodyRenderer.color = IdleTint();
                lungeOffset = Vector3.zero;
                dangerRing.SetActive(false);
                attackTypeIndicatorSR.color = Color.clear;
                SetWeapon(30f, 200f);
                break;

            case CombatState.Windup:
                dangerRing.SetActive(true);
                windupStartTime = Time.time;
                lungeOffset = Vector3.zero;

                if (atk != null && !CombatManager.Instance.hideAttackTells)
                {
                    attackTypeIndicatorSR.color = atk.attackType switch
                    {
                        AttackType.Heavy       => new Color(1f, 0.3f, 0.05f, 0.9f),
                        AttackType.Feint       => new Color(0.7f, 0.1f, 0.9f, 0.9f),
                        AttackType.Double      => new Color(0.2f, 0.8f, 1f,  0.9f),
                        AttackType.Unblockable => new Color(1f, 1f, 1f, 0.95f),
                        _                      => new Color(1f, 0.2f, 0.1f,  0.9f),
                    };
                }
                else if (atk != null)
                {
                    attackTypeIndicatorSR.color = Color.clear;   // final phase: read timing, not type
                }

                if (atk != null)
                {
                    // wind the weapon back slowly (anticipation)
                    float sign = (atk.requiredDodge == DodgeDirection.Left) ? 1f : -1f;
                    float pullAngle = (atk.attackType == AttackType.Heavy) ? 95f : 75f;
                    SetWeapon(sign * pullAngle, 150f);
                }
                break;

            case CombatState.Active:
                if (bodyRenderer) bodyRenderer.color = activeColor;
                dangerRing.SetActive(false);
                attackTypeIndicatorSR.color = Color.clear;

                if (atk != null)
                {
                    float atkSign  = (atk.requiredDodge == DodgeDirection.Left) ? 1f : -1f;
                    bool  heavy    = atk.attackType == AttackType.Heavy;
                    float lungeAmt = heavy ? 1.7f : 1.15f;

                    posSmoothTime = 0.03f;   // snap into the strike
                    lungeOffset   = Vector3.down * lungeAmt + Vector3.right * (-atkSign) * 0.25f;
                    SetWeapon(-atkSign * 55f, 900f);   // fast slash-through
                    AddShake(heavy ? 0.32f : 0.2f);
                    TriggerSlash(-atkSign, heavy);
                }
                break;

            case CombatState.Recovery:
                if (bodyRenderer) bodyRenderer.color = recoveryColor;
                lungeOffset = Vector3.zero;
                SetWeapon(30f, 260f);
                break;

            case CombatState.PerfectWindow:
                if (bodyRenderer) bodyRenderer.color = staggerColor;
                lungeOffset = Vector3.up * 0.45f;
                SetWeapon(10f, 400f);
                StartCoroutine(StaggerShake());
                break;

            case CombatState.Counter:
                lungeOffset = Vector3.up * 0.8f;
                break;
        }
    }

    void OnCounterHit()
    {
        if (isDefeated) return;
        AddShake(0.28f);
        StartCoroutine(CounterFlash());
    }

    IEnumerator CounterFlash()
    {
        for (int i = 0; i < 3; i++)
        {
            if (bodyRenderer) bodyRenderer.color = counterHitColor;
            scalePunch = 1.2f;
            yield return new WaitForSeconds(0.06f);
            if (bodyRenderer) bodyRenderer.color = IdleTint();
            scalePunch = 1f;
            yield return new WaitForSeconds(0.04f);
        }
    }

    IEnumerator StaggerShake()
    {
        for (int i = 0; i < 6; i++)
        {
            AddShake(0.16f);
            yield return new WaitForSeconds(0.05f);
        }
    }

    void OnFeintSwitch()
    {
        if (isDefeated) return;
        AddShake(0.12f);
        StartCoroutine(FeintEyeFlash());
    }

    IEnumerator FeintEyeFlash()
    {
        if (eyeRenderers == null) yield break;
        for (int i = 0; i < 3; i++)
        {
            foreach (var e in eyeRenderers) e.color = new Color(0.85f, 0.2f, 1f, 1f);
            yield return new WaitForSeconds(0.08f);
            foreach (var e in eyeRenderers) e.color = new Color(1f, 0.3f, 0.1f, 0.8f);
            yield return new WaitForSeconds(0.08f);
        }
    }

    // ---- phase escalation ----

    void OnRage()
    {
        if (isEnraged) return;
        isEnraged = true;
        StartCoroutine(PhaseRoar(new Color(1f, 0.2f, 0.05f)));
    }

    void OnFinal()
    {
        isFinal = true;
        StartCoroutine(PhaseRoar(new Color(1f, 0.6f, 0.1f)));
    }

    IEnumerator PhaseRoar(Color flash)
    {
        AddShake(0.5f);
        for (int i = 0; i < 5; i++)
        {
            if (bodyRenderer) bodyRenderer.color = flash;
            scalePunch = 1.24f;
            yield return new WaitForSeconds(0.05f);
            if (bodyRenderer) bodyRenderer.color = IdleTint();
            scalePunch = 1f;
            yield return new WaitForSeconds(0.05f);
        }
    }

    // ---- defeat ----

    void OnDefeated()
    {
        isDefeated = true;
        if (telegraphLeft_GO)  telegraphLeft_GO.SetActive(false);
        if (telegraphRight_GO) telegraphRight_GO.SetActive(false);
        dangerRing.SetActive(false);
        if (slashGO) slashGO.SetActive(false);
        if (auraSR) auraSR.color = Color.clear;
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
                children[i].Rotate(0f, 0f, velocities[i].x * 60f * Time.deltaTime);
                velocities[i] += Vector3.down * 5f * Time.deltaTime;
                SpriteRenderer sr = children[i].GetComponent<SpriteRenderer>();
                if (sr) { Color c = sr.color; c.a = Mathf.Lerp(1f, 0f, t / 1.3f); sr.color = c; }
            }
            yield return null;
        }
    }

    // ======================================================================
    // helpers
    // ======================================================================

    void SetWeapon(float targetAngle, float turnSpeed)
    {
        weaponTargetAngle = targetAngle;
        weaponTurnSpeed   = turnSpeed;
    }

    void ResetWeapon() => SetWeapon(30f, 200f);

    void AddShake(float magnitude)
        => shakeOffset += (Vector3)Random.insideUnitCircle * magnitude;

    void TriggerSlash(float dirSign, bool heavy)
    {
        if (slashGO == null) return;
        StartCoroutine(SlashFlash(dirSign, heavy));
    }

    IEnumerator SlashFlash(float dirSign, bool heavy)
    {
        slashGO.SetActive(true);
        slashGO.transform.localPosition = new Vector3(0f, -0.7f, 0f);
        slashGO.transform.localRotation = Quaternion.Euler(0f, 0f, dirSign > 0 ? 22f : -22f);

        Color hot = heavy ? new Color(1f, 0.45f, 0.1f, 0.95f) : new Color(1f, 0.85f, 0.7f, 0.85f);
        float t = 0f, dur = 0.18f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = t / dur;
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            float w = Mathf.Lerp(0.3f, heavy ? 2.3f : 1.8f, ease);
            slashGO.transform.localScale = new Vector3(w, Mathf.Lerp(1.5f, 0.4f, k), 1f);
            Color c = hot; c.a = Mathf.Lerp(hot.a, 0f, k);
            slashSR.color = c;
            yield return null;
        }
        slashSR.color = Color.clear;
        slashGO.SetActive(false);
    }

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