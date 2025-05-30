using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    private PlayerControls _controls;

    // Events für Mouse Actions
    public delegate void MouseAction(Vector2 mousePosition);
    public event MouseAction OnMousePressed;
    public event MouseAction OnMouseReleased;

    private void Awake()
    {
        // Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeControls();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeControls()
    {
        _controls = new PlayerControls();
        
        // FIXED: Mouse Press Event (performed = gedrückt)
        _controls.Player.MousePress.performed += ctx => 
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            OnMousePressed?.Invoke(mousePos);
        };
        
        // FIXED: Mouse Release Event (performed statt canceled!)
        _controls.Player.MouseRelease.performed += ctx => 
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            OnMouseReleased?.Invoke(mousePos);
        };
    }

    private void OnEnable()
    {
        _controls?.Enable();
    }

    private void OnDisable()
    {
        _controls?.Disable();
    }

    private void OnDestroy()
    {
        _controls?.Dispose();
    }

    // Utility Methods
    public Vector2 GetMousePosition()
    {
        return Mouse.current.position.ReadValue();
    }

    public bool IsMousePressed()
    {
        return Mouse.current.leftButton.isPressed;
    }
}