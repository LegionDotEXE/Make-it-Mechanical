using UnityEngine;
using UnityEngine.SceneManagement;

public class GameBootstrap : MonoBehaviour
{
    [Header("Attack Data")]
    public AttackData[] attacks;

    [Header("Layout")]
    public Vector3 playerPosition = new Vector3(0f, -2.5f, 0f);
    public Vector3 bossPosition   = new Vector3(0f,  2.0f, 0f);

    [Header("Arena")]
    public Color bgColor = new Color(0.06f, 0.05f, 0.04f);

    void Awake()
    {
        CreateArena();

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.backgroundColor  = bgColor;
            cam.orthographicSize = 6f;
        }

        new GameObject("InputManager").AddComponent<InputManager>();
        new GameObject("CombatManager").AddComponent<CombatManager>();

        GameObject player = new GameObject("Player");
        player.transform.position = playerPosition;
        player.AddComponent<PlayerController>();
        player.AddComponent<PlayerVisuals>();

        GameObject boss = new GameObject("Boss");
        boss.transform.position = bossPosition;
        BossController bc = boss.AddComponent<BossController>();
        boss.AddComponent<BossVisuals>();

        bc.attacks = (attacks != null && attacks.Length > 0)
            ? attacks : CreateDefaultAttacks();

        UIManager.CreateUI();

        gameObject.AddComponent<CameraEffects>();

        new GameObject("RestartHandler").AddComponent<RestartHandler>();

        Debug.Log("[GameBootstrap] Ready. A=left D=right W=counter R=restart");

