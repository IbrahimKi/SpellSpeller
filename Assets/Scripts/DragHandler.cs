using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.UI;

public class DragHandler : MonoBehaviour
{
    public static DragHandler Instance { get; private set; }

    private bool _isDragging;
    private DragObject _currentDragObject;
    private Vector2 _dragOffset;
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private Vector3 _originalScale;

    [SerializeField] private Canvas canvas;
    private RectTransform _canvasRectTransform;
    
    [SerializeField] private Camera uiCamera;
    private GraphicRaycaster _graphicRaycaster;
    
    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(4);
    private PointerEventData _pointerEventData;
    
    // OPTIMIZED: Cache mouse position to avoid multiple reads per frame
    private Vector2 _cachedMousePosition;

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
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnMousePressed += StartDrag;
            InputManager.Instance.OnMouseReleased += StopDrag;
        }

        SetupCanvas();
    }

    private void SetupCanvas()
    {
        if (canvas != null)
        {
            _canvasRectTransform = canvas.GetComponent<RectTransform>();
            _graphicRaycaster = canvas.GetComponent<GraphicRaycaster>();
            
            if (uiCamera == null)
                uiCamera = Camera.main;
        }
        else
        {
            // OPTIMIZED: Use FindFirstObjectByType instead of FindObjectsByType for single object
            canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
                SetupCanvas();
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
        if (_isDragging) return;
        if (_canvasRectTransform == null || _graphicRaycaster == null) return;

        DragObject foundDragObject = FindDragObjectAtPosition(mousePosition);
        
        if (foundDragObject != null && foundDragObject.IsDraggable)
            StartDragging(foundDragObject, mousePosition);
    }

    private DragObject FindDragObjectAtPosition(Vector2 mousePosition)
    {
        _raycastResults.Clear();
        _pointerEventData.position = mousePosition;
        
        _graphicRaycaster.Raycast(_pointerEventData, _raycastResults);
        
        foreach (var result in _raycastResults)
        {
            // OPTIMIZED: Try direct component first, then parent lookup
            DragObject dragObject = result.gameObject.GetComponent<DragObject>();
            if (dragObject != null) return dragObject;
            
            dragObject = result.gameObject.GetComponentInParent<DragObject>();
            if (dragObject != null) return dragObject;
        }
        
        return null; // REMOVED: Physics2D fallback (not needed for UI cards)
    }

    private void StartDragging(DragObject dragObject, Vector2 mousePosition)
    {
        _currentDragObject = dragObject;
        _isDragging = true;
        
        // Store original transform
        RectTransform dragRect = _currentDragObject.GetComponent<RectTransform>();
        _originalPosition = dragRect.localPosition;
        _originalRotation = dragRect.localRotation;
        _originalScale = dragRect.localScale;
        
        // Notify systems
        if (HandLayoutManager.Instance != null)
            HandLayoutManager.Instance.SetCardDragging(_currentDragObject, true);
        
        // Calculate drag offset
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRectTransform, mousePosition, uiCamera, out Vector2 localPoint))
        {
            Vector3 worldPos = _canvasRectTransform.TransformPoint(localPoint);
            _dragOffset = (Vector2)_currentDragObject.transform.position - (Vector2)worldPos;
        }
        else
        {
            _dragOffset = Vector2.zero;
        }
        
        _currentDragObject.OnDragStart();
    }

    private void StopDrag(Vector2 mousePosition)
    {
        if (!_isDragging || _currentDragObject == null) return;
        
        _currentDragObject.OnDragEnd();
        StartCoroutine(ReturnToOriginalPosition());
    }

    private System.Collections.IEnumerator ReturnToOriginalPosition()
    {
        if (_currentDragObject == null) yield break;
        
        RectTransform dragRect = _currentDragObject.GetComponent<RectTransform>();
        Vector3 startPos = dragRect.localPosition;
        Quaternion startRot = dragRect.localRotation;
        Vector3 startScale = dragRect.localScale;
        
        float duration = 0.3f;
        float elapsed = 0f;
        
        while (elapsed < duration && dragRect != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            
            dragRect.localPosition = Vector3.Lerp(startPos, _originalPosition, t);
            dragRect.localRotation = Quaternion.Lerp(startRot, _originalRotation, t);
            dragRect.localScale = Vector3.Lerp(startScale, _originalScale, t);
            
            yield return null;
        }
        
        // Ensure final position
        if (dragRect != null)
        {
            dragRect.localPosition = _originalPosition;
            dragRect.localRotation = _originalRotation;
            dragRect.localScale = _originalScale;
        }
        
        // Re-enable layout updates
        if (HandLayoutManager.Instance != null)
            HandLayoutManager.Instance.SetCardDragging(_currentDragObject, false);
        
        ResetDragState();
    }

    private void ResetDragState()
    {
        _isDragging = false;
        _currentDragObject = null;
        _dragOffset = Vector2.zero;
    }

    private void Update()
    {
        if (!_isDragging || _currentDragObject == null) return;
        
        // OPTIMIZED: Cache mouse position once per frame
        _cachedMousePosition = Mouse.current.position.ReadValue();
        UpdateDragPosition();
    }

    private void UpdateDragPosition()
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRectTransform, _cachedMousePosition, uiCamera, out Vector2 localPoint))
        {
            Vector3 targetPosition = _canvasRectTransform.TransformPoint(localPoint);
            _currentDragObject.transform.position = new Vector3(
                targetPosition.x + _dragOffset.x, 
                targetPosition.y + _dragOffset.y, 
                _currentDragObject.transform.position.z
            );
        }
    }
    
    public bool IsDragging => _isDragging;
    public DragObject CurrentDragObject => _currentDragObject;

    public void ForceStopDragging()
    {
        if (_isDragging && _currentDragObject != null)
        {
            _currentDragObject.OnDragEnd();
            
            RectTransform dragRect = _currentDragObject.GetComponent<RectTransform>();
            if (dragRect != null)
            {
                dragRect.localPosition = _originalPosition;
                dragRect.localRotation = _originalRotation;
                dragRect.localScale = _originalScale;
            }
            
            if (HandLayoutManager.Instance != null)
                HandLayoutManager.Instance.SetCardDragging(_currentDragObject, false);
        }
        ResetDragState();
    }
}