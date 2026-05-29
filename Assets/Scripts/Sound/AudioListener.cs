using UnityEngine;

public class AudioListener : MonoBehaviour
{
    [Header("References")]
    public BossController bossController;

    [Header("Music")]
    public AudioClip bossMusic;

    [Header("Player SFX")]
    public AudioClip dodgeClip;
    public AudioClip perfectDodgeClip;
    public AudioClip playerHitClip;
    public AudioClip playerDeathClip;
    public AudioClip counterClip;

    [Header("Attack1 SFX")]
    public AudioClip attack1_1Clip; // left claw
    public AudioClip attack1_2Clip; // right claw
    public AudioClip attack1_3Clip; // fire breath

    [Header("Dash SFX")]
    public AudioClip dashClip;

    [Header("Boss Start SFX")]
    public AudioClip bossStartClip;

    private BossState currentBossState = BossState.Start;

    void Start()
    {
        if (bossMusic != null)
            SoundManager.Instance.PlayMusic(bossMusic);

        if (bossController != null)
        {
            currentBossState = bossController.CurrentBossState;
            bossController.OnBossStateChanged += HandleBossStateChanged;
        }

        CombatManager.Instance.OnPlayerDodgedSuccessfully += PlayDodge;
        CombatManager.Instance.OnPlayerPerfectDodge += PlayPerfectDodge;
        CombatManager.Instance.OnPlayerHit += PlayPlayerHit;
        CombatManager.Instance.OnPlayerDeath += PlayPlayerDeath;
        CombatManager.Instance.OnCounterLanded += PlayCounter;
        CombatManager.Instance.OnStateChanged += HandleCombatStateChanged;
    }

    void OnDestroy()
    {
        if (bossController != null)
            bossController.OnBossStateChanged -= HandleBossStateChanged;

        if (CombatManager.Instance == null) return;

        CombatManager.Instance.OnPlayerDodgedSuccessfully -= PlayDodge;
        CombatManager.Instance.OnPlayerPerfectDodge -= PlayPerfectDodge;
        CombatManager.Instance.OnPlayerHit -= PlayPlayerHit;
        CombatManager.Instance.OnPlayerDeath -= PlayPlayerDeath;
        CombatManager.Instance.OnCounterLanded -= PlayCounter;
        CombatManager.Instance.OnStateChanged -= HandleCombatStateChanged;
    }

    void HandleBossStateChanged(BossState state)
    {
        currentBossState = state;

        if (state == BossState.Start)
            SoundManager.Instance.PlaySFX(bossStartClip);
    }

    void HandleCombatStateChanged(CombatState state)
    {
        if (state != CombatState.Active) return;

        switch (currentBossState)
        {
            case BossState.Attack1:
                PlayAttack1Clip();
                break;

            case BossState.Dash:
                SoundManager.Instance.PlaySFX(dashClip);
                break;
        }
    }

    void PlayAttack1Clip()
    {
        AttackData attack = CombatManager.Instance.CurrentAttack;
        if (attack == null) return;

        if (attack.name.Contains("1_1"))
            SoundManager.Instance.PlaySFX(attack1_1Clip);
        else if (attack.name.Contains("1_2"))
            SoundManager.Instance.PlaySFX(attack1_2Clip);
        else if (attack.name.Contains("1_3"))
            SoundManager.Instance.PlaySFX(attack1_3Clip);
    }

    void PlayDodge()
    {
        SoundManager.Instance.PlaySFX(dodgeClip);
    }

    void PlayPerfectDodge()
    {
        SoundManager.Instance.PlaySFX(perfectDodgeClip);
    }

    void PlayPlayerHit()
    {
        SoundManager.Instance.PlaySFX(playerHitClip);
    }

    void PlayPlayerDeath()
    {
        SoundManager.Instance.PlaySFX(playerDeathClip);
    }

    void PlayCounter()
    {
        SoundManager.Instance.PlaySFX(counterClip);
    }
}