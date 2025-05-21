using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.UI;

public class DragHandler : MonoBehaviour
{
    public static DragHandler Instance { get; private set; }

    private bool _isDragging;
    private DragObject _lastDragged;
    private Vector2 _dragOffset;

    [SerializeField] private Canvas canvas; // Reference to Canvas
    private RectTransform _canvasRectTransform; // RectTransform of Canvas
    
    // For UI raycasting
    [SerializeField] private Camera uiCamera; // Assign your UI camera here
    private GraphicRaycaster _graphicRaycaster;
    
    // Performance optimizations
    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(4);
    private PointerEventData _pointerEventData;
    private Vector3 _worldPoint;

    // Debug mode flag - set to false in production
    [SerializeField] private bool _debugMode = false;

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
        
        _pointerEventData = new PointerEventData(EventSystem.current);
    }

    private void Start()
    {
        // Subscribe to InputManager events
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnMousePressed += StartDrag;
            InputManager.Instance.OnMouseReleased += StopDrag;
        }
        else
        {
            LogWarning("InputManager instance not found!");
        }

        // Set Canvas reference
        if (canvas != null)
        {
            _canvasRectTransform = canvas.GetComponent<RectTransform>();
            _graphicRaycaster = canvas.GetComponent<GraphicRaycaster>();
            
            // If no UI camera assigned, try to use main camera
            if (uiCamera == null)
            {
                uiCamera = Camera.main;
            }
        }
        else
        {
            LogWarning("Canvas reference not assigned!");
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
        if (_canvasRectTransform == null || _graphicRaycaster == null) return;

        LogDebug($"Mouse Position: {mousePosition.x}, {mousePosition.y}");
        
        // First, try UI raycasting since we're working with Canvas elements
        _raycastResults.Clear();
        _pointerEventData.position = mousePosition;
        _graphicRaycaster.Raycast(_pointerEventData, _raycastResults);
        
        // Check UI hits first
        foreach (var result in _raycastResults)
        {
            DragObject dragObject = result.gameObject.GetComponent<DragObject>();
            if (dragObject != null && dragObject.IsDraggable)
            {
                _lastDragged = dragObject;
                _isDragging = true;
                
                // Calculate drag offset so the object doesn't jump to cursor position
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRectTransform, mousePosition, uiCamera, out Vector2 localPoint);
                Vector3 worldPos = _canvasRectTransform.TransformPoint(localPoint);
                _dragOffset = (Vector2)_lastDragged.transform.position - (Vector2)worldPos;
                
                _lastDragged.OnDragStart();
                LogDebug($"Dragging started on {result.gameObject.name} (UI)");
                return; // Found a draggable UI object, exit
            }
        }
        
        // If no UI hit, try physics raycast as fallback only if needed
        if (TryPhysicsRaycast(mousePosition)) return;
        
        // No draggable object found
        ResetDragState();
    }
    
    private bool TryPhysicsRaycast(Vector2 mousePosition)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRectTransform, mousePosition, uiCamera, out Vector2 localPoint))
            return false;
            
        LogDebug($"Local Canvas Position: {localPoint.x}, {localPoint.y}");
        
        // Perform Raycast
        _worldPoint = uiCamera.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, Mathf.Abs(uiCamera.transform.position.z)));
        RaycastHit2D hit = Physics2D.Raycast(_worldPoint, Vector2.zero);
        
        if (hit.collider != null)
        {
            LogDebug($"Hit Object: {hit.collider.name}");
            DragObject dragObject = hit.transform.GetComponent<DragObject>();
            if (dragObject != null && dragObject.IsDraggable)
            {
                _lastDragged = dragObject;
                _isDragging = true;
                
                // Calculate drag offset
                Vector3 worldPos = _canvasRectTransform.TransformPoint(localPoint);
                _dragOffset = (Vector2)_lastDragged.transform.position - (Vector2)worldPos;
                
                _lastDragged.OnDragStart();
                LogDebug("Dragging started (Physics)");
                return true;
            }
        }
        
        return false;
    }

    private void StopDrag(Vector2 mousePosition)
    {
        LogDebug("Dragging ended");
        
        if (_lastDragged != null)
        {
            _lastDragged.OnDragEnd();
        }
        
        ResetDragState();
    }

    private void ResetDragState()
    {
        _isDragging = false;
        _lastDragged = null;
    }

    private void Update()
    {
        if (!_isDragging || _lastDragged == null) return;
        
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        // Calculate local position in Canvas - only do this calculation if actually dragging
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRectTransform, mousePosition, uiCamera, out Vector2 localPoint))
        {
            Vector3 targetPosition = _canvasRectTransform.TransformPoint(localPoint);
            _lastDragged.transform.position = new Vector3(targetPosition.x + _dragOffset.x, targetPosition.y + _dragOffset.y, _lastDragged.transform.position.z);
        }
    }
    
    // Conditional logging methods to avoid string concatenation overhead when debug is disabled
    private void LogDebug(string message)
    {
        if (_debugMode)
        {
            Debug.Log($"[DragHandler] {message}");
        }
    }
    
    private void LogWarning(string message)
    {
        Debug.LogWarning($"[DragHandler] {message}");
    }
}