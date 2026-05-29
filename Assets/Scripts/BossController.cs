using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class BossController : MonoBehaviour
{
    [Header("Boss Health")]
    public float maxHealth    = 200f;
    public float counterDamage = 25f;
    public float currentHealth { get; private set; }

    [Header("Rage Phase")]
    [Tooltip("Below this HP fraction the boss enters rage — faster attacks, more damage.")]
    public float rageThreshold    = 0.4f;   // 40% HP
    public float rageSpeedMult    = 0.65f;  // telegraph/active durations multiplied by this
    public float rageDamageMult   = 1.5f;   // hit damage multiplied by this
    private bool isEnraged        = false;
    public bool IsEnraged         => isEnraged;

    [Header("Attacks")]
    public AttackData[] attacks;

    [Header("Loop Settings")]
    public float delayBetweenAttacks = 0.8f;

    [Header("Events")]
    public UnityEvent<float> OnBossHealthChanged;
    public UnityEvent OnBossDefeated;
    public UnityEvent OnAttackWindup;
    public UnityEvent OnAttackActive;
    public UnityEvent OnAttackRecovery;
    public UnityEvent OnRageEntered;           // UIManager / BossVisuals hooks this

    private int   currentAttackIndex = 0;
    private bool  combatRunning      = false;
    private float idleTimer          = 0f;

    void Start()
    {
        currentHealth = maxHealth;

        if (attacks == null || attacks.Length == 0)
        {
            Debug.LogError("[BossController] No attacks assigned!");
            return;
        }

        CombatManager.Instance.OnCounterLanded += TakeCounterDamage;
        CombatManager.Instance.OnStateChanged  += HandleStateChanged;
        CombatManager.Instance.OnBossDefeated  += HandleDefeated;
        CombatManager.Instance.OnPlayerDeath   += HandlePlayerDied;

        combatRunning = true;
        StartNextAttack();
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
    }

    void Update()
    {
        if (!combatRunning) return;

        CombatManager.Instance.Tick();

        if (CombatManager.Instance.CurrentState == CombatState.Idle)
        {
            idleTimer += Time.deltaTime;
            // rage phase shortens delay between attacks too
            float delay = isEnraged ? delayBetweenAttacks * 0.5f : delayBetweenAttacks;
            if (idleTimer >= delay)
            {
                idleTimer = 0f;
                StartNextAttack();
            }
        }
    }

    void StartNextAttack()
    {
        currentAttackIndex = currentAttackIndex % attacks.Length;
        AttackData src = attacks[currentAttackIndex];
        currentAttackIndex++;

        // in rage phase, apply speed and damage multipliers at runtime
        // we clone the values into a temp ScriptableObject so we don't dirty the asset
        AttackData next = src;
        if (isEnraged)
        {
            AttackData enraged = ScriptableObject.CreateInstance<AttackData>();
            enraged.name               = src.name + "_ENRAGED";
            enraged.telegraphDuration  = src.telegraphDuration  * rageSpeedMult;
            enraged.activeDuration     = src.activeDuration     * rageSpeedMult;
            enraged.recoveryDuration   = src.recoveryDuration   * rageSpeedMult;
            enraged.perfectWindowRadius = src.perfectWindowRadius; // window stays fair
            enraged.requiredDodge      = src.requiredDodge;
            enraged.damageOnHit        = src.damageOnHit * rageDamageMult;
            next = enraged;
        }

        Debug.Log($"[Boss] attack: {next.name} | dodge: {next.requiredDodge} | enraged: {isEnraged}");
        CombatManager.Instance.BeginAttack(next);
    }

    void HandleStateChanged(CombatState newState)
    {
        switch (newState)
        {
            case CombatState.Windup:   OnAttackWindup?.Invoke();   break;
            case CombatState.Active:   OnAttackActive?.Invoke();   break;
            case CombatState.Recovery: OnAttackRecovery?.Invoke(); break;
        }
    }

    void TakeCounterDamage()
    {
        if (!combatRunning) return;

        currentHealth -= counterDamage;
        currentHealth  = Mathf.Max(currentHealth, 0f);

        OnBossHealthChanged?.Invoke(currentHealth / maxHealth);
        Debug.Log($"[Boss] countered — HP: {currentHealth}/{maxHealth}");

        // check rage threshold
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
        Debug.Log("[Boss] defeated!");
    }

    void HandlePlayerDied()
    {
        combatRunning = false;
    }
}
