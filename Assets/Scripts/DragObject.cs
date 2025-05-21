using UnityEngine;

public class DragObject : MonoBehaviour
{
    [SerializeField] private bool isDraggable = true;
    
    // Cached transform component for better performance
    private RectTransform _rectTransform;
    
    // Original sorting order/layer for returning after drag ends
    private int _originalSortingOrder;
    private int _dragSortingOrderBonus = 10; // How much to increase when dragging
    
    // Optional - sorting layer or canvas sorting order component
    private Canvas _canvas;
    
    public bool IsDraggable => isDraggable;
    
    private void Awake()
    {
        // Cache components for performance
        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponent<Canvas>();
        
        if (_canvas != null)
        {
            _originalSortingOrder = _canvas.sortingOrder;
        }
    }
    
    public void OnDragStart()
    {
        // Bring the card to front
        if (_canvas != null)
        {
            _canvas.sortingOrder = _originalSortingOrder + _dragSortingOrderBonus;
        }
        
        // You could add more effects here:
        // - Scale up slightly
        // - Play sound
        // - Show glow effect
    }
    
    public void OnDragEnd()
    {
        // Return to original sorting order
        if (_canvas != null)
        {
            _canvas.sortingOrder = _originalSortingOrder;
        }
        
        // Other possible actions:
        // - Snap to grid
        // - Validate move
        // - Play drop sound
    }
}