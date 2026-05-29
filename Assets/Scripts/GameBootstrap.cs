using UnityEngine;
using UnityEngine.SceneManagement;

public class GameBootstrap : MonoBehaviour
{
    [Header("Combo Patterns (leave empty for defaults)")]
    public ComboPattern[] combos;

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

        bc.combos = (combos != null && combos.Length > 0)
            ? combos : CreateDefaultCombos();

        UIManager.CreateUI();

        gameObject.AddComponent<CameraEffects>();

        new GameObject("RestartHandler").AddComponent<RestartHandler>();

        Debug.Log("[GameBootstrap] Ready. A=left W=counter D=right R=restart");

        CreateAudioSystem();
    }

    ComboPattern[] CreateDefaultCombos()
    {
        // ---- Individual attacks ----

        AttackData swipeL = ScriptableObject.CreateInstance<AttackData>();
        swipeL.name = "SwipeLeft"; swipeL.attackType = AttackType.Normal;
        swipeL.telegraphDuration = 1.3f; swipeL.activeDuration = 0.5f;
        swipeL.recoveryDuration = 1.0f; swipeL.perfectWindowRadius = 0.13f;
        swipeL.requiredDodge = DodgeDirection.Left; swipeL.damageOnHit = 20f;

        AttackData swipeR = ScriptableObject.CreateInstance<AttackData>();
        swipeR.name = "SwipeRight"; swipeR.attackType = AttackType.Normal;
        swipeR.telegraphDuration = 1.1f; swipeR.activeDuration = 0.45f;
        swipeR.recoveryDuration = 0.9f; swipeR.perfectWindowRadius = 0.13f;
        swipeR.requiredDodge = DodgeDirection.Right; swipeR.damageOnHit = 20f;

        AttackData heavyL = ScriptableObject.CreateInstance<AttackData>();
        heavyL.name = "HeavyLeft"; heavyL.attackType = AttackType.Heavy;
        heavyL.telegraphDuration = 2.0f; heavyL.activeDuration = 0.7f;
        heavyL.recoveryDuration = 1.4f; heavyL.perfectWindowRadius = 0.18f;
        heavyL.requiredDodge = DodgeDirection.Left; heavyL.damageOnHit = 35f;

        AttackData feintR = ScriptableObject.CreateInstance<AttackData>();
        feintR.name = "FeintRight"; feintR.attackType = AttackType.Feint;
        feintR.telegraphDuration = 1.5f; feintR.activeDuration = 0.45f;
        feintR.recoveryDuration = 1.1f; feintR.perfectWindowRadius = 0.11f;
        feintR.requiredDodge = DodgeDirection.Right;
        feintR.feintSwitchPoint = 0.55f; feintR.damageOnHit = 25f;

        AttackData feintL = ScriptableObject.CreateInstance<AttackData>();
        feintL.name = "FeintLeft"; feintL.attackType = AttackType.Feint;
        feintL.telegraphDuration = 1.3f; feintL.activeDuration = 0.4f;
        feintL.recoveryDuration = 1.0f; feintL.perfectWindowRadius = 0.10f;
        feintL.requiredDodge = DodgeDirection.Left;
        feintL.feintSwitchPoint = 0.45f; feintL.damageOnHit = 25f;

        AttackData flurryR = ScriptableObject.CreateInstance<AttackData>();
        flurryR.name = "FlurryRight"; flurryR.attackType = AttackType.Double;
        flurryR.telegraphDuration = 1.0f; flurryR.activeDuration = 0.4f;
        flurryR.recoveryDuration = 0.9f; flurryR.perfectWindowRadius = 0.11f;
        flurryR.requiredDodge = DodgeDirection.Right; flurryR.damageOnHit = 15f;
        flurryR.doubleStrikeDelay = 0.28f;

        AttackData flurryL = ScriptableObject.CreateInstance<AttackData>();
        flurryL.name = "FlurryLeft"; flurryL.attackType = AttackType.Double;
        flurryL.telegraphDuration = 1.0f; flurryL.activeDuration = 0.4f;
        flurryL.recoveryDuration = 0.9f; flurryL.perfectWindowRadius = 0.11f;
        flurryL.requiredDodge = DodgeDirection.Left; flurryL.damageOnHit = 15f;
        flurryL.doubleStrikeDelay = 0.24f;

        AttackData jabR = ScriptableObject.CreateInstance<AttackData>();
        jabR.name = "QuickJabRight"; jabR.attackType = AttackType.Normal;
        jabR.telegraphDuration = 0.7f; jabR.activeDuration = 0.35f;
        jabR.recoveryDuration = 0.7f; jabR.perfectWindowRadius = 0.10f;
        jabR.requiredDodge = DodgeDirection.Right; jabR.damageOnHit = 12f;

        AttackData jabL = ScriptableObject.CreateInstance<AttackData>();
        jabL.name = "QuickJabLeft"; jabL.attackType = AttackType.Normal;
        jabL.telegraphDuration = 0.7f; jabL.activeDuration = 0.35f;
        jabL.recoveryDuration = 0.7f; jabL.perfectWindowRadius = 0.10f;
        jabL.requiredDodge = DodgeDirection.Left; jabL.damageOnHit = 12f;

        AttackData surgeR = ScriptableObject.CreateInstance<AttackData>();
        surgeR.name = "SurgeRight"; surgeR.attackType = AttackType.Surge;
        surgeR.telegraphDuration = 1.8f; surgeR.activeDuration = 0.5f;
        surgeR.recoveryDuration = 1.0f; surgeR.perfectWindowRadius = 0.12f;
        surgeR.requiredDodge = DodgeDirection.Right; surgeR.damageOnHit = 28f;
        surgeR.surgeWindowStart = 0.3f; surgeR.surgeWindowEnd = 0.65f;
        surgeR.surgeSpeedMultiplier = 2.5f;

        AttackData surgeL = ScriptableObject.CreateInstance<AttackData>();
        surgeL.name = "SurgeLeft"; surgeL.attackType = AttackType.Surge;
        surgeL.telegraphDuration = 1.8f; surgeL.activeDuration = 0.5f;
        surgeL.recoveryDuration = 1.0f; surgeL.perfectWindowRadius = 0.12f;
        surgeL.requiredDodge = DodgeDirection.Left; surgeL.damageOnHit = 28f;
        surgeL.surgeWindowStart = 0.3f; surgeL.surgeWindowEnd = 0.65f;
        surgeL.surgeSpeedMultiplier = 2.5f;

        AttackData slam = ScriptableObject.CreateInstance<AttackData>();
        slam.name = "OverheadSlam"; slam.attackType = AttackType.Combo;
        slam.telegraphDuration = 2.2f; slam.activeDuration = 0.7f;
        slam.recoveryDuration = 1.5f; slam.perfectWindowRadius = 0.18f;
        slam.requiredDodge = DodgeDirection.All; slam.damageOnHit = 45f;

        // ---- Fast variants for hard combos (shorter telegraphs) ----

        AttackData fastSwipeL = ScriptableObject.CreateInstance<AttackData>();
        fastSwipeL.name = "FastSwipeLeft"; fastSwipeL.attackType = AttackType.Normal;
        fastSwipeL.telegraphDuration = 0.5f; fastSwipeL.activeDuration = 0.35f;
        fastSwipeL.recoveryDuration = 0.5f; fastSwipeL.perfectWindowRadius = 0.09f;
        fastSwipeL.requiredDodge = DodgeDirection.Left; fastSwipeL.damageOnHit = 18f;

        AttackData fastSwipeR = ScriptableObject.CreateInstance<AttackData>();
        fastSwipeR.name = "FastSwipeRight"; fastSwipeR.attackType = AttackType.Normal;
        fastSwipeR.telegraphDuration = 0.5f; fastSwipeR.activeDuration = 0.35f;
        fastSwipeR.recoveryDuration = 0.5f; fastSwipeR.perfectWindowRadius = 0.09f;
        fastSwipeR.requiredDodge = DodgeDirection.Right; fastSwipeR.damageOnHit = 18f;

        AttackData fastJabL = ScriptableObject.CreateInstance<AttackData>();
        fastJabL.name = "FastJabLeft"; fastJabL.attackType = AttackType.Normal;
        fastJabL.telegraphDuration = 0.4f; fastJabL.activeDuration = 0.25f;
        fastJabL.recoveryDuration = 0.4f; fastJabL.perfectWindowRadius = 0.08f;
        fastJabL.requiredDodge = DodgeDirection.Left; fastJabL.damageOnHit = 10f;

        AttackData fastJabR = ScriptableObject.CreateInstance<AttackData>();
        fastJabR.name = "FastJabRight"; fastJabR.attackType = AttackType.Normal;
        fastJabR.telegraphDuration = 0.4f; fastJabR.activeDuration = 0.25f;
        fastJabR.recoveryDuration = 0.4f; fastJabR.perfectWindowRadius = 0.08f;
        fastJabR.requiredDodge = DodgeDirection.Right; fastJabR.damageOnHit = 10f;

        AttackData fastFeintR = ScriptableObject.CreateInstance<AttackData>();
        fastFeintR.name = "FastFeintRight"; fastFeintR.attackType = AttackType.Feint;
        fastFeintR.telegraphDuration = 0.6f; fastFeintR.activeDuration = 0.3f;
        fastFeintR.recoveryDuration = 0.5f; fastFeintR.perfectWindowRadius = 0.08f;
        fastFeintR.requiredDodge = DodgeDirection.Right;
        fastFeintR.feintSwitchPoint = 0.5f; fastFeintR.damageOnHit = 22f;

        // ---- 6 Combo Patterns ----

        // Combo 1: "Flurry Assault" — fast jabs into a flurry finisher
        ComboPattern c1 = new ComboPattern
        {
            comboName = "Flurry Assault",
            attacks = new AttackData[] { jabL, jabR, jabL, flurryR },
            minCounterHits = 1,
            maxCounterHits = 2
        };

        // Combo 2: "Deceptive Onslaught" — feints mixed with swipes
        ComboPattern c2 = new ComboPattern
        {
            comboName = "Deceptive Onslaught",
            attacks = new AttackData[] { swipeR, feintL, swipeL, feintR, flurryL },
            minCounterHits = 2,
            maxCounterHits = 3
        };

        // Combo 3: "Wrath of the Abyss" — heavy + surge, big damage
        ComboPattern c3 = new ComboPattern
        {
            comboName = "Wrath of the Abyss",
            attacks = new AttackData[] { heavyL, surgeR, swipeL, surgeL, heavyL, slam },
            minCounterHits = 2,
            maxCounterHits = 3
        };

        // Combo 4: "Relentless Barrage" — long chain of mixed attacks
        ComboPattern c4 = new ComboPattern
        {
            comboName = "Relentless Barrage",
            attacks = new AttackData[] { jabR, swipeL, flurryR, feintL, surgeR },
            minCounterHits = 1,
            maxCounterHits = 3
        };

        // Combo 5: "Blade Storm" — rapid-fire alternating slashes, no breathing room
        ComboPattern c5 = new ComboPattern
        {
            comboName = "Blade Storm",
            attacks = new AttackData[] { fastJabL, fastJabR, fastSwipeL, fastJabR, fastSwipeR, fastJabL },
            minCounterHits = 2,
            maxCounterHits = 3
        };

        // Combo 6: "Abyssal Frenzy" — fast attacks with a feint thrown in to trip you up
        ComboPattern c6 = new ComboPattern
        {
            comboName = "Abyssal Frenzy",
            attacks = new AttackData[] { fastSwipeR, fastJabL, fastFeintR, fastSwipeL, fastJabR, fastSwipeL, slam },
            minCounterHits = 2,
            maxCounterHits = 3
        };

        return new ComboPattern[] { c1, c2, c3, c4, c5, c6 };
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
