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

    [Header("Attacks")]
    public AttackData[] attacks;

    [Header("Loop Settings")]
    public float delayBetweenAttacks = 0.8f;

    [Header("Events")]
    public UnityEvent<float> OnBossHealthChanged = new UnityEvent<float>();
    public UnityEvent        OnBossDefeated      = new UnityEvent();
    public UnityEvent        OnAttackWindup      = new UnityEvent();
    public UnityEvent        OnAttackActive      = new UnityEvent();
    public UnityEvent        OnAttackRecovery    = new UnityEvent();
    public UnityEvent        OnRageEntered       = new UnityEvent();

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
            float delay = isEnraged
                ? delayBetweenAttacks * 0.5f
                : delayBetweenAttacks;
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

        AttackData next = src;
        if (isEnraged)
        {
            AttackData rage = ScriptableObject.CreateInstance<AttackData>();
            rage.name                = src.name + "_RAGE";
            rage.attackType          = src.attackType;
            rage.telegraphDuration   = src.telegraphDuration  * rageSpeedMult;
            rage.activeDuration      = src.activeDuration     * rageSpeedMult;
            rage.recoveryDuration    = src.recoveryDuration   * rageSpeedMult;
            rage.perfectWindowRadius = src.perfectWindowRadius;
            rage.requiredDodge       = src.requiredDodge;
            rage.feintSwitchPoint    = src.feintSwitchPoint;
            rage.damageOnHit         = src.damageOnHit * rageDamageMult;
            next = rage;
        }

        CombatManager.Instance.BeginAttack(next);
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
