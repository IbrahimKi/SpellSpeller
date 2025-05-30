using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    private PlayerControls _controls;
    private readonly object _eventLock = new object();
    private bool _isProcessingInput = false;

    // Thread-safe events
    public delegate void MouseAction(Vector2 mousePosition);
    public event MouseAction OnMousePressed;
    public event MouseAction OnMouseReleased;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _controls = new PlayerControls();
    }

    private void OnEnable()
    {
        if (_controls != null)
        {
            _controls.Enable();
            _controls.Player.MousePress.performed += HandleMousePress;
            _controls.Player.MouseRelease.canceled += HandleMouseRelease;
        }
    }

    private void OnDisable()
    {
        if (_controls != null)
        {
            _controls.Player.MousePress.performed -= HandleMousePress;
            _controls.Player.MouseRelease.canceled -= HandleMouseRelease;
            _controls.Disable();
        }
    }

    private void OnDestroy()
    {
        if (_controls != null)
        {
            _controls.Dispose();
            _controls = null;
        }
    }

    private void HandleMousePress(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        lock (_eventLock)
        {
            if (_isProcessingInput) return;
            _isProcessingInput = true;
        }

        try
        {
            Vector2 mousePosition = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            OnMousePressed?.Invoke(mousePosition);
        }
        finally
        {
            lock (_eventLock)
            {
                _isProcessingInput = false;
            }
        }
    }

    private void HandleMouseRelease(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        lock (_eventLock)
        {
            if (_isProcessingInput) return;
            _isProcessingInput = true;
        }

        try
        {
            Vector2 mousePosition = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            OnMouseReleased?.Invoke(mousePosition);
        }
        finally
        {
            lock (_eventLock)
            {
                _isProcessingInput = false;
            }
        }
    }
}