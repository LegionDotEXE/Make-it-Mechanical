using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PlayerController : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth       = 100f;
    public float currentHealth   { get; private set; }

    [Header("Hit Stagger")]
    [Tooltip("Seconds after a hit where the player cannot be hit again.")]
    public float invincibilityDuration = 0.6f;
    private bool isInvincible = false;
    private bool isDead       = false;

    [Header("Events — hook UI here (Tommy)")]
    public UnityEvent<float> OnHealthChanged;   // 0-1 normalized
    public UnityEvent<float> OnHealthChangedRaw; // actual HP value, for damage numbers
    public UnityEvent OnDeath;
    public UnityEvent OnDodge;
    public UnityEvent OnPerfectDodge;
    public UnityEvent OnHitWhileInvincible;    

    private Action dodgeLeftHandler;
    private Action dodgeRightHandler;

    void Start()
    {
        currentHealth = maxHealth;

        dodgeLeftHandler  = () => TryDodge(DodgeDirection.Left);
        dodgeRightHandler = () => TryDodge(DodgeDirection.Right);

        InputManager.Instance.OnDodgeLeft  += dodgeLeftHandler;
        InputManager.Instance.OnDodgeRight += dodgeRightHandler;
        InputManager.Instance.OnCounter    += TryCounter;

        CombatManager.Instance.OnPlayerHit   += TakeHit;
        CombatManager.Instance.OnPlayerDeath += HandleDeath;
    }

    void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnDodgeLeft  -= dodgeLeftHandler;
            InputManager.Instance.OnDodgeRight -= dodgeRightHandler;
            InputManager.Instance.OnCounter    -= TryCounter;
        }
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnPlayerHit   -= TakeHit;
            CombatManager.Instance.OnPlayerDeath -= HandleDeath;
        }
    }

    void TryDodge(DodgeDirection dir)
    {
        if (isDead) return;

        bool dodged = CombatManager.Instance.TryDodge(dir);
        if (!dodged) return;

        if (CombatManager.Instance.CurrentState == CombatState.PerfectWindow)
            OnPerfectDodge?.Invoke();
        else
            OnDodge?.Invoke();
    }

    void TryCounter()
    {
        if (isDead) return;
        CombatManager.Instance.TryCounter();
    }

    void TakeHit()
    {
        if (isDead) return;

        if (isInvincible)
        {
            OnHitWhileInvincible?.Invoke();
            Debug.Log("[Player] hit blocked by invincibility");
            return;
        }

        float damage = CombatManager.Instance.CurrentAttack?.damageOnHit ?? 20f;
        currentHealth -= damage;
        currentHealth  = Mathf.Max(currentHealth, 0f);

        OnHealthChanged?.Invoke(currentHealth / maxHealth);
        OnHealthChangedRaw?.Invoke(currentHealth);

        Debug.Log($"[Player] hit for {damage} — HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f)
        {
            CombatManager.Instance.NotifyPlayerDeath();
            return;
        }

        StartCoroutine(InvincibilityWindow());
    }

    IEnumerator InvincibilityWindow()
    {
        isInvincible = true;
        yield return new WaitForSeconds(invincibilityDuration);
        isInvincible = false;
    }

    void HandleDeath()
    {
        isDead = true;
        isInvincible = true; 
        OnDeath?.Invoke();
        Debug.Log("[Player] died");
    }

    public float HealthNormalized => currentHealth / maxHealth;
}
