using System;
using UnityEngine;

public enum CombatState
{
    Idle,
    Windup,
    Active,
    PerfectWindow,
    Counter,
    Recovery
}

public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    public CombatState CurrentState { get; private set; } = CombatState.Idle;

    [HideInInspector] public AttackData CurrentAttack;

    public event Action<CombatState> OnStateChanged;
    public event Action OnPlayerDodgedSuccessfully;
    public event Action OnPlayerPerfectDodge;
    public event Action OnPlayerHit;
    public event Action OnCounterLanded;
    public event Action OnPlayerDeath;
    public event Action OnBossDefeated;

    [HideInInspector] public float attackImpactTime;
    private float stateEnterTime;

    [Header("Debug")]
    public bool showDebugOverlay = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void BeginAttack(AttackData attack)
    {
        CurrentAttack    = attack;
        attackImpactTime = (float)AudioSettings.dspTime + attack.telegraphDuration;
        TransitionTo(CombatState.Windup);
    }

    public void Tick()
    {
        float elapsed = Time.time - stateEnterTime;

        switch (CurrentState)
        {
            case CombatState.Windup:
                if (elapsed >= CurrentAttack.telegraphDuration)
                    TransitionTo(CombatState.Active);
                break;

            case CombatState.Active:
                if (elapsed >= CurrentAttack.activeDuration)
                {
                    OnPlayerHit?.Invoke();
                    TransitionTo(CombatState.Recovery);
                }
                break;

            case CombatState.PerfectWindow:
                if (elapsed >= CurrentAttack.activeDuration)
                    TransitionTo(CombatState.Recovery);
                break;

            case CombatState.Counter:
                if (elapsed >= 0.3f)
                    TransitionTo(CombatState.Recovery);
                break;

            case CombatState.Recovery:
                if (elapsed >= CurrentAttack.recoveryDuration)
                    TransitionTo(CombatState.Idle);
                break;
        }
    }

    public bool TryDodge(DodgeDirection dir)
    {
        if (CurrentState != CombatState.Windup && CurrentState != CombatState.Active)
            return false;

        if (dir != CurrentAttack.requiredDodge)
            return false;

        float timeToImpact = Mathf.Abs(attackImpactTime - (float)AudioSettings.dspTime);
        bool isPerfect     = timeToImpact <= CurrentAttack.perfectWindowRadius;

        if (isPerfect)
        {
            OnPlayerPerfectDodge?.Invoke();
            TransitionTo(CombatState.PerfectWindow);
        }
        else
        {
            OnPlayerDodgedSuccessfully?.Invoke();
            TransitionTo(CombatState.Recovery);
        }

        return true;
    }

    public void TryCounter()
    {
        if (CurrentState != CombatState.PerfectWindow) return;
        OnCounterLanded?.Invoke();
        TransitionTo(CombatState.Counter);
    }

    public void NotifyPlayerDeath()   => OnPlayerDeath?.Invoke();
    public void NotifyBossDefeated()  => OnBossDefeated?.Invoke();

    void TransitionTo(CombatState next)
    {
        CurrentState   = next;
        stateEnterTime = Time.time;
        OnStateChanged?.Invoke(next);

        if (showDebugOverlay)
            Debug.Log($"[CombatManager] -> {next}  t={Time.time:F2}");
    }

    void OnGUI()
    {
        if (!showDebugOverlay) return;
        GUI.color = Color.yellow;
        GUI.Label(new Rect(10, 10, 300, 20), $"State: {CurrentState}");
        if (CurrentAttack != null)
        {
            float toImpact = attackImpactTime - (float)AudioSettings.dspTime;
            GUI.Label(new Rect(10, 30, 300, 20), $"Time to impact: {toImpact:F3}s");
            GUI.Label(new Rect(10, 50, 300, 20), $"Perfect window: +/-{CurrentAttack.perfectWindowRadius:F3}s");
            GUI.Label(new Rect(10, 70, 300, 20), $"Required dodge: {CurrentAttack.requiredDodge}");
        }
    }
}
