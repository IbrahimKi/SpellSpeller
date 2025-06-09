using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : SingletonBehaviour<InputManager>, IGameManager
{
    [Header("Input Mode")]
    [SerializeField] private bool enableNonUIInput = false; // Für Performance deaktiviert
    
    private PlayerControls _controls;
    
    // Events für Non-UI Mouse Actions (optional)
    public delegate void MouseAction(Vector2 mousePosition);
    public event MouseAction OnMousePressed;
    public event MouseAction OnMouseReleased;
    
    private bool _isReady = false;
    public bool IsReady => _isReady;
    
    // Cache mouse position für Performance
    private Vector2 _cachedMousePosition;
    private float _lastMousePositionUpdate;
    private const float MOUSE_CACHE_TIME = 0.016f;

    protected override void OnAwakeInitialize()
    {
        InitializeControls();
        _isReady = true;
    }

    private void InitializeControls()
    {
        _controls = new PlayerControls();
        
        // NUR aktivieren wenn Non-UI Input benötigt wird
        if (enableNonUIInput)
        {
            _controls.Player.MousePress.started += OnMousePressStarted;
            _controls.Player.MousePress.canceled += OnMousePressCanceled;
            Debug.Log("[InputManager] Non-UI input enabled");
        }
        else
        {
            Debug.Log("[InputManager] UI-Only mode - Non-UI input disabled");
        }
    }
    
    private void OnMousePressStarted(InputAction.CallbackContext context)
    {
        if (!enableNonUIInput) return;
        
        // Delay UI check to next frame für Input System compatibility
        StartCoroutine(CheckUIAndTriggerMousePress());
    }
    
    private void OnMousePressCanceled(InputAction.CallbackContext context)
    {
        if (!enableNonUIInput) return;
        
        // Delay UI check to next frame für Input System compatibility
        StartCoroutine(CheckUIAndTriggerMouseRelease());
    }

    // FIXED: Async UI-Overlap Detection für Input System
    private System.Collections.IEnumerator CheckUIAndTriggerMousePress()
    {
        yield return null; // Wait one frame
        
        if (!IsPointerOverUI())
        {
            Vector2 mousePos = GetMousePosition();
            OnMousePressed?.Invoke(mousePos);
        }
    }
    
    private System.Collections.IEnumerator CheckUIAndTriggerMouseRelease()
    {
        yield return null; // Wait one frame
        
        if (!IsPointerOverUI())
        {
            Vector2 mousePos = GetMousePosition();
            OnMouseReleased?.Invoke(mousePos);
        }
    }

    // UI-Overlap Detection (now called from Coroutine)
    private bool IsPointerOverUI()
    {
        return UnityEngine.EventSystems.EventSystem.current?.IsPointerOverGameObject() ?? false;
    }

    private void OnEnable()
    {
        _controls?.Enable();
    }

    private void OnDisable()
    {
        _controls?.Disable();
    }

    protected override void OnDestroy()
    {
        if (_controls != null)
        {
            if (enableNonUIInput)
            {
                _controls.Player.MousePress.started -= OnMousePressStarted;
                _controls.Player.MousePress.canceled -= OnMousePressCanceled;
            }
            _controls.Dispose();
        }
        base.OnDestroy();
    }

    public Vector2 GetMousePosition()
    {
        if (Time.unscaledTime - _lastMousePositionUpdate > MOUSE_CACHE_TIME)
        {
            _cachedMousePosition = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            _lastMousePositionUpdate = Time.unscaledTime;
        }
        return _cachedMousePosition;
    }

    public bool IsInputSystemWorking()
    {
        return _controls != null && 
               _controls.Player.enabled && 
               Mouse.current != null;
    }
    
    // Runtime Toggle für Non-UI Input
    public void SetNonUIInputEnabled(bool enabled)
    {
        if (enableNonUIInput == enabled) return;
        
        enableNonUIInput = enabled;
        
        if (_controls != null)
        {
            if (enabled)
            {
                _controls.Player.MousePress.started += OnMousePressStarted;
                _controls.Player.MousePress.canceled += OnMousePressCanceled;
                Debug.Log("[InputManager] Non-UI input enabled");
            }
            else
            {
                _controls.Player.MousePress.started -= OnMousePressStarted;
                _controls.Player.MousePress.canceled -= OnMousePressCanceled;
                Debug.Log("[InputManager] Non-UI input disabled");
            }
        }
    }
    
#if UNITY_EDITOR
    [ContextMenu("Toggle Non-UI Input")]
    public void ToggleNonUIInput()
    {
        SetNonUIInputEnabled(!enableNonUIInput);
    }
#endif
}