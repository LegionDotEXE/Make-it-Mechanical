using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Drop this on an empty GameObject in your scene. It spawns all the managers,
/// the player, the boss, the UI, and the arena background — everything needed
/// to hit Play and test combat.
///
/// Assign at least one AttackData asset in the Inspector (or it creates a default).
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [Header("Attack Data (assign BossAttack_01 etc.)")]
    [Tooltip("Drag your AttackData ScriptableObjects here. If empty, a default attack is created at runtime.")]
    public AttackData[] attacks;

    [Header("Layout")]
    public Vector3 playerPosition = new Vector3(0f, -2.5f, 0f);
    public Vector3 bossPosition   = new Vector3(0f,  2.0f, 0f);

    [Header("Arena")]
    public Color bgColor       = new Color(0.06f, 0.05f, 0.04f);  // near black
    public Color floorColor    = new Color(0.12f, 0.1f,  0.08f);  // dark stone
    public Color ambientColor  = new Color(0.08f, 0.06f, 0.05f);

    void Awake()
    {
        // --- 1. Arena background ---
        CreateArena();

        // --- 2. Camera setup ---
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.backgroundColor  = bgColor;
            cam.orthographicSize = 6f;
        }

        CreateSingleton<InputManager>("InputManager");
        CreateSingleton<CombatManager>("CombatManager");

        GameObject player = new GameObject("Player");
        player.transform.position = playerPosition;
        player.AddComponent<PlayerController>();
        player.AddComponent<PlayerVisuals>();

        GameObject boss = new GameObject("Boss");
        boss.transform.position = bossPosition;

        BossController bc = boss.AddComponent<BossController>();
        boss.AddComponent<BossVisuals>();

        // assign attacks
        if (attacks != null && attacks.Length > 0)
        {
            bc.attacks = attacks;
        }
        else
        {
            // create a runtime default so we can still test
            AttackData defaultAtk = ScriptableObject.CreateInstance<AttackData>();
            defaultAtk.name               = "DefaultAttack";
            defaultAtk.telegraphDuration   = 1.2f;
            defaultAtk.activeDuration      = 0.5f;
            defaultAtk.recoveryDuration    = 1.0f;
            defaultAtk.perfectWindowRadius = 0.12f;
            defaultAtk.requiredDodge       = DodgeDirection.Left;
            defaultAtk.damageOnHit         = 20f;

            AttackData defaultAtk2 = ScriptableObject.CreateInstance<AttackData>();
            defaultAtk2.name               = "DefaultAttackRight";
            defaultAtk2.telegraphDuration   = 1.0f;
            defaultAtk2.activeDuration      = 0.4f;
            defaultAtk2.recoveryDuration    = 0.8f;
            defaultAtk2.perfectWindowRadius = 0.15f;
            defaultAtk2.requiredDodge       = DodgeDirection.Right;
            defaultAtk2.damageOnHit         = 15f;

            bc.attacks = new AttackData[] { defaultAtk, defaultAtk2 };
            Debug.LogWarning("[GameBootstrap] No attacks assigned — using runtime defaults. " +
                             "Drag your AttackData assets into the Bootstrap Inspector for real data.");
        }

        UIManager ui = UIManager.CreateUI();

        bc.OnBossHealthChanged.AddListener(ui.UpdateBossHealth);
        PlayerController pc = player.GetComponent<PlayerController>();
        pc.OnHealthChanged.AddListener(ui.UpdatePlayerHealth);

        gameObject.AddComponent<RestartHandler>();
        gameObject.AddComponent<CameraEffects>();


        //Debug.Log("[GameBootstrap] Scene initialized. Controls: A = dodge left, D = dodge right, W = counter, R = restart");
        Debug.Log("[GameBootstrap] Scene initialized. Controls: A = dodge left, D = dodge right, W = counter, R = restart");
    }

    void CreateArena()
    {
        // dark floor
        GameObject floor = new GameObject("ArenaFloor");
        SpriteRenderer floorSR = floor.AddComponent<SpriteRenderer>();
        floorSR.sprite       = PlayerVisuals.CreateRect(20f, 12f);
        floorSR.color        = bgColor;
        floorSR.sortingOrder = -10;
        floor.transform.position = Vector3.zero;

        // floor line
        GameObject floorLine = new GameObject("FloorLine");
        SpriteRenderer lineSR = floorLine.AddComponent<SpriteRenderer>();
        lineSR.sprite       = PlayerVisuals.CreateRect(14f, 0.03f);
        lineSR.color        = new Color(0.2f, 0.15f, 0.1f, 0.5f);
        lineSR.sortingOrder = -5;
        floorLine.transform.position = new Vector3(0f, -3.5f, 0f);

        // atmospheric fog bars (subtle horizontal lines)
        for (int i = 0; i < 5; i++)
        {
            GameObject fog = new GameObject($"FogBar_{i}");
            SpriteRenderer fogSR = fog.AddComponent<SpriteRenderer>();
            fogSR.sprite       = PlayerVisuals.CreateRect(16f, 0.02f);
            fogSR.color        = new Color(0.15f, 0.1f, 0.08f, 0.15f);
            fogSR.sortingOrder = -4;
            fog.transform.position = new Vector3(0f, -4f + i * 2.5f, 0f);
        }

        // side pillars for atmosphere
        CreatePillar(new Vector3(-5.5f, 0f, 0f));
        CreatePillar(new Vector3(5.5f,  0f, 0f));
    }

    void CreatePillar(Vector3 pos)
    {
        GameObject pillar = new GameObject("Pillar");
        SpriteRenderer sr = pillar.AddComponent<SpriteRenderer>();
        sr.sprite       = PlayerVisuals.CreateRect(0.4f, 8f);
        sr.color        = new Color(0.1f, 0.08f, 0.06f, 0.6f);
        sr.sortingOrder = -3;
        pillar.transform.position = pos;
    }

    T CreateSingleton<T>(string name) where T : MonoBehaviour
    {
        // don't double-create if already in scene
        T existing = FindAnyObjectByType<T>();
        if (existing != null) return existing;

        GameObject go = new GameObject(name);
        return go.AddComponent<T>();
    }
}


public class RestartHandler : MonoBehaviour
{
    private InputAction restartAction;

    void Awake()
    {
        restartAction = new InputAction("Restart", InputActionType.Button, "<Keyboard>/r");
        restartAction.performed += ctx => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnEnable()  => restartAction?.Enable();
    void OnDisable() => restartAction?.Disable();
    void OnDestroy() => restartAction?.Dispose();
}
