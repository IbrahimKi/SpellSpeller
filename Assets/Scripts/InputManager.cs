using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; } // Singleton-Instanz

    private PlayerControls _controls;

    // Events für Eingaben
    public delegate void MouseAction(Vector2 mousePosition);
    public event MouseAction OnMousePressed;
    public event MouseAction OnMouseReleased;

    private void Awake()
    {
        // Singleton-Setup
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Objekt bleibt zwischen Szenenwechseln bestehen
        }
        else
        {
            Destroy(gameObject); // Zusätzliche Instanzen entfernen
            return;
        }

        _controls = new PlayerControls();
    }

    private void OnEnable()
    {
        _controls.Enable();
        _controls.PlayerControls.MousePress.performed += HandleMousePress;
        _controls.PlayerControls.MouseRelease.canceled += HandleMouseRelease;
    }

    private void OnDisable()
    {
        _controls.PlayerControls.MousePress.performed -= HandleMousePress;
        _controls.PlayerControls.MouseRelease.canceled -= HandleMouseRelease;
        _controls.Disable();
    }

    private void HandleMousePress(InputAction.CallbackContext context)
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        OnMousePressed?.Invoke(mousePosition); // Event auslösen
    }

    private void HandleMouseRelease(InputAction.CallbackContext context)
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        OnMouseReleased?.Invoke(mousePosition); // Event auslösen
    }
}