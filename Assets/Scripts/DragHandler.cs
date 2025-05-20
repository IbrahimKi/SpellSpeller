using UnityEngine;
using UnityEngine.InputSystem;

public class DragHandler : MonoBehaviour
{
    public static DragHandler Instance { get; private set; }

    private bool _isDragging;
    private DragObject _lastDragged;

    [SerializeField] private Canvas canvas; // Referenz zum Canvas
    private RectTransform canvasRectTransform; // RectTransform des Canvas

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
        // Abonniere die InputManager Events
        InputManager.Instance.OnMousePressed += StartDrag;
        InputManager.Instance.OnMouseReleased += StopDrag;

        // Hole das RectTransform des Canvas
        if (canvas != null)
        {
            canvasRectTransform = canvas.GetComponent<RectTransform>();
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
        if (canvasRectTransform == null) return;

        Debug.Log($"Mouse Position: {mousePosition.x}, {mousePosition.y}"); 
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, mousePosition, Camera.main, out Vector2 localPoint))
        {
            Debug.Log($"Lokale Canvas-Position: {localPoint.x}, {localPoint.y}");
            RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(mousePosition), Vector2.zero);
            if (hit.collider != null)
            {
                DragObject dragObject = hit.transform.GetComponent<DragObject>();
                if (dragObject != null)
                {
                    _lastDragged = dragObject;
                    _isDragging = true;
                }
            }
        }
    }

    private void StopDrag(Vector2 mousePosition)
    {
        Debug.Log("Dragging Ended");
        _isDragging = false;
        _lastDragged = null;
    }

    private void FixedUpdate()
    {
        if (_isDragging && _lastDragged != null)
        {
            
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, mousePosition, Camera.main, out Vector2 localPoint))
            {
                _lastDragged.transform.localPosition = new Vector3(localPoint.x, localPoint.y, _lastDragged.transform.localPosition.z);
                Debug.Log($"Objekt bewegt: {localPoint.x}, {localPoint.y}");
            }
        }
    }
}
