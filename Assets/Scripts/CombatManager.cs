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

    // ---- events (all existing events are preserved; new ones added at the bottom) ----
    public event Action<CombatState> OnStateChanged;
    public event Action OnPlayerDodgedSuccessfully;   // a "good" (safe but not perfect) dodge, fired at impact
    public event Action OnPlayerPerfectDodge;         // perfect dodge, fired at impact
    public event Action OnPlayerHit;                  // the attack connected, fired at impact
    public event Action OnCounterLanded;              // riposte (perfect-window counter) OR successful parry
    public event Action OnPlayerDeath;
    public event Action OnBossDefeated;
    public event Action OnFeintSwitch;                // boss flips the telegraphed direction mid-windup

    public event Action<DodgeDirection> OnPlayerEvadeStarted;  // fired the instant a dodge is committed (for responsive visuals)
    public event Action OnCounterWhiffed;             // pressed counter with no valid target (mash punish)

    // direction the boss is currently telegraphing (fake-until-switch for feints)
    public DodgeDirection CurrentTelegraphDirection { get; private set; }

    public float CurrentWindupDuration => currentTelegraphDuration;

    [HideInInspector] public float attackImpactTime;
    private float stateEnterTime;

    private float currentTelegraphDuration;
    private int   strikesRemaining;
    private bool  feintSwitched;

    [Header("Dodge Feel")]
    [Tooltip("How long a dodge protects you. You must press so this window covers impact.")]
    public float dodgeIFrameDuration = 0.30f;
    [Tooltip("Lockout after any dodge/parry/counter press before you can act again.")]
    public float dodgeCooldown       = 0.50f;
    [Tooltip("How long the boss stays staggered/open after a counter or parry.")]
    public float counterStateDuration = 0.30f;

    // ---- phase modifiers (driven by BossController) ----
    [HideInInspector] public float perfectWindowScale = 1f;     // <1 shrinks the perfect window in later phases
    [HideInInspector] public bool  hideAttackTells    = false;  // late phase: suppress the attack-type cue

    // ---- player action state ----
    private bool           isEvading;
    private float          evadeStartTime;
    private DodgeDirection evadeDir;
    private bool           isParrying;
    private float          parryStartTime;
    private float          lastActionTime = -999f;

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
        ClearEvade();
        ClearParry();
        StartStrike(attack.telegraphDuration);
    }

    void StartStrike(float windupDuration)
    {
        currentTelegraphDuration = windupDuration;

        CurrentTelegraphDirection =
            (CurrentAttack.attackType == AttackType.Feint && !feintSwitched)
                ? Opposite(CurrentAttack.requiredDodge)
                : CurrentAttack.requiredDodge;

        TransitionTo(CombatState.Windup);
        attackImpactTime = stateEnterTime + windupDuration;
    }

    public void Tick()
    {
        float elapsed = Time.time - stateEnterTime;

        switch (CurrentState)
        {
            case CombatState.Windup:
                if (CurrentAttack.attackType == AttackType.Feint && !feintSwitched &&
                    elapsed >= currentTelegraphDuration * CurrentAttack.feintSwitchPoint)
                {
                    feintSwitched = true;
                    CurrentTelegraphDirection = CurrentAttack.requiredDodge;
                    OnFeintSwitch?.Invoke();
                }

                if (elapsed >= currentTelegraphDuration)
                    ResolveImpact();   // <- the hit is decided here, the moment the windup ends
                break;

            case CombatState.Active:
                if (elapsed >= CurrentAttack.activeDuration)
                {
                    if (HasPendingStrike()) BeginNextStrike();
                    else                    TransitionTo(CombatState.Recovery);
                }
                break;

            case CombatState.PerfectWindow:
                if (elapsed >= CurrentAttack.activeDuration)
                    TransitionTo(CombatState.Recovery);
                break;

            case CombatState.Counter:
                if (elapsed >= counterStateDuration)
                    TransitionTo(CombatState.Recovery);
                break;

            case CombatState.Recovery:
                if (elapsed >= CurrentAttack.recoveryDuration)
                    TransitionTo(CombatState.Idle);
                break;
        }
    }

    // Decides the outcome of a strike based on whether the player's i-frames (or parry)
    // covered the impact moment. Called once per strike, at the end of its windup.
    void ResolveImpact()
    {
        float now = Time.time;
        AttackData atk = CurrentAttack;

        if (atk.attackType == AttackType.Unblockable)
        {
            bool parried = isParrying && (now - parryStartTime) >= 0f
                                      && (now - parryStartTime) <= dodgeIFrameDuration;
            ClearEvade();
            ClearParry();

            if (parried)
            {
                OnCounterLanded?.Invoke();          // a clean parry ripostes
                TransitionTo(CombatState.Counter);
            }
            else
            {
                OnPlayerHit?.Invoke();
                TransitionTo(CombatState.Active);
            }
            return;
        }

        bool covering = isEvading && (now - evadeStartTime) >= 0f
                                  && (now - evadeStartTime) <= dodgeIFrameDuration;
        bool correct  = covering && evadeDir == atk.requiredDodge;

        ClearParry();

        if (correct)
        {
            // perfect = you committed within the perfect window before impact (a late, reactive dodge)
            bool perfect = (now - evadeStartTime) <= EffectivePerfectRadius();
            if (perfect) OnPlayerPerfectDodge?.Invoke();
            else         OnPlayerDodgedSuccessfully?.Invoke();

            ClearEvade();

            // no counter window on a non-final strike of a double; just chain via Active
            if (perfect && !HasPendingStrike())
                TransitionTo(CombatState.PerfectWindow);
            else
                TransitionTo(CombatState.Active);
            return;
        }

        // missed the window (too early), wrong direction, or no dodge at all -> hit
        ClearEvade();
        OnPlayerHit?.Invoke();
        TransitionTo(CombatState.Active);
    }

    // ---- player input entry points ----

    public bool TryStartEvade(DodgeDirection dir)
    {
        if (CurrentState != CombatState.Windup && CurrentState != CombatState.Active)
            return false;                              // can only dodge during an incoming attack
        if (!CanAct())
            return false;                              // still locked out from a previous action

        isEvading      = true;
        evadeStartTime = Time.time;
        evadeDir       = dir;
        isParrying     = false;
        lastActionTime = Time.time;
        OnPlayerEvadeStarted?.Invoke(dir);
        return true;
    }

    bool TryStartParry()
    {
        if (CurrentState != CombatState.Windup && CurrentState != CombatState.Active) return false;
        if (!CanAct()) return false;

        isParrying     = true;
        parryStartTime = Time.time;
        isEvading      = false;
        lastActionTime = Time.time;
        return true;
    }

    // Single entry point for the counter/parry key (W). Decides what it means in context.
    public void TryCounterInput()
    {
        // 1) riposte the open window after a perfect dodge
        if (CurrentState == CombatState.PerfectWindow)
        {
            OnCounterLanded?.Invoke();
            TransitionTo(CombatState.Counter);
            return;
        }

        // 2) parry an incoming unblockable
        if (CurrentAttack != null && CurrentAttack.attackType == AttackType.Unblockable &&
            (CurrentState == CombatState.Windup || CurrentState == CombatState.Active))
        {
            TryStartParry();
            return;
        }

        // 3) mashed it with no valid target -> brief lockout so offense is a real decision
        lastActionTime = Time.time;
        OnCounterWhiffed?.Invoke();
    }

    // legacy alias (only the perfect-window riposte path)
    public void TryCounter()
    {
        if (CurrentState != CombatState.PerfectWindow) return;
        OnCounterLanded?.Invoke();
        TransitionTo(CombatState.Counter);
    }

    // ---- helpers ----

    bool CanAct() => Time.time - lastActionTime >= dodgeCooldown;

    float EffectivePerfectRadius() => CurrentAttack.perfectWindowRadius * perfectWindowScale;

    bool HasPendingStrike()
        => CurrentAttack.attackType == AttackType.Double && strikesRemaining > 1;

    void BeginNextStrike()
    {
        strikesRemaining--;
        StartStrike(CurrentAttack.doubleStrikeDelay);
    }

    void ClearEvade() { isEvading  = false; }
    void ClearParry() { isParrying = false; }

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
            GUI.Label(new Rect(10, 50, 380, 20), $"i-frames: {dodgeIFrameDuration:F2}s  perfect: +/-{EffectivePerfectRadius():F3}s");
            GUI.Label(new Rect(10, 70, 380, 20), $"Required: {CurrentAttack.requiredDodge}  telegraph: {CurrentTelegraphDirection}");
            GUI.Label(new Rect(10, 90, 380, 20), $"Type: {CurrentAttack.attackType}  strikes left: {strikesRemaining}");
            GUI.Label(new Rect(10,110, 380, 20), $"Can act: {CanAct()}");
        }
    }
}