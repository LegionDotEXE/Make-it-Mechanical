using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    public event Action OnDodgeLeft;
    public event Action OnDodgeRight;
    public event Action OnDodgeAll;       // A+D+W simultaneous
    public event Action<int> OnCounter;   // int = lane (0=left/A, 1=center/W, 2=right/D)

    private InputAction dodgeLeftAction;
    private InputAction dodgeRightAction;
    private InputAction counterAction;

    // Combo detection: buffer key presses and wait a short window
    // before deciding if it's a single press or a combo.
    private const float COMBO_TOLERANCE = 0.1f; // seconds
    private float leftPressTime    = -1f;
    private float rightPressTime   = -1f;
    private float counterPressTime = -1f;
    private bool  leftPending;
    private bool  rightPending;
    private bool  counterPending;

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

        dodgeLeftAction.performed  += ctx => BufferKey(ref leftPressTime, ref leftPending);
        dodgeRightAction.performed += ctx => BufferKey(ref rightPressTime, ref rightPending);
        counterAction.performed    += ctx => BufferKey(ref counterPressTime, ref counterPending);
    }

    void BufferKey(ref float pressTime, ref bool pending)
    {
        pressTime = Time.time;
        pending   = true;
    }

    void Update()
    {
        // Nothing buffered
        if (!leftPending && !rightPending && !counterPending) return;

        // Find the earliest press in the current buffer
        float earliest = float.MaxValue;
        if (leftPending)    earliest = Mathf.Min(earliest, leftPressTime);
        if (rightPending)   earliest = Mathf.Min(earliest, rightPressTime);
        if (counterPending) earliest = Mathf.Min(earliest, counterPressTime);

        // Wait for the tolerance window to close before resolving
        if (Time.time - earliest < COMBO_TOLERANCE) return;

        // Resolve: check which keys were pressed within the window
        bool hasLeft    = leftPending    && (leftPressTime    - earliest) < COMBO_TOLERANCE;
        bool hasRight   = rightPending   && (rightPressTime   - earliest) < COMBO_TOLERANCE;
        bool hasCounter = counterPending && (counterPressTime - earliest) < COMBO_TOLERANCE;

        // Clear the consumed inputs
        if (hasLeft)    leftPending    = false;
        if (hasRight)   rightPending   = false;
        if (hasCounter) counterPending = false;

        // Fire the appropriate event.
        // During ComboRecovery, A/W/D double as counter-lane keys:
        //   A = lane 0 (left), W = lane 1 (center), D = lane 2 (right)
        if (hasLeft && hasRight && hasCounter)
        {
            OnDodgeAll?.Invoke();
        }
        else if (hasLeft)
        {
            OnDodgeLeft?.Invoke();
            OnCounter?.Invoke(0); // A = left counter lane
        }
        else if (hasRight)
        {
            OnDodgeRight?.Invoke();
            OnCounter?.Invoke(2); // D = right counter lane
        }
        else if (hasCounter)
        {
            OnCounter?.Invoke(1); // W = center counter lane
        }
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
