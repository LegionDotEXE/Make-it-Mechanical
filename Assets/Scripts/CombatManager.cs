using System;
using UnityEngine;

public enum CombatState
{
    Idle,
    Windup,
    Active,
    PerfectWindow,
    Counter,
    Recovery,
    ComboRecovery   // extended stagger after a full combo — multi-counter window
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
    public event Action OnFeintSwitch;
    public event Action OnSurgeTriggered;
    public event Action<int> OnComboRecoveryStarted;  // fires with the number of counter opportunities
    public event Action<int> OnComboCounterReady;      // fires with lane index (0=left, 1=center, 2=right)

    // Direction the boss is *currently telegraphing*.
    public DodgeDirection CurrentTelegraphDirection { get; private set; }

    // Windup length of the strike currently being telegraphed.
    public float CurrentWindupDuration => currentTelegraphDuration;

    // After a surge triggers, this is the multiplier tiles should apply to their fall speed.
    public float SurgeFallSpeedMultiplier => surgeFallSpeedMult;

    // When true, perfect dodges go straight to Recovery instead of PerfectWindow.
    // BossController sets this for mid-combo attacks.
    public bool SuppressPerfectWindow { get; set; }

    [HideInInspector] public float attackImpactTime;
    private float stateEnterTime;

    private float currentTelegraphDuration;
    private int   strikesRemaining;
    private bool  feintSwitched;

    // Surge state
    private bool  surgePending;
    private float surgePoint;
    private float surgeFallSpeedMult;

    // ComboRecovery state
    private int   comboCountersTotal;
    private int   comboCountersLanded;
    private int   comboCountersSpawned;
    private float comboNextCounterTime;
    private float comboCounterInterval = 0.45f;  // time between counter tile drops
    private float comboRecoveryTimeout = 3f;      // max time before combo recovery ends

    // Lane tracking for combo counters (0=Q/left, 1=W/center, 2=E/right)
    private int[] comboCounterLanes;
    private int   comboCounterHead;  // index of the next counter the player should hit

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

        if (attack.attackType == AttackType.Surge)
        {
            surgePending       = true;
            surgePoint         = UnityEngine.Random.Range(attack.surgeWindowStart, attack.surgeWindowEnd);
            surgeFallSpeedMult = 1f;
        }
        else
        {
            surgePending       = false;
            surgeFallSpeedMult = 1f;
        }

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

    /// <summary>
    /// Enter the multi-counter window after a full combo. Called by BossController.
    /// </summary>
    public void BeginComboRecovery(int counterCount)
    {
        comboCountersTotal   = counterCount;
        comboCountersLanded  = 0;
        comboCountersSpawned = 0;
        comboCounterHead     = 0;
        comboNextCounterTime = 0f; // spawn first one immediately

        // Pre-roll random lanes for each counter tile
        comboCounterLanes = new int[counterCount];
        for (int i = 0; i < counterCount; i++)
            comboCounterLanes[i] = UnityEngine.Random.Range(0, 3);

        TransitionTo(CombatState.ComboRecovery);
        OnComboRecoveryStarted?.Invoke(counterCount);
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

                if (CurrentAttack.attackType == AttackType.Surge && surgePending &&
                    elapsed >= currentTelegraphDuration * surgePoint)
                {
                    surgePending = false;
                    float remaining = currentTelegraphDuration - elapsed;
                    float compressed = remaining / CurrentAttack.surgeSpeedMultiplier;
                    attackImpactTime = Time.time + compressed;
                    surgeFallSpeedMult = CurrentAttack.surgeSpeedMultiplier;
                    OnSurgeTriggered?.Invoke();
                }

                if (Time.time >= attackImpactTime)
                    TransitionTo(CombatState.Active);
                break;

            case CombatState.Active:
                if (elapsed >= CurrentAttack.activeDuration)
                {
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

            case CombatState.ComboRecovery:
                // Spawn counter tiles at intervals
                if (comboCountersSpawned < comboCountersTotal &&
                    elapsed >= comboNextCounterTime)
                {
                    int lane = comboCounterLanes[comboCountersSpawned];
                    comboCountersSpawned++;
                    comboNextCounterTime = elapsed + comboCounterInterval;
                    OnComboCounterReady?.Invoke(lane);
                }

                // End after timeout or all counters landed/missed.
                // Wait long enough after the last spawn for the tile to reach
                // the judgment line (~0.6s fall + small buffer).
                if (elapsed >= comboRecoveryTimeout ||
                    (comboCountersSpawned >= comboCountersTotal &&
                     elapsed >= comboNextCounterTime + 0.8f))
                {
                    TransitionTo(CombatState.Idle);
                }
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
            BeginNextStrike();
        }
        else if (isPerfect && !SuppressPerfectWindow)
        {
            TransitionTo(CombatState.PerfectWindow);
        }
        else
        {
            TransitionTo(CombatState.Recovery);
        }

        return true;
    }

    /// <summary>
    /// Attempt a counter. lane = 0 (A/left), 1 (W/center), 2 (D/right).
    /// During PerfectWindow any lane works. During ComboRecovery the lane
    /// must match the next expected counter tile.
    /// </summary>
    public void TryCounter(int lane)
    {
        // Single-attack counter (legacy, still works for non-combo flow)
        if (CurrentState == CombatState.PerfectWindow)
        {
            OnCounterLanded?.Invoke();
            TransitionTo(CombatState.Counter);
            return;
        }

        // Multi-counter during combo recovery — lane must match
        if (CurrentState == CombatState.ComboRecovery &&
            comboCounterHead < comboCountersSpawned &&
            comboCounterHead < comboCountersTotal)
        {
            if (comboCounterLanes[comboCounterHead] == lane)
            {
                comboCountersLanded++;
                comboCounterHead++;
                OnCounterLanded?.Invoke();
            }
        }
    }

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
        if (CurrentState == CombatState.ComboRecovery)
        {
            GUI.Label(new Rect(10, 110, 380, 20), $"Counters: {comboCountersLanded}/{comboCountersTotal} landed  ({comboCountersSpawned} spawned)");
        }
    }
}
