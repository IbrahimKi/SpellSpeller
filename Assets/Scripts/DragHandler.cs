using UnityEngine;
using UnityEngine.InputSystem;

public class DragHandler : MonoBehaviour
{
    public static DragHandler Instance { get; private set; }

    private bool _isDragging;
    private DragObject _lastDragged;

    [SerializeField] private Canvas canvas; // Referenz zum Canvas
    private RectTransform _canvasRectTransform; // RectTransform des Canvas

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
        }
    }

    private void Start()
    {
        // InputManager Events abonnieren
        InputManager.Instance.OnMousePressed += StartDrag;
        InputManager.Instance.OnMouseReleased += StopDrag;

        // Canvas-Referenz setzen
        if (canvas != null)
        {
            _canvasRectTransform = canvas.GetComponent<RectTransform>();
        }
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnMousePressed -= StartDrag;
            InputManager.Instance.OnMouseReleased -= StopDrag;
        }
    }

    private void StartDrag(Vector2 mousePosition)
    {
        if (_canvasRectTransform == null) return;

        Debug.Log($"Mouse Position: {mousePosition.x}, {mousePosition.y}");
        
        // Lokale Position im Canvas berechnen
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRectTransform, mousePosition, Camera.main, out Vector2 localPoint))
        {
            Debug.Log($"Lokale Canvas-Position: {localPoint.x}, {localPoint.y}");
            
            // Raycast ausführen
            Vector3 worldPoint = Camera.main.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, Mathf.Abs(Camera.main.transform.position.z)));
            RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);
            
            if (hit.collider != null)
            {
                Debug.Log($"Getroffenes Objekt: {hit.collider.name}");
                DragObject dragObject = hit.transform.GetComponent<DragObject>();
                if (dragObject != null)
                {
                    _lastDragged = dragObject;
                    _isDragging = true;
                    Debug.Log("Dragging gestartet.");
                }
                else
                {
                    Debug.Log("Kein DragObject Component gefunden!");
                }
            }
            else
            {
                Debug.Log("Raycast hat nichts getroffen!");
                ResetDragState(); // Setze den Zustand zurück
            }
        }
    }

    private void StopDrag(Vector2 mousePosition)
    {
        Debug.Log("Dragging beendet.");
        ResetDragState();
    }

    private void ResetDragState()
    {
        _isDragging = false;
        _lastDragged = null;
    }

    private void FixedUpdate()
    {
        if (_isDragging && _lastDragged != null)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            // Lokale Position im Canvas berechnen
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle( _canvasRectTransform, mousePosition, Camera.main, out Vector2 localPoint))
            {
                Debug.Log($"Objekt wird bewegt: {localPoint.x}, {localPoint.y}");
                _lastDragged.transform.localPosition = new Vector3(localPoint.x, localPoint.y, _lastDragged.transform.localPosition.z);
            }
        }
    }
}
