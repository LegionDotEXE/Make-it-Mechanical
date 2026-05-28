using System;
using UnityEngine;
using UnityEngine.Events;
public class PlayerController : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth { get; private set; }

    [Header("Events - hook UI here (Tommy)")]
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnDeath;
    public UnityEvent OnDodge;
    public UnityEvent OnPerfectDodge;

    // cached delegates so we can properly unsubscribe
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

        CombatManager.Instance.OnPlayerHit     += TakeHit;
        CombatManager.Instance.OnPlayerDeath   += HandleDeath;
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
        bool dodged = CombatManager.Instance.TryDodge(dir);

        if (!dodged)
        {
            // wrong timing or wrong direction - could show "MISS" feedback here
            Debug.Log("[Player] dodge failed - wrong direction or bad timing");
            return;
        }

        // fire events for animation / UI
        if (CombatManager.Instance.CurrentState == CombatState.PerfectWindow)
            OnPerfectDodge?.Invoke();
        else
            OnDodge?.Invoke();
    }

    void TryCounter()
    {
        CombatManager.Instance.TryCounter();
        // animation trigger would go here
    }

    void TakeHit()
    {
        if (CombatManager.Instance.CurrentAttack == null) return;

        currentHealth -= CombatManager.Instance.CurrentAttack.damageOnHit;
        currentHealth  = Mathf.Max(currentHealth, 0f);

        OnHealthChanged?.Invoke(currentHealth / maxHealth);

        Debug.Log($"[Player] took hit - health: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f)
            CombatManager.Instance.NotifyPlayerDeath();
    }

    void HandleDeath()
    {
        OnDeath?.Invoke();
        Debug.Log("[Player] died");
        // trigger game over screen here
    }
}
