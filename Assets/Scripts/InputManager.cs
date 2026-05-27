using System;
using UnityEngine;


public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("Key Bindings")]
    public KeyCode dodgeLeftKey  = KeyCode.A;
    public KeyCode dodgeRightKey = KeyCode.D;
    public KeyCode counterKey    = KeyCode.W;

    public event Action OnDodgeLeft;
    public event Action OnDodgeRight;
    public event Action OnCounter;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        if (Input.GetKeyDown(dodgeLeftKey))
            OnDodgeLeft?.Invoke();

        if (Input.GetKeyDown(dodgeRightKey))
            OnDodgeRight?.Invoke();

        if (Input.GetKeyDown(counterKey))
            OnCounter?.Invoke();
    }
}
