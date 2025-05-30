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
            canvas = FindObjectsByType<Canvas>(FindObjectsSortMode.None)[0];
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
            DragObject dragObject = result.gameObject.GetComponentInParent<DragObject>();
            if (dragObject != null)
                return dragObject;
        }
        
        return TryPhysicsRaycast(mousePosition);
    }

    private DragObject TryPhysicsRaycast(Vector2 mousePosition)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRectTransform, mousePosition, uiCamera, out Vector2 localPoint))
            return null;
            
        Vector3 worldPoint = uiCamera.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, Mathf.Abs(uiCamera.transform.position.z)));
        RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);
        
        return hit.collider?.GetComponent<DragObject>();
    }

    private void StartDragging(DragObject dragObject, Vector2 mousePosition)
    {
        _currentDragObject = dragObject;
        _isDragging = true;
        
        // Store original transform for return animation
        RectTransform dragRect = _currentDragObject.GetComponent<RectTransform>();
        _originalPosition = dragRect.localPosition;
        _originalRotation = dragRect.localRotation;
        _originalScale = dragRect.localScale;
        
        // Notify HandLayoutManager to pause updates for this card
        if (HandLayoutManager.Instance != null)
            HandLayoutManager.Instance.SetCardDragging(_currentDragObject, true);
        
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
        
        // Return card to original position
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
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t);
            
            dragRect.localPosition = Vector3.Lerp(startPos, _originalPosition, t);
            dragRect.localRotation = Quaternion.Lerp(startRot, _originalRotation, t);
            dragRect.localScale = Vector3.Lerp(startScale, _originalScale, t);
            
            yield return null;
        }
        
        if (dragRect != null)
        {
            dragRect.localPosition = _originalPosition;
            dragRect.localRotation = _originalRotation;
            dragRect.localScale = _originalScale;
        }
        
        // Re-enable layout updates for this card
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
        
        UpdateDragPosition();
    }

    private void UpdateDragPosition()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRectTransform, mousePosition, uiCamera, out Vector2 localPoint))
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
            
            // Immediately return to position without animation
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