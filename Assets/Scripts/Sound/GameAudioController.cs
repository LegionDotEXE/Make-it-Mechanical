using UnityEngine;

public class GameAudioController : MonoBehaviour
{
    [Header("Music")]
    public AudioClip bossMusic;   // assign a track you have the rights to use

    [Header("Player")]
    public AudioClip dodgeClip;
    public AudioClip perfectDodgeClip;
    public AudioClip playerHit1;
    public AudioClip playerHit2;
    public AudioClip counterClip;
    public AudioClip deathClip;
    public AudioClip evadeWhooshClip;    // played on every dodge press (falls back to dodgeClip)
    public AudioClip counterWhiffClip;   // played when W is mashed with no valid target

    [Header("Boss")]
    public AudioClip normalAttackClip;
    public AudioClip feintAttackClip;

    public AudioClip heavyAttack1;
    public AudioClip heavyAttack2;
    public AudioClip heavyWindup;

    public AudioClip unblockableAttackClip;   // swing/impact of an unblockable
    public AudioClip unblockableWindup;        // optional windup loop (falls back to heavyWindup)

    private bool windupLoopPlaying = false;
    private bool attackSoundPlayed = false;

    [Header("Volumes")]
    [Range(0f, 1f)] public float bossAttackVolume = 0.9f;
    [Range(0f, 1f)] public float heavyWindupVolume = 0.9f;
    [Range(0f, 1f)] public float unblockableWindupVolume = 0.9f;
    [Range(0f, 1f)] public float dodgeVolume = 0.75f;
    [Range(0f, 1f)] public float perfectDodgeVolume = 1f;
    [Range(0f, 1f)] public float playerHitVolume = 1f;
    [Range(0f, 1f)] public float counterVolume = 1f;
    [Range(0f, 1f)] public float counterWhiffVolume = 0.5f;
    [Range(0f, 1f)] public float deathVolume = 1f;

    void Start()
    {
        if (bossMusic != null)
            SoundManager.Instance.PlayMusic(bossMusic);

        CombatManager.Instance.OnStateChanged += HandleStateChanged;

        CombatManager.Instance.OnPlayerEvadeStarted       += PlayEvadeWhoosh;
        CombatManager.Instance.OnPlayerDodgedSuccessfully += PlayDodge;
        CombatManager.Instance.OnPlayerPerfectDodge       += PlayPerfectDodge;
        CombatManager.Instance.OnPlayerHit                += PlayPlayerHit;
        CombatManager.Instance.OnCounterLanded            += PlayCounter;
        CombatManager.Instance.OnCounterWhiffed           += PlayCounterWhiff;
        CombatManager.Instance.OnPlayerDeath              += PlayDeath;
    }

    void OnDestroy()
    {
        if (CombatManager.Instance == null) return;

        CombatManager.Instance.OnStateChanged -= HandleStateChanged;

        CombatManager.Instance.OnPlayerEvadeStarted       -= PlayEvadeWhoosh;
        CombatManager.Instance.OnPlayerDodgedSuccessfully -= PlayDodge;
        CombatManager.Instance.OnPlayerPerfectDodge       -= PlayPerfectDodge;
        CombatManager.Instance.OnPlayerHit                -= PlayPlayerHit;
        CombatManager.Instance.OnCounterLanded            -= PlayCounter;
        CombatManager.Instance.OnCounterWhiffed           -= PlayCounterWhiff;
        CombatManager.Instance.OnPlayerDeath              -= PlayDeath;
    }

    void HandleStateChanged(CombatState state)
    {
        AttackData attack = CombatManager.Instance.CurrentAttack;
        if (attack == null) return;

        switch (state)
        {
            case CombatState.Windup:
                attackSoundPlayed = false;

                if (attack.attackType == AttackType.Heavy)
                {
                    SoundManager.Instance.StartLoop(heavyWindup, heavyWindupVolume);
                    windupLoopPlaying = true;
                }
                else if (attack.attackType == AttackType.Unblockable)
                {
                    SoundManager.Instance.StartLoop(
                        unblockableWindup != null ? unblockableWindup : heavyWindup,
                        unblockableWindupVolume);
                    windupLoopPlaying = true;
                }
                break;

            case CombatState.Active:
                StopWindupLoop();
                PlayAttackSoundOnce(attack);
                break;

            case CombatState.Recovery:
            case CombatState.Idle:
            case CombatState.PerfectWindow:
            case CombatState.Counter:
                StopWindupLoop();
                break;
        }
    }

    void PlayAttackSoundOnce(AttackData attack)
    {
        if (attackSoundPlayed) return;
        attackSoundPlayed = true;

        switch (attack.attackType)
        {
            case AttackType.Normal:
                SoundManager.Instance.PlaySFX(normalAttackClip, bossAttackVolume);
                break;

            case AttackType.Feint:
                SoundManager.Instance.PlaySFX(
                    feintAttackClip != null ? feintAttackClip : normalAttackClip,
                    bossAttackVolume);
                break;

            case AttackType.Heavy:
                SoundManager.Instance.PlaySFX(
                    Random.value > 0.5f ? heavyAttack1 : heavyAttack2,
                    bossAttackVolume);
                break;

            case AttackType.Double:
                SoundManager.Instance.PlaySFX(normalAttackClip, bossAttackVolume);
                break;

            case AttackType.Unblockable:
                SoundManager.Instance.PlaySFX(
                    unblockableAttackClip != null ? unblockableAttackClip
                        : (heavyAttack1 != null ? heavyAttack1 : normalAttackClip),
                    bossAttackVolume);
                break;
        }
    }

    void PlayEvadeWhoosh(DodgeDirection dir)
    {
        // fires on every committed dodge press, so the roll is audible even on a mistime
        SoundManager.Instance.PlaySFX(
            evadeWhooshClip != null ? evadeWhooshClip : dodgeClip, dodgeVolume);
    }

    void PlayDodge()
    {
        // whoosh already played on the press; just ensure the boss swing reads and the
        // windup loop is stopped
        PlayAttackSoundOnce(CombatManager.Instance.CurrentAttack);
        StopWindupLoop();
    }

    void PlayPerfectDodge()
    {
        PlayAttackSoundOnce(CombatManager.Instance.CurrentAttack);
        StopWindupLoop();
        SoundManager.Instance.PlaySFX(perfectDodgeClip, perfectDodgeVolume);
    }

    void PlayCounter()
    {
        SoundManager.Instance.PlaySFX(counterClip, counterVolume);
    }

    void PlayCounterWhiff()
    {
        SoundManager.Instance.PlaySFX(counterWhiffClip, counterWhiffVolume);
    }

    void PlayPlayerHit()
    {
        if (Random.value > 0.5f)
            SoundManager.Instance.PlaySFX(playerHit1, playerHitVolume);
        else
            SoundManager.Instance.PlaySFX(playerHit2, playerHitVolume);
    }

    void PlayDeath()
    {
        StopWindupLoop();
        SoundManager.Instance.PlaySFX(deathClip, deathVolume);
    }

    void StopWindupLoop()
    {
        if (!windupLoopPlaying) return;
        SoundManager.Instance.StopLoop();
        windupLoopPlaying = false;
    }
}