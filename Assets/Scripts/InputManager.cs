using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    public event Action OnDodgeLeft;
    public event Action OnDodgeRight;
    public event Action OnCounter;

    private InputAction dodgeLeftAction;
    private InputAction dodgeRightAction;
    private InputAction counterAction;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        dodgeLeftAction  = new InputAction("DodgeLeft",  InputActionType.Button, "<Keyboard>/a");
        dodgeRightAction = new InputAction("DodgeRight", InputActionType.Button, "<Keyboard>/d");
        counterAction    = new InputAction("Counter",    InputActionType.Button, "<Keyboard>/w");

        dodgeLeftAction.performed  += ctx => OnDodgeLeft?.Invoke();
        dodgeRightAction.performed += ctx => OnDodgeRight?.Invoke();
        counterAction.performed    += ctx => OnCounter?.Invoke();
    }

    void OnEnable()
    {
        dodgeLeftAction?.Enable();
        dodgeRightAction?.Enable();
        counterAction?.Enable();
    }

    void OnDisable()
    {
        dodgeLeftAction?.Disable();
        dodgeRightAction?.Disable();
        counterAction?.Disable();
    }

    void OnDestroy()
    {
        dodgeLeftAction?.Dispose();
        dodgeRightAction?.Dispose();
        counterAction?.Dispose();
    }
}
