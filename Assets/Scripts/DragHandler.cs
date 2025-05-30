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
            _currentDragObject.OnDragEnd();
        ResetDragState();
    }
}