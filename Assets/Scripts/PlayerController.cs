using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PlayerController : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth     = 100f;
    public float currentHealth { get; private set; }

    [Header("Hit Stagger")]
    public float invincibilityDuration = 0.6f;
    private bool isInvincible = false;
    private bool isDead       = false;

    [Header("Input Buffer")]
    [Tooltip("How long an early dodge press stays queued so it isn't dropped, in seconds.")]
    public float inputBufferTime = 0.15f;

    [Header("Events")]
    public UnityEvent<float> OnHealthChanged      = new UnityEvent<float>();
    public UnityEvent        OnDeath              = new UnityEvent();
    public UnityEvent        OnDodge              = new UnityEvent();
    public UnityEvent        OnPerfectDodge       = new UnityEvent();
    public UnityEvent        OnHitWhileInvincible = new UnityEvent();

    private Action dodgeLeftHandler;
    private Action dodgeRightHandler;

    // Buffered (too-early) dodge press.
    private bool           hasBufferedDodge;
    private DodgeDirection bufferedDir;
    private float          bufferedTime;

    void Start()
    {
        currentHealth = maxHealth;

        dodgeLeftHandler  = () => OnDodgePressed(DodgeDirection.Left);
        dodgeRightHandler = () => OnDodgePressed(DodgeDirection.Right);

        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnDodgeLeft  += dodgeLeftHandler;
            InputManager.Instance.OnDodgeRight += dodgeRightHandler;
            InputManager.Instance.OnCounter    += TryCounter;
        }
        else Debug.LogError("[PlayerController] No InputManager in scene.");

        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnPlayerHit    += TakeHit;
            CombatManager.Instance.OnPlayerDeath  += HandleDeath;
            CombatManager.Instance.OnStateChanged += HandleStateChanged;
        }
        else Debug.LogError("[PlayerController] No CombatManager in scene.");
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
            CombatManager.Instance.OnPlayerHit    -= TakeHit;
            CombatManager.Instance.OnPlayerDeath  -= HandleDeath;
            CombatManager.Instance.OnStateChanged -= HandleStateChanged;
        }
    }

    void OnDodgePressed(DodgeDirection dir)
    {
        if (isDead) return;

        CombatState state = CombatManager.Instance.CurrentState;
        bool windowOpen = state == CombatState.Windup || state == CombatState.Active;

        if (windowOpen)
        {
            // Live press: resolve immediately (TryDodge handles right vs wrong direction).
            ExecuteDodge(dir);
        }
        else
        {
            // Pressed before the window opened - queue it instead of eating it.
            hasBufferedDodge = true;
            bufferedDir      = dir;
            bufferedTime     = Time.time;
        }
    }

    void HandleStateChanged(CombatState state)
    {
        // A dodge window just opened - consume a fresh buffered press if we have one.
        if (state != CombatState.Windup || !hasBufferedDodge) return;

        if (Time.time - bufferedTime <= inputBufferTime)
            ExecuteDodge(bufferedDir);

        hasBufferedDodge = false;
    }

    void ExecuteDodge(DodgeDirection dir)
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