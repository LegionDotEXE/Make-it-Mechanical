using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PlayerController : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth     = 100f;
    public float currentHealth { get; private set; }

    [Header("Hit Stagger")]
    [Tooltip("Short post-hit mercy. Kept under the double-strike gap so combos can connect.")]
    public float invincibilityDuration = 0.2f;
    private bool isInvincible = false;
    private bool isDead       = false;

    [Header("Events")]
    public UnityEvent<float> OnHealthChanged      = new UnityEvent<float>();
    public UnityEvent        OnDeath              = new UnityEvent();
    public UnityEvent        OnHitWhileInvincible = new UnityEvent();

    private System.Action dodgeLeftHandler;
    private System.Action dodgeRightHandler;

    void Start()
    {
        currentHealth = maxHealth;

        dodgeLeftHandler  = () => Dodge(DodgeDirection.Left);
        dodgeRightHandler = () => Dodge(DodgeDirection.Right);

        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnDodgeLeft  += dodgeLeftHandler;
            InputManager.Instance.OnDodgeRight += dodgeRightHandler;
            InputManager.Instance.OnCounter    += Counter;
        }
        else Debug.LogError("[PlayerController] No InputManager in scene.");

        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnPlayerHit   += TakeHit;
            CombatManager.Instance.OnPlayerDeath += HandleDeath;
        }
        else Debug.LogError("[PlayerController] No CombatManager in scene.");
    }

    void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnDodgeLeft  -= dodgeLeftHandler;
            InputManager.Instance.OnDodgeRight -= dodgeRightHandler;
            InputManager.Instance.OnCounter    -= Counter;
        }
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.OnPlayerHit   -= TakeHit;
            CombatManager.Instance.OnPlayerDeath -= HandleDeath;
        }
    }

    // mistimed presses get punished.
    void Dodge(DodgeDirection dir)
    {
        if (isDead) return;
        CombatManager.Instance.TryStartEvade(dir);
    }

    void Counter()
    {
        if (isDead) return;
        CombatManager.Instance.TryCounterInput();
    }

    void TakeHit()
    {
        if (isDead || isInvincible)
        {
            OnHitWhileInvincible?.Invoke();
            return;
        }

        AttackData atk = CombatManager.Instance.CurrentAttack;
        float damage = atk == null ? 20f
            : (atk.attackType == AttackType.Unblockable ? atk.unblockableDamage : atk.damageOnHit);

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