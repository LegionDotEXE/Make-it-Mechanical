using UnityEngine;
using UnityEngine.Events;

public class BossController : MonoBehaviour
{
    [Header("Boss Health")]
    public float maxHealth     = 200f;
    public float counterDamage = 25f;
    public float currentHealth { get; private set; }

    [Header("Rage Phase")]
    public float rageThreshold  = 0.4f;
    public float rageSpeedMult  = 0.65f;
    public float rageDamageMult = 1.5f;
    public bool  IsEnraged      => isEnraged;
    private bool isEnraged      = false;

    [Header("Combos")]
    public ComboPattern[] combos;

    [Header("Loop Settings")]
    public float delayBetweenAttacks = 0.8f;
    public float delayBetweenCombos  = 1.2f;

    [Header("Events")]
    public UnityEvent<float> OnBossHealthChanged = new UnityEvent<float>();
    public UnityEvent        OnBossDefeated      = new UnityEvent();
    public UnityEvent        OnAttackWindup      = new UnityEvent();
    public UnityEvent        OnAttackActive       = new UnityEvent();
    public UnityEvent        OnAttackRecovery    = new UnityEvent();
    public UnityEvent        OnRageEntered       = new UnityEvent();

    private bool  combatRunning = false;
    private float idleTimer     = 0f;

    // Combo tracking
    private ComboPattern currentCombo;
    private int          comboAttackIndex;
    private bool         waitingForComboRecovery;
    private int          lastComboIndex = -1;

    // Reused scratch instance for modified attacks (rage / mid-combo shortened recovery).
    private AttackData activeRageInstance;

    [Header("Mid-Combo Timing")]
    [Tooltip("Recovery duration for mid-combo attacks (shorter = tighter combo feel).")]
    public float midComboRecovery = 0.15f;

    void Start()
    {
        currentHealth = maxHealth;

        if (combos == null || combos.Length == 0)
        {
            Debug.LogError("[BossController] No combos assigned!");
            return;
        }

        CombatManager.Instance.OnCounterLanded += TakeCounterDamage;
        CombatManager.Instance.OnStateChanged  += HandleStateChanged;
        CombatManager.Instance.OnBossDefeated  += HandleDefeated;
        CombatManager.Instance.OnPlayerDeath   += HandlePlayerDied;

        combatRunning = true;
        StartNextCombo();
    }

    void OnDestroy()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnCounterLanded -= TakeCounterDamage;
            CombatManager.Instance.OnStateChanged  -= HandleStateChanged;
            CombatManager.Instance.OnBossDefeated  -= HandleDefeated;
            CombatManager.Instance.OnPlayerDeath   -= HandlePlayerDied;
        }

        if (activeRageInstance != null)
            Destroy(activeRageInstance);
    }

    void Update()
    {
        if (!combatRunning) return;

        CombatManager.Instance.Tick();

        if (CombatManager.Instance.CurrentState == CombatState.Idle)
        {
            // If we just finished a combo recovery, start the next combo
            if (waitingForComboRecovery)
            {
                waitingForComboRecovery = false;
                idleTimer = 0f;
                // Brief pause before next combo
                return;
            }

            idleTimer += Time.deltaTime;

            // Are we mid-combo or between combos?
            if (currentCombo != null && comboAttackIndex < currentCombo.attacks.Length)
            {
                // Mid-combo: no gap — fire immediately for tight rhythm game feel
                idleTimer = 0f;
                FireNextComboAttack();
            }
            else
            {
                // Between combos: longer delay
                float delay = isEnraged
                    ? delayBetweenCombos * 0.5f
                    : delayBetweenCombos;

                if (idleTimer >= delay)
                {
                    idleTimer = 0f;
                    StartNextCombo();
                }
            }
        }
    }

    void StartNextCombo()
    {
        // Pick a random combo, avoiding immediate repeat
        int pick;
        if (combos.Length == 1)
        {
            pick = 0;
        }
        else
        {
            do { pick = Random.Range(0, combos.Length); }
            while (pick == lastComboIndex);
        }

        lastComboIndex   = pick;
        currentCombo     = combos[pick];
        comboAttackIndex = 0;

        Debug.Log($"[Boss] Starting combo: {currentCombo.comboName}");
        FireNextComboAttack();
    }

    void FireNextComboAttack()
    {
        if (currentCombo == null || comboAttackIndex >= currentCombo.attacks.Length)
            return;

        AttackData src  = currentCombo.attacks[comboAttackIndex];
        bool isLastHit  = (comboAttackIndex == currentCombo.attacks.Length - 1);
        comboAttackIndex++;

        // Suppress perfect window for ALL combo attacks (including the last).
        // The multi-counter window after the combo replaces the single-attack
        // perfect→counter flow entirely.
        CombatManager.Instance.SuppressPerfectWindow = true;

        // Always use a scratch copy so we can tweak recovery/rage without
        // mutating the original ScriptableObject.
        if (activeRageInstance == null)
            activeRageInstance = ScriptableObject.CreateInstance<AttackData>();

        AttackData copy              = activeRageInstance;
        float speedMult              = isEnraged ? rageSpeedMult : 1f;
        float dmgMult                = isEnraged ? rageDamageMult : 1f;

        copy.name                    = src.name + (isEnraged ? "_RAGE" : "");
        copy.attackType              = src.attackType;
        copy.telegraphDuration       = src.telegraphDuration  * speedMult;
        copy.activeDuration          = src.activeDuration     * speedMult;
        copy.perfectWindowRadius     = src.perfectWindowRadius;
        copy.requiredDodge           = src.requiredDodge;
        copy.feintSwitchPoint        = src.feintSwitchPoint;
        copy.doubleStrikeDelay       = src.doubleStrikeDelay;
        copy.surgeWindowStart        = src.surgeWindowStart;
        copy.surgeWindowEnd          = src.surgeWindowEnd;
        copy.surgeSpeedMultiplier    = src.surgeSpeedMultiplier;
        copy.damageOnHit             = src.damageOnHit * dmgMult;

        // Mid-combo attacks get a very short recovery so the next attack
        // fires almost immediately — keeps the rhythm tight.
        copy.recoveryDuration = isLastHit
            ? src.recoveryDuration * speedMult
            : midComboRecovery;

        CombatManager.Instance.BeginAttack(copy);
    }

    void HandleStateChanged(CombatState s)
    {
        switch (s)
        {
            case CombatState.Windup:   OnAttackWindup?.Invoke();   break;
            case CombatState.Active:   OnAttackActive?.Invoke();   break;
            case CombatState.Recovery: OnAttackRecovery?.Invoke(); break;
        }

        // When the last attack in a combo finishes its recovery, trigger combo recovery
        if (s == CombatState.Idle && currentCombo != null &&
            comboAttackIndex >= currentCombo.attacks.Length &&
            !waitingForComboRecovery)
        {
            int counterCount = currentCombo.RollCounterHits();
            waitingForComboRecovery = true;
            CombatManager.Instance.SuppressPerfectWindow = false;
            CombatManager.Instance.BeginComboRecovery(counterCount);
        }
    }

    void TakeCounterDamage()
    {
        if (!combatRunning) return;

        currentHealth -= counterDamage;
        currentHealth  = Mathf.Max(currentHealth, 0f);
        OnBossHealthChanged?.Invoke(currentHealth / maxHealth);

        if (!isEnraged && currentHealth / maxHealth <= rageThreshold)
        {
            isEnraged = true;
            OnRageEntered?.Invoke();
            Debug.Log("[Boss] ENRAGED");
        }

        if (currentHealth <= 0f)
            CombatManager.Instance.NotifyBossDefeated();
    }

    void HandleDefeated()
    {
        combatRunning = false;
        OnBossDefeated?.Invoke();
    }

    void HandlePlayerDied() => combatRunning = false;
}
