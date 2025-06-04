using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : SingletonBehaviour<InputManager>, IGameManager
{
    private PlayerControls _controls;
    private bool _isMousePressed = false;

    // Events für Mouse Actions
    public delegate void MouseAction(Vector2 mousePosition);
    public event MouseAction OnMousePressed;
    public event MouseAction OnMouseReleased;
    
    private bool _isReady = false;
    public bool IsReady => _isReady;
    
    // PERFORMANCE: Cache mouse position
    private Vector2 _cachedMousePosition;
    private float _lastMousePositionUpdate;
    private const float MOUSE_CACHE_TIME = 0.016f; // ~60fps

    protected override void OnAwakeInitialize()
    {
        InitializeControls();
        _isReady = true;
    }

    private void InitializeControls()
    {
        _controls = new PlayerControls();
        
        // FIXED: Verwende nur ein Mouse-Action mit started/canceled
        _controls.Player.MousePress.started += OnMousePressStarted;
        _controls.Player.MousePress.canceled += OnMousePressCanceled;
        
        // ENTFERNE MouseRelease komplett - wird durch canceled gehandelt
    }
    
    private void OnMousePressStarted(InputAction.CallbackContext context)
    {
        if (_isMousePressed) return; // Verhindere doppelte Events
        
        _isMousePressed = true;
        Vector2 mousePos = GetMousePosition();
        Debug.Log($"Mouse press STARTED at: {mousePos}");
        OnMousePressed?.Invoke(mousePos);
    }
    
    private void OnMousePressCanceled(InputAction.CallbackContext context)
    {
        if (!_isMousePressed) return; // Verhindere doppelte Events
        
        _isMousePressed = false;
        Vector2 mousePos = GetMousePosition();
        Debug.Log($"Mouse press CANCELED at: {mousePos}");
        OnMouseReleased?.Invoke(mousePos);
    }

    private void OnEnable()
    {
        _controls?.Enable();
        Debug.Log("InputManager enabled");
    }

    private void OnDisable()
    {
        _controls?.Disable();
        Debug.Log("InputManager disabled");
    }

    protected override void OnDestroy()
    {
        if (_controls != null)
        {
            _controls.Player.MousePress.started -= OnMousePressStarted;
            _controls.Player.MousePress.canceled -= OnMousePressCanceled;
            _controls.Dispose();
        }
        base.OnDestroy();
    }

    // PERFORMANCE: Cached mouse position getter
    public Vector2 GetMousePosition()
    {
        // Cache mouse position to avoid multiple reads per frame
        if (Time.unscaledTime - _lastMousePositionUpdate > MOUSE_CACHE_TIME)
        {
            _cachedMousePosition = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            _lastMousePositionUpdate = Time.unscaledTime;
        }
        return _cachedMousePosition;
    }

    public bool IsMousePressed()
    {
        return _isMousePressed && (Mouse.current?.leftButton.isPressed ?? false);
    }
    
    // NEUE METHODE: Direkter Status-Check ohne Cache
    public bool IsMousePressedDirect()
    {
        return Mouse.current?.leftButton.isPressed ?? false;
    }
    
    // UTILITY: Check if input system is working correctly
    public bool IsInputSystemWorking()
    {
        return _controls != null && 
               _controls.Player.enabled && 
               Mouse.current != null;
    }
    
    // NEUE METHODE: Force mouse state reset (für Debug-Zwecke)
    public void ForceResetMouseState()
    {
        bool wasPressed = _isMousePressed;
        _isMousePressed = IsMousePressedDirect();
        
        if (wasPressed && !_isMousePressed)
        {
            Debug.Log("Force releasing mouse - state was desynchronized");
            OnMouseReleased?.Invoke(GetMousePosition());
        }
    }
    
    // DEBUG UTILITIES
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void Update()
    {
        // Debug: Check for state desync
        bool actualPressed = IsMousePressedDirect();
        if (_isMousePressed != actualPressed)
        {
            Debug.LogWarning($"Mouse state desync detected! Tracked: {_isMousePressed}, Actual: {actualPressed}");
            // Auto-correct in editor
            if (Application.isEditor)
            {
                ForceResetMouseState();
            }
        }
    }
    
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [ContextMenu("Test Input System")]
    public void TestInputSystem()
    {
        Debug.Log($"Input System Status:");
        Debug.Log($"- Controls exist: {_controls != null}");
        Debug.Log($"- Player enabled: {_controls?.Player.enabled}");
        Debug.Log($"- Mouse available: {Mouse.current != null}");
        Debug.Log($"- Mouse position: {GetMousePosition()}");
        Debug.Log($"- Mouse pressed (tracked): {_isMousePressed}");
        Debug.Log($"- Mouse pressed (direct): {IsMousePressedDirect()}");
        Debug.Log($"- Subscribers to OnMousePressed: {OnMousePressed?.GetInvocationList()?.Length ?? 0}");
        Debug.Log($"- Subscribers to OnMouseReleased: {OnMouseReleased?.GetInvocationList()?.Length ?? 0}");
    }
}