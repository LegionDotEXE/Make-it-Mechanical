using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BossController : MonoBehaviour
{
    [Header("Boss Health")]
    public float maxHealth     = 550f;
    public float counterDamage = 25f;
    public float currentHealth { get; private set; }

    [Header("Perfect-Dodge Reward")]
    [Tooltip("Chip damage dealt to the boss on a perfect dodge, even without a counter.")]
    public float perfectChipDamage = 6f;

    [Header("Rage Phase (mid fight)")]
    public float rageThreshold  = 0.4f;
    public float rageSpeedMult  = 0.65f;
    public float rageDamageMult = 1.5f;
    public bool  IsEnraged      => isEnraged;
    private bool isEnraged      = false;

    [Header("Final Phase (low HP)")]
    public float finalThreshold        = 0.15f;
    [Tooltip("Multiplier applied to every perfect window in the final phase (smaller = harder).")]
    public float finalPerfectScale     = 0.7f;
    public bool  IsFinalPhase          => isFinalPhase;
    private bool isFinalPhase          = false;

    [Header("Attacks")]
    public AttackData[] attacks;

    [Header("Loop / Pacing")]
    public float delayBetweenAttacks = 0.5f;   
    public float enragedDelayMult    = 0.5f;
    public float finalDelayMult      = 0.35f;

    [Header("Events")]
    public UnityEvent<float> OnBossHealthChanged = new UnityEvent<float>();
    public UnityEvent        OnBossDefeated      = new UnityEvent();
    public UnityEvent        OnAttackWindup      = new UnityEvent();
    public UnityEvent        OnAttackActive      = new UnityEvent();
    public UnityEvent        OnAttackRecovery    = new UnityEvent();
    public UnityEvent        OnRageEntered       = new UnityEvent();
    public UnityEvent        OnFinalPhaseEntered = new UnityEvent();

    private int   lastAttackIndex = -1;
    private bool  combatRunning   = false;
    private float idleTimer       = 0f;

    private AttackData activeRageInstance;

    void Start()
    {
        currentHealth = maxHealth;

        if (attacks == null || attacks.Length == 0)
        {
            Debug.LogError("[BossController] No attacks assigned!");
            return;
        }

        CombatManager.Instance.OnCounterLanded     += TakeCounterDamage;
        CombatManager.Instance.OnPlayerPerfectDodge += TakePerfectChip;
        CombatManager.Instance.OnStateChanged      += HandleStateChanged;
        CombatManager.Instance.OnBossDefeated      += HandleDefeated;
        CombatManager.Instance.OnPlayerDeath       += HandlePlayerDied;

        combatRunning = true;
        StartNextAttack();
    }

    void OnDestroy()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnCounterLanded      -= TakeCounterDamage;
            CombatManager.Instance.OnPlayerPerfectDodge -= TakePerfectChip;
            CombatManager.Instance.OnStateChanged       -= HandleStateChanged;
            CombatManager.Instance.OnBossDefeated       -= HandleDefeated;
            CombatManager.Instance.OnPlayerDeath        -= HandlePlayerDied;
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
            idleTimer += Time.deltaTime;
            float delay = delayBetweenAttacks
                        * (isFinalPhase ? finalDelayMult : isEnraged ? enragedDelayMult : 1f);
            if (idleTimer >= delay)
            {
                idleTimer = 0f;
                StartNextAttack();
            }
        }
    }

    void StartNextAttack()
    {
        int idx = PickNextIndex();
        AttackData src = attacks[idx];

        AttackData next = src;
        if (isEnraged)
        {
            if (activeRageInstance == null)
                activeRageInstance = ScriptableObject.CreateInstance<AttackData>();

            AttackData rage = activeRageInstance;
            rage.name                = src.name + "_RAGE";
            rage.attackType          = src.attackType;
            rage.telegraphDuration   = src.telegraphDuration  * rageSpeedMult;
            rage.activeDuration      = src.activeDuration     * rageSpeedMult;
            rage.recoveryDuration    = src.recoveryDuration   * rageSpeedMult;
            rage.perfectWindowRadius = src.perfectWindowRadius;
            rage.requiredDodge       = src.requiredDodge;
            rage.feintSwitchPoint    = src.feintSwitchPoint;
            rage.doubleStrikeDelay   = src.doubleStrikeDelay;
            rage.damageOnHit         = src.damageOnHit       * rageDamageMult;
            rage.unblockableDamage   = src.unblockableDamage * rageDamageMult;
            next = rage;
        }

        CombatManager.Instance.BeginAttack(next);
    }

    // Random selection with no immediate repeat. Once enraged, harder attack types are
    // weighted more heavily so the fight stops being a memorizable sequence.
    int PickNextIndex()
    {
        if (attacks.Length == 1) { lastAttackIndex = 0; return 0; }

        List<int> pool = new List<int>();
        for (int i = 0; i < attacks.Length; i++)
        {
            if (i == lastAttackIndex) continue;
            int weight = 1;
            if (isEnraged)
            {
                switch (attacks[i].attackType)
                {
                    case AttackType.Heavy:
                    case AttackType.Feint:
                    case AttackType.Double:
                    case AttackType.Unblockable:
                        weight = isFinalPhase ? 4 : 3;
                        break;
                }
            }
            for (int w = 0; w < weight; w++) pool.Add(i);
        }

        int pick = pool[Random.Range(0, pool.Count)];
        lastAttackIndex = pick;
        return pick;
    }

    void HandleStateChanged(CombatState s)
    {
        switch (s)
        {
            case CombatState.Windup:   OnAttackWindup?.Invoke();   break;
            case CombatState.Active:   OnAttackActive?.Invoke();   break;
            case CombatState.Recovery: OnAttackRecovery?.Invoke(); break;
        }
    }

    void TakeCounterDamage() => ApplyBossDamage(counterDamage);
    void TakePerfectChip()   => ApplyBossDamage(perfectChipDamage);

    void ApplyBossDamage(float amount)
    {
        if (!combatRunning) return;

        currentHealth -= amount;
        currentHealth  = Mathf.Max(currentHealth, 0f);
        OnBossHealthChanged?.Invoke(currentHealth / maxHealth);

        float frac = currentHealth / maxHealth;

        if (!isEnraged && frac <= rageThreshold)
        {
            isEnraged = true;
            OnRageEntered?.Invoke();
            Debug.Log("[Boss] ENRAGED");
        }

        if (!isFinalPhase && frac <= finalThreshold)
        {
            isFinalPhase = true;
            CombatManager.Instance.perfectWindowScale = finalPerfectScale;
            CombatManager.Instance.hideAttackTells    = true;
            OnFinalPhaseEntered?.Invoke();
            Debug.Log("[Boss] FINAL PHASE");
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