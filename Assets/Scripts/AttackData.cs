using UnityEngine;

[CreateAssetMenu(fileName = "NewAttack", menuName = "BossGame/AttackData")]
public class AttackData : ScriptableObject
{
    [Header("Timing (seconds)")]
    [Tooltip("How long the boss winds up before the attack goes active.")]
    public float telegraphDuration = 1.2f;

    [Tooltip("How long the hitbox is actually active / the dodge window is open.")]
    public float activeDuration = 0.5f;

    [Tooltip("How long the boss rests after the attack before the next one.")]
    public float recoveryDuration = 1.0f;

    [Header("Timing Window")]
    [Tooltip("Seconds on either side of impact that count as a perfect dodge.")]
    public float perfectWindowRadius = 0.12f;

    [Header("Direction")]
    [Tooltip("Which direction the player must dodge to avoid this attack.")]
    public DodgeDirection requiredDodge = DodgeDirection.Left;

    [Header("Damage")]
    [Tooltip("How much health the player loses if they don't dodge.")]
    public float damageOnHit = 20f;
}

// kept here since it's tightly coupled to AttackData
public enum DodgeDirection
{
    Left,
    Right
    // could add Forward later if we want a third dodge type
}
