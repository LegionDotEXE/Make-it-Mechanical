using UnityEngine;

public class GameAudioController : MonoBehaviour
{
    [Header("Player")]
    public AudioClip dodgeClip;
    public AudioClip perfectDodgeClip;
    public AudioClip playerHit1;
    public AudioClip playerHit2;
    public AudioClip counterClip;
    public AudioClip deathClip;

    [Header("Boss")]
    public AudioClip normalAttackClip;
    public AudioClip feintAttackClip;

    public AudioClip heavyAttack1;
    public AudioClip heavyAttack2;

    public AudioClip heavyWindup;

    private bool heavyWindupPlaying = false;
    private bool attackSoundPlayed = false;

    [Header("Volumes")]
    [Range(0f, 1f)] public float bossAttackVolume = 0.9f;
    [Range(0f, 1f)] public float heavyWindupVolume = 0.9f;
    [Range(0f, 1f)] public float dodgeVolume = 0.75f;
    [Range(0f, 1f)] public float perfectDodgeVolume = 1f;
    [Range(0f, 1f)] public float playerHitVolume = 1.2f;
    [Range(0f, 1f)] public float counterVolume = 1f;
    [Range(0f, 1f)] public float deathVolume = 1f;

    void Start()
    {
        CombatManager.Instance.OnStateChanged += HandleStateChanged;

        CombatManager.Instance.OnPlayerDodgedSuccessfully += PlayDodge;
        CombatManager.Instance.OnPlayerPerfectDodge += PlayPerfectDodge;
        CombatManager.Instance.OnPlayerHit += PlayPlayerHit;
        CombatManager.Instance.OnCounterLanded += PlayCounter;
        CombatManager.Instance.OnPlayerDeath += PlayDeath;
    }

    void OnDestroy()
    {
        if (CombatManager.Instance == null) return;

        CombatManager.Instance.OnStateChanged -= HandleStateChanged;

        CombatManager.Instance.OnPlayerDodgedSuccessfully -= PlayDodge;
        CombatManager.Instance.OnPlayerPerfectDodge -= PlayPerfectDodge;
        CombatManager.Instance.OnPlayerHit -= PlayPlayerHit;
        CombatManager.Instance.OnCounterLanded -= PlayCounter;
        CombatManager.Instance.OnPlayerDeath -= PlayDeath;
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
                    heavyWindupPlaying = true;
                }
                break;

            case CombatState.Active:
                StopHeavyWindupIfNeeded();
                PlayAttackSoundOnce(attack);
                break;

            case CombatState.Recovery:
            case CombatState.Idle:
            case CombatState.PerfectWindow:
            case CombatState.Counter:
                StopHeavyWindupIfNeeded();
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
                    bossAttackVolume
                );
                break;

            case AttackType.Heavy:
                SoundManager.Instance.PlaySFX(
                    Random.value > 0.5f ? heavyAttack1 : heavyAttack2,
                    bossAttackVolume
                );
                break;

            case AttackType.Double:
                SoundManager.Instance.PlaySFX(normalAttackClip, bossAttackVolume);
                break;
        }
    }

    void PlayDodge()
    {
        PlayAttackSoundOnce(CombatManager.Instance.CurrentAttack);
        StopHeavyWindupIfNeeded();
        SoundManager.Instance.PlaySFX(dodgeClip, dodgeVolume);
    }

    void PlayPerfectDodge()
    {
        PlayAttackSoundOnce(CombatManager.Instance.CurrentAttack);
        StopHeavyWindupIfNeeded();
        SoundManager.Instance.PlaySFX(perfectDodgeClip, perfectDodgeVolume);
    }

    void PlayCounter()
    {
        SoundManager.Instance.PlaySFX(counterClip, counterVolume);
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
        StopHeavyWindupIfNeeded();
        SoundManager.Instance.PlaySFX(deathClip, deathVolume);
    }

    void StopHeavyWindupIfNeeded()
    {
        if (!heavyWindupPlaying) return;

        SoundManager.Instance.StopLoop();
        heavyWindupPlaying = false;
    }
}
