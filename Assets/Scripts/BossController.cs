using UnityEngine;
using UnityEngine.Events;

public class BossController : MonoBehaviour
{
    [Header("Boss Health")]
    public float maxHealth = 200f;
    public float currentHealth { get; private set; }
    public float counterDamage = 25f;  // damage per successful player counter

    // Parker will set up attacks in the Inspector 
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
            Debug.LogError("[BossController] No attacks assigned! Add an AttackData in the Inspector.");
            return;
        }

        CombatManager.Instance.OnCounterLanded += TakeCounterDamage;
        CombatManager.Instance.OnStateChanged  += HandleStateChanged;
        CombatManager.Instance.OnBossDefeated  += HandleDefeated;

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
        }
    }

    void Update()
    {
        if (!combatRunning) return;

        // tick the state machine every frame
        CombatManager.Instance.Tick();

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
        currentAttackIndex = currentAttackIndex % attacks.Length;
        AttackData next = attacks[currentAttackIndex];
        currentAttackIndex++;

        Debug.Log($"[Boss] starting attack: {next.name}  required dodge: {next.requiredDodge}");
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
        currentHealth -= counterDamage;
        currentHealth  = Mathf.Max(currentHealth, 0f);

        OnBossHealthChanged?.Invoke(currentHealth / maxHealth);

        Debug.Log($"[Boss] hit by counter - health: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f)
            CombatManager.Instance.NotifyBossDefeated();
    }

    void HandleDefeated()
    {
        combatRunning = false;
        OnBossDefeated?.Invoke();
        Debug.Log("[Boss] defeated!");
        // could add victory dance or something here, but for now we'll just stop the fight
    }
}
