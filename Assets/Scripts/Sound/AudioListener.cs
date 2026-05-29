using UnityEngine;

public class AudioListener : MonoBehaviour
{
    [Header("Music")]
    public AudioClip bossMusic;

    [Header("Player SFX")]
    public AudioClip dodgeClip;
    public AudioClip perfectDodgeClip;
    public AudioClip playerHitClip;
    public AudioClip playerDeathClip;
    public AudioClip counterClip;

    [Header("Attack SFX")]
    public AudioClip attack1_1Clip; // left claw
    public AudioClip attack1_2Clip; // right claw
    public AudioClip attack1_3Clip; // fire breath

    [Header("Dash SFX")]
    public AudioClip dashClip;

    [Header("Boss Start SFX")]
    public AudioClip bossStartClip;

    void Start()
    {
        if (bossMusic != null)
            SoundManager.Instance.PlayMusic(bossMusic);

        if (bossStartClip != null)
            SoundManager.Instance.PlaySFX(bossStartClip);

        CombatManager.Instance.OnPlayerDodgedSuccessfully += PlayDodge;
        CombatManager.Instance.OnPlayerPerfectDodge += PlayPerfectDodge;
        CombatManager.Instance.OnPlayerHit += PlayPlayerHit;
        CombatManager.Instance.OnPlayerDeath += PlayPlayerDeath;
        CombatManager.Instance.OnCounterLanded += PlayCounter;
        CombatManager.Instance.OnStateChanged += HandleCombatStateChanged;
    }

    void OnDestroy()
    {
        if (CombatManager.Instance == null) return;

        CombatManager.Instance.OnPlayerDodgedSuccessfully -= PlayDodge;
        CombatManager.Instance.OnPlayerPerfectDodge -= PlayPerfectDodge;
        CombatManager.Instance.OnPlayerHit -= PlayPlayerHit;
        CombatManager.Instance.OnPlayerDeath -= PlayPlayerDeath;
        CombatManager.Instance.OnCounterLanded -= PlayCounter;
        CombatManager.Instance.OnStateChanged -= HandleCombatStateChanged;
    }

    void HandleCombatStateChanged(CombatState state)
    {
        if (state != CombatState.Active) return;

        AttackData attack = CombatManager.Instance.CurrentAttack;
        if (attack == null) return;

        string attackName = attack.name.ToLower();

        if (attackName.Contains("1_1"))
            SoundManager.Instance.PlaySFX(attack1_1Clip);
        else if (attackName.Contains("1_2"))
            SoundManager.Instance.PlaySFX(attack1_2Clip);
        else if (attackName.Contains("1_3"))
            SoundManager.Instance.PlaySFX(attack1_3Clip);
        else if (attackName.Contains("dash"))
            SoundManager.Instance.PlaySFX(dashClip);
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