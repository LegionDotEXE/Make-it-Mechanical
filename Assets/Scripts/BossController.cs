using UnityEngine;
using UnityEngine.Events;

public class BossController : MonoBehaviour
{
    [Header("Boss Health")]
    public float maxHealth = 200f;
    public float currentHealth { get; private set; }
    public float counterDamage = 25f;

    [Header("Attacks")]
    public AttackData[] attacks;

    [Header("Loop Settings")]
    [Tooltip("Seconds between the end of one attack cycle and the start of the next.")]
    public float delayBetweenAttacks = 0.8f;

    [Header("Events - hook UI and animations here")]
    public UnityEvent<float> OnBossHealthChanged;
    public UnityEvent OnBossDefeated;
    public UnityEvent OnAttackWindup;
    public UnityEvent OnAttackActive;
    public UnityEvent OnAttackRecovery;

    private int currentAttackIndex = 0;
    private bool combatRunning = false;
    private float idleTimer = 0f;

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

        // stop the loop when player dies too — no point ticking after game over
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
        // guard first — if combat stopped don't tick anything
        if (!combatRunning) return;

        CombatManager.Instance.Tick();

        // when idle, wait the delay then fire the next attack
        if (CombatManager.Instance.CurrentState == CombatState.Idle)
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= delayBetweenAttacks)
            {
                idleTimer = 0f;
                StartNextAttack();
            }
        }
    }

    void StartNextAttack()
    {
        // loop the attack list — works with 1 attack or 10
        currentAttackIndex = currentAttackIndex % attacks.Length;
        AttackData next = attacks[currentAttackIndex];
        currentAttackIndex++;

        Debug.Log($"[Boss] attack: {next.name} | dodge: {next.requiredDodge}");
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
        // don't process damage after combat ends
        if (!combatRunning) return;

        currentHealth -= counterDamage;
        currentHealth  = Mathf.Max(currentHealth, 0f);

        OnBossHealthChanged?.Invoke(currentHealth / maxHealth);
        Debug.Log($"[Boss] countered — health: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f)
            CombatManager.Instance.NotifyBossDefeated();
    }

    void HandleDefeated()
    {
        combatRunning = false;  // stops Update() from ticking
        OnBossDefeated?.Invoke();
        Debug.Log("[Boss] defeated!");
    }

    void HandlePlayerDied()
    {
        combatRunning = false;  // stops Update() from ticking after game over
        Debug.Log("[Boss] player died — stopping combat loop");
    }
}
