using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PlayerController : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth           = 100f;
    public float currentHealth       { get; private set; }

    [Header("Hit Stagger")]
    public float invincibilityDuration = 0.6f;
    private bool isInvincible = false;
    private bool isDead       = false;

    [Header("Events")]
    public UnityEvent<float> OnHealthChanged     = new UnityEvent<float>();
    public UnityEvent        OnDeath             = new UnityEvent();
    public UnityEvent        OnDodge             = new UnityEvent();
    public UnityEvent        OnPerfectDodge      = new UnityEvent();
    public UnityEvent        OnHitWhileInvincible = new UnityEvent();

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
        if (isDead || isInvincible)
        {
            OnHitWhileInvincible?.Invoke();
            return;
        }

        float damage = CombatManager.Instance.CurrentAttack?.damageOnHit ?? 20f;
        currentHealth -= damage;
        currentHealth  = Mathf.Max(currentHealth, 0f);

        OnHealthChanged?.Invoke(currentHealth / maxHealth);

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
    }
}
