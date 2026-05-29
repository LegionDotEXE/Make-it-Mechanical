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
    public event Action OnFeintSwitch;   // boss flips the telegraphed direction mid-windup

    // Direction the boss is *currently telegraphing*. For a feint this starts as the
    // fake (opposite of requiredDodge) and flips to requiredDodge at feintSwitchPoint.
    // BossVisuals should read this (and/or OnFeintSwitch) to draw the windup.
    public DodgeDirection CurrentTelegraphDirection { get; private set; }

    // Windup length of the strike currently being telegraphed: the full telegraph
    // normally, or doubleStrikeDelay for the second hit of a double strike.
    public float CurrentWindupDuration => currentTelegraphDuration;

    [HideInInspector] public float attackImpactTime;
    private float stateEnterTime;

    // Windup length of the *current* strike: the full telegraph normally, or the short
    // gap before the second hit of a double strike.
    private float currentTelegraphDuration;
    private int   strikesRemaining;
    private bool  feintSwitched;

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
        strikesRemaining = (attack.attackType == AttackType.Double) ? 2 : 1;
        feintSwitched    = false;
        StartStrike(attack.telegraphDuration);
    }

    // Sets up one windup -> active cycle. Used for the opening strike and, for double
    // strikes, for the follow-up hit (with a shorter windup = doubleStrikeDelay).
    void StartStrike(float windupDuration)
    {
        currentTelegraphDuration = windupDuration;

        CurrentTelegraphDirection =
            (CurrentAttack.attackType == AttackType.Feint && !feintSwitched)
                ? Opposite(CurrentAttack.requiredDodge)
                : CurrentAttack.requiredDodge;

        TransitionTo(CombatState.Windup);

        // Impact = the moment the windup ends. Tied to the same Time.time clock the
        // state machine runs on, so pause / slow-mo can't desync the perfect window.
        attackImpactTime = stateEnterTime + windupDuration;
    }

    public void Tick()
    {
        float elapsed = Time.time - stateEnterTime;

        switch (CurrentState)
        {
            case CombatState.Windup:
                // Feint: flip the telegraphed direction partway through the windup.
                if (CurrentAttack.attackType == AttackType.Feint && !feintSwitched &&
                    elapsed >= currentTelegraphDuration * CurrentAttack.feintSwitchPoint)
                {
                    feintSwitched = true;
                    CurrentTelegraphDirection = CurrentAttack.requiredDodge;
                    OnFeintSwitch?.Invoke();
                }

                if (elapsed >= currentTelegraphDuration)
                    TransitionTo(CombatState.Active);
                break;

            case CombatState.Active:
                if (elapsed >= CurrentAttack.activeDuration)
                {
                    // Active window ended without a dodge -> the hit lands.
                    OnPlayerHit?.Invoke();
                    ResolveStrike();
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

        float timeToImpact = Mathf.Abs(attackImpactTime - Time.time);
        bool isPerfect     = timeToImpact <= CurrentAttack.perfectWindowRadius;

        if (isPerfect) OnPlayerPerfectDodge?.Invoke();
        else           OnPlayerDodgedSuccessfully?.Invoke();

        if (HasPendingStrike())
        {
            // Double strike: surviving this hit does NOT end the attack or open a
            // counter window - the second hit is still coming.
            BeginNextStrike();
        }
        else if (isPerfect)
        {
            TransitionTo(CombatState.PerfectWindow);
        }
        else
        {
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

    // Called when a strike finishes (whether dodged or landed). Either chains the next
    // hit of a double strike or ends the attack.
    void ResolveStrike()
    {
        if (HasPendingStrike())
            BeginNextStrike();
        else
            TransitionTo(CombatState.Recovery);
    }

    bool HasPendingStrike()
        => CurrentAttack.attackType == AttackType.Double && strikesRemaining > 1;

    void BeginNextStrike()
    {
        strikesRemaining--;
        StartStrike(CurrentAttack.doubleStrikeDelay);
    }

    static DodgeDirection Opposite(DodgeDirection d)
        => d == DodgeDirection.Left ? DodgeDirection.Right : DodgeDirection.Left;

    public void NotifyPlayerDeath()  => OnPlayerDeath?.Invoke();
    public void NotifyBossDefeated() => OnBossDefeated?.Invoke();

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
        GUI.Label(new Rect(10, 10, 380, 20), $"State: {CurrentState}");
        if (CurrentAttack != null)
        {
            float toImpact = attackImpactTime - Time.time;
            GUI.Label(new Rect(10, 30, 380, 20), $"Time to impact: {toImpact:F3}s");
            GUI.Label(new Rect(10, 50, 380, 20), $"Perfect window: +/-{CurrentAttack.perfectWindowRadius:F3}s");
            GUI.Label(new Rect(10, 70, 380, 20), $"Required dodge: {CurrentAttack.requiredDodge}  (telegraph: {CurrentTelegraphDirection})");
            GUI.Label(new Rect(10, 90, 380, 20), $"Type: {CurrentAttack.attackType}  strikes left: {strikesRemaining}");
        }
    }
}