using UnityEngine;

public enum DodgeDirection { Left, Right, All }

public enum AttackType
{
    Normal,   // standard single hit, left or right
    Feint,    // fakes one direction then switches to the other mid-windup
    Heavy,    // slow windup, big damage, longer active window
    Double,   // two quick hits back to back, same direction both times
    Surge,    // starts slow, then randomly accelerates mid-windup
    Combo     // requires pressing multiple keys simultaneously (Both = A+D, All = A+D+W)
}

[CreateAssetMenu(fileName = "NewAttack", menuName = "BossGame/AttackData")]
public class AttackData : ScriptableObject
{
    [Header("Type")]
    [Tooltip("Determines timing pattern and visual behavior.")]
    public AttackType attackType = AttackType.Normal;

    [Header("Timing (seconds)")]
    public float telegraphDuration  = 1.2f;
    public float activeDuration     = 0.5f;
    public float recoveryDuration   = 1.0f;

    [Header("Timing Window")]
    [Tooltip("Seconds either side of impact that count as a perfect dodge.")]
    public float perfectWindowRadius = 0.12f;

    [Header("Direction")]
    public DodgeDirection requiredDodge = DodgeDirection.Left;

    [Header("Feint (only used if attackType = Feint)")]
    [Tooltip("How far into the telegraph (0-1) the boss fakes the switch.")]
    public float feintSwitchPoint = 0.55f;

    [Header("Double Strike (only used if attackType = Double)")]
    [Tooltip("Delay between first and second hit in a double strike.")]
    public float doubleStrikeDelay = 0.28f;

    [Header("Surge (only used if attackType = Surge)")]
    [Tooltip("Earliest point (0-1) in the windup the surge can trigger.")]
    public float surgeWindowStart = 0.3f;
    [Tooltip("Latest point (0-1) in the windup the surge can trigger.")]
    public float surgeWindowEnd   = 0.65f;
    [Tooltip("How much faster the remaining windup plays after the surge (e.g. 2.5 = 2.5x speed).")]
    public float surgeSpeedMultiplier = 2.5f;

    [Header("Damage")]
    public float damageOnHit = 20f;
}