        CreateAudioSystem();
    }

    AttackData[] CreateDefaultAttacks()
    {
        AttackData a1 = ScriptableObject.CreateInstance<AttackData>();
        a1.name = "SwipeLeft"; a1.attackType = AttackType.Normal;
        a1.telegraphDuration = 1.3f; a1.activeDuration = 0.5f;
        a1.recoveryDuration = 1.0f; a1.perfectWindowRadius = 0.13f;
        a1.requiredDodge = DodgeDirection.Left; a1.damageOnHit = 20f;

        AttackData a2 = ScriptableObject.CreateInstance<AttackData>();
        a2.name = "SwipeRight"; a2.attackType = AttackType.Normal;
        a2.telegraphDuration = 1.1f; a2.activeDuration = 0.45f;
        a2.recoveryDuration = 0.9f; a2.perfectWindowRadius = 0.13f;
        a2.requiredDodge = DodgeDirection.Right; a2.damageOnHit = 20f;

        AttackData a3 = ScriptableObject.CreateInstance<AttackData>();
        a3.name = "HeavyLeft"; a3.attackType = AttackType.Heavy;
        a3.telegraphDuration = 2.0f; a3.activeDuration = 0.7f;
        a3.recoveryDuration = 1.4f; a3.perfectWindowRadius = 0.18f;
        a3.requiredDodge = DodgeDirection.Left; a3.damageOnHit = 35f;

        AttackData a4 = ScriptableObject.CreateInstance<AttackData>();
        a4.name = "FeintRight"; a4.attackType = AttackType.Feint;
        a4.telegraphDuration = 1.5f; a4.activeDuration = 0.45f;
        a4.recoveryDuration = 1.1f; a4.perfectWindowRadius = 0.11f;
        a4.requiredDodge = DodgeDirection.Right;
        a4.feintSwitchPoint = 0.55f; a4.damageOnHit = 25f;

        AttackData a5 = ScriptableObject.CreateInstance<AttackData>();
        a5.name = "FlurryRight"; a5.attackType = AttackType.Double;
        a5.telegraphDuration = 1.0f; a5.activeDuration = 0.4f;
        a5.recoveryDuration = 0.9f; a5.perfectWindowRadius = 0.11f;
        a5.requiredDodge = DodgeDirection.Right; a5.damageOnHit = 15f;
        a5.doubleStrikeDelay = 0.28f;

        AttackData a6 = ScriptableObject.CreateInstance<AttackData>();
        a6.name = "FlurryLeft"; a6.attackType = AttackType.Double;
        a6.telegraphDuration = 1.0f; a6.activeDuration = 0.4f;
        a6.recoveryDuration = 0.9f; a6.perfectWindowRadius = 0.11f;
        a6.requiredDodge = DodgeDirection.Left; a6.damageOnHit = 15f;
        a6.doubleStrikeDelay = 0.24f;

        AttackData a7 = ScriptableObject.CreateInstance<AttackData>();
        a7.name = "FeintLeft"; a7.attackType = AttackType.Feint;
        a7.telegraphDuration = 1.3f; a7.activeDuration = 0.4f;
        a7.recoveryDuration = 1.0f; a7.perfectWindowRadius = 0.10f;
        a7.requiredDodge = DodgeDirection.Left;
        a7.feintSwitchPoint = 0.45f; a7.damageOnHit = 25f;

        AttackData a8 = ScriptableObject.CreateInstance<AttackData>();
        a8.name = "QuickJabRight"; a8.attackType = AttackType.Normal;
        a8.telegraphDuration = 0.7f; a8.activeDuration = 0.35f;
        a8.recoveryDuration = 0.7f; a8.perfectWindowRadius = 0.10f;
        a8.requiredDodge = DodgeDirection.Right; a8.damageOnHit = 12f;

        AttackData a9 = ScriptableObject.CreateInstance<AttackData>();
        a9.name = "QuickJabLeft"; a9.attackType = AttackType.Normal;
        a9.telegraphDuration = 0.7f; a9.activeDuration = 0.35f;
        a9.recoveryDuration = 0.7f; a9.perfectWindowRadius = 0.10f;
        a9.requiredDodge = DodgeDirection.Left; a9.damageOnHit = 12f;

        return new AttackData[] { a1, a2, a3, a4, a5, a6, a7, a8, a9 };
    }

    void CreateArena()
    {
        MakeSprite("ArenaFloor", Vector3.zero,
            new Vector2(20f, 12f), bgColor, -10);
        MakeSprite("FloorLine", new Vector3(0f, -3.5f, 0f),
            new Vector2(14f, 0.03f), new Color(0.2f, 0.15f, 0.1f, 0.5f), -5);
        for (int i = 0; i < 5; i++)
            MakeSprite($"Fog_{i}", new Vector3(0f, -4f + i * 2.5f, 0f),
                new Vector2(16f, 0.02f), new Color(0.15f, 0.1f, 0.08f, 0.15f), -4);
        MakeSprite("PillarL", new Vector3(-5.5f, 0f, 0f),
            new Vector2(0.4f, 8f), new Color(0.1f, 0.08f, 0.06f, 0.6f), -3);
        MakeSprite("PillarR", new Vector3( 5.5f, 0f, 0f),
            new Vector2(0.4f, 8f), new Color(0.1f, 0.08f, 0.06f, 0.6f), -3);
    }

    void MakeSprite(string name, Vector3 pos, Vector2 size, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.position = pos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PlayerVisuals.CreateRect(size.x, size.y);
        sr.color  = color;
        sr.sortingOrder = order;
    }

    void CreateAudioSystem()
    {
        GameObject audioGO = new GameObject("SoundManager");

        SoundManager sm = audioGO.AddComponent<SoundManager>();

        AudioSource musicSource = audioGO.AddComponent<AudioSource>();
        AudioSource sfxSource = audioGO.AddComponent<AudioSource>();
        AudioSource loopSource = audioGO.AddComponent<AudioSource>();

        musicSource.playOnAwake = false;
        sfxSource.playOnAwake = false;
        loopSource.playOnAwake = false;

        sm.musicSource = musicSource;
        sm.sfxSource = sfxSource;
        sm.loopSource = loopSource;

        new GameObject("GameAudioController").AddComponent<GameAudioController>();
    }
}

public class RestartHandler : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
