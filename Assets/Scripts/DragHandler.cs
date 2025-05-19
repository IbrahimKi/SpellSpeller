using UnityEngine;
using UnityEngine.InputSystem;

public class DragHandler : MonoBehaviour
{
    public static DragHandler Instance { get; private set; }

    private bool isDragging = false;
    private DragObject lastDragged;

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
    }

    private void Start()
    {
        // Abonniere Events vom InputManager-Singleton
        InputManager.Instance.OnMousePressed += StartDrag;
        InputManager.Instance.OnMouseReleased += StopDrag;
    }

    private void OnDestroy()
    {
        // Events abmelden, falls das Objekt zerstört wird
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnMousePressed -= StartDrag;
            InputManager.Instance.OnMouseReleased -= StopDrag;
        }
    }

    private void StartDrag(Vector2 mousePosition)
    {
        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, Camera.main.nearClipPlane));
        RaycastHit2D hit = Physics2D.Raycast(worldPosition, Vector2.zero);

        if (hit.collider != null)
        {
            DragObject dragObject = hit.transform.GetComponent<DragObject>();
            if (dragObject != null)
            {
                lastDragged = dragObject;
                isDragging = true;
            }
        }
    }

    private void StopDrag(Vector2 mousePosition)
    {
        isDragging = false;
        lastDragged = null;
    }

    private void Update()
    {
        if (isDragging && lastDragged != null)
        {
            Vector3 mousePosition = Mouse.current.position.ReadValue();
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, Camera.main.nearClipPlane));
            lastDragged.transform.position = new Vector3(worldPosition.x, worldPosition.y, 0);
        }
    }
}
