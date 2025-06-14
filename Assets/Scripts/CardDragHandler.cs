using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Drag Settings")]
    [SerializeField] private float dragScaleMultiplier = 1.1f;
    [SerializeField] private int dragSortOrder = 100;
    [SerializeField] private float snapBackDuration = 0.2f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // Cached components
    private Card _card;
    private RectTransform _cardRectTransform;
    private Canvas _parentCanvas;
    private CanvasGroup _canvasGroup;
    private GraphicRaycaster _raycaster;
    
    // Drag state
    private Transform _originalParent;
    private Vector2 _originalAnchoredPosition;
    private Vector3 _originalScale;
    private bool _isDragging;
    private Canvas _tempCanvas;
    
    // Events
    public static event System.Action<Card> OnCardDragStart;
    public static event System.Action<Card> OnCardDragEnd;
    public static event System.Action<Card, GameObject> OnCardDropped;
    
    private void Awake()
    {
        CacheComponents();
    }
    
    private void CacheComponents()
    {
        _card = GetComponent<Card>();
        if (_card == null)
        {
            Debug.LogError($"[CardDragHandler] No Card component found on {gameObject.name}!");
            enabled = false;
            return;
        }
        
        _cardRectTransform = GetComponent<RectTransform>();
        
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (enableDebugLogs) Debug.Log($"[CardDragHandler] Added CanvasGroup to {gameObject.name}");
        }
        
        // Don't search for Canvas in Awake - wait until the card is properly parented
        if (enableDebugLogs)
        {
            Debug.Log($"[CardDragHandler] Components cached for {gameObject.name}");
        }
    }
    
    // NEW: Find Canvas when actually needed (lazy initialization)
    private bool EnsureCanvasReference()
    {
        if (_parentCanvas != null) return true;
        
        // Find the root Canvas
        _parentCanvas = GetComponentInParent<Canvas>();
        while (_parentCanvas != null && !_parentCanvas.isRootCanvas)
        {
            _parentCanvas = _parentCanvas.GetComponentInParent<Canvas>();
        }
        
        if (_parentCanvas == null)
        {
            if (enableDebugLogs) Debug.LogWarning($"[CardDragHandler] No parent Canvas found for {gameObject.name}");
            return false;
        }
        
        _raycaster = _parentCanvas.GetComponent<GraphicRaycaster>();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[CardDragHandler] Found Canvas: {_parentCanvas.name}");
        }
        
        return true;
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        // Ensure we have a canvas reference before starting drag
        if (!EnsureCanvasReference())
        {
            Debug.LogError($"[CardDragHandler] Cannot drag - no Canvas found!");
            return;
        }
        
        if (!CanStartDrag(eventData)) return;
        
        _isDragging = true;
        
        // Store original state
        _originalParent = _cardRectTransform.parent;
        _originalAnchoredPosition = _cardRectTransform.anchoredPosition;
        _originalScale = _cardRectTransform.localScale;
        
        // Setup for dragging
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 0.8f;
        
        // Create temporary Canvas for proper sorting
        _tempCanvas = gameObject.AddComponent<Canvas>();
        _tempCanvas.overrideSorting = true;
        _tempCanvas.sortingOrder = dragSortOrder;
        
        // Scale the card
        _cardRectTransform.localScale = _originalScale * dragScaleMultiplier;
        
        OnCardDragStart?.Invoke(_card);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[CardDragHandler] Drag started on {_card.name}");
            Debug.Log($"  Original Position: {_originalAnchoredPosition}");
            Debug.Log($"  Parent Canvas: {_parentCanvas.name}");
        }
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging || _parentCanvas == null) return;
        
        // Convert screen position to local position in parent Canvas
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parentCanvas.transform as RectTransform,
            eventData.position,
            _parentCanvas.worldCamera,
            out localPoint))
        {
            _cardRectTransform.anchoredPosition = localPoint;
        }
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;
        
        _isDragging = false;
        
        // Check drop target
        GameObject dropTarget = GetDropTarget(eventData);
        
        if (dropTarget != null)
        {
            HandleDrop(dropTarget);
            OnCardDropped?.Invoke(_card, dropTarget);
        }
        else
        {
            SnapBack();
        }
        
        // Cleanup
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.alpha = 1f;
        
        if (_tempCanvas != null)
        {
            Destroy(_tempCanvas);
            _tempCanvas = null;
        }
        
        OnCardDragEnd?.Invoke(_card);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[CardDragHandler] Drag ended on {_card.name}");
            if (dropTarget != null)
                Debug.Log($"  Dropped on: {dropTarget.name}");
            else
                Debug.Log("  No valid drop target");
        }
    }
    
    private bool CanStartDrag(PointerEventData eventData)
    {
        if (_card == null || !_card.IsInteractable)
        {
            if (enableDebugLogs) Debug.Log("[CardDragHandler] Cannot drag: Card not interactable");
            return false;
        }
        
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            if (enableDebugLogs) Debug.Log("[CardDragHandler] Cannot drag: Not left mouse button");
            return false;
        }
        
        // Canvas will be found lazily in OnBeginDrag
        return true;
    }
    
    private GameObject GetDropTarget(PointerEventData eventData)
    {
        if (_raycaster == null) return null;
        
        var results = new System.Collections.Generic.List<RaycastResult>();
        _raycaster.Raycast(eventData, results);
        
        foreach (var result in results)
        {
            if (result.gameObject != gameObject)
            {
                // Check for tagged drop areas
                if (result.gameObject.CompareTag("PlayArea") || 
                    result.gameObject.CompareTag("DiscardArea"))
                {
                    return result.gameObject;
                }
                
                // Check for DropAreaHandler component
                var dropArea = result.gameObject.GetComponent<DropAreaHandler>();
                if (dropArea != null)
                {
                    return result.gameObject;
                }
            }
        }
        
        return null;
    }
    
    private void HandleDrop(GameObject dropTarget)
    {
        if (!_card.IsSelected) _card.Select();
        
        switch (dropTarget.tag)
        {
            case "PlayArea":
                HandlePlayAreaDrop();
                break;
                
            case "DiscardArea":
                HandleDiscardAreaDrop();
                break;
                
            default:
                // Try DropAreaHandler
                var dropAreaHandler = dropTarget.GetComponent<DropAreaHandler>();
                if (dropAreaHandler != null)
                {
                    // Let DropAreaHandler determine the action
                    if (dropTarget.CompareTag("PlayArea"))
                        HandlePlayAreaDrop();
                    else if (dropTarget.CompareTag("DiscardArea"))
                        HandleDiscardAreaDrop();
                    else
                        SnapBack();
                }
                else
                {
                    SnapBack();
                }
                break;
        }
    }
    
    private void HandlePlayAreaDrop()
    {
        if (SpellcastManager.HasInstance)
        {
            SpellcastManager.Instance.TryPlayCards(new System.Collections.Generic.List<Card> { _card });
            if (enableDebugLogs) Debug.Log($"[CardDragHandler] Card {_card.name} played");
        }
        else
        {
            Debug.LogWarning("[CardDragHandler] SpellcastManager not available for play");
            SnapBack();
        }
    }
    
    private void HandleDiscardAreaDrop()
    {
        if (!CombatManager.HasInstance || !CombatManager.Instance.CanSpendCreativity(1))
        {
            if (enableDebugLogs) Debug.Log("[CardDragHandler] Cannot discard: Not enough creativity");
            SnapBack();
            return;
        }
        
        // Spend creativity
        CombatManager.Instance.SpendCreativity(1);
        
        // Add to discard pile
        if (DeckManager.HasInstance && _card.CardData != null)
        {
            DeckManager.Instance.DiscardCard(_card.CardData);
        }
        
        // Remove from hand and destroy
        if (CardManager.HasInstance)
        {
            CardManager.Instance.DiscardCard(_card);
        }
        
        // Draw replacement card
        if (DeckManager.HasInstance && !DeckManager.Instance.IsDeckEmpty)
        {
            var newCard = DeckManager.Instance.DrawCard();
            if (newCard != null && CardManager.HasInstance)
            {
                CardManager.Instance.SpawnCard(newCard, null, true);
            }
        }
        
        if (enableDebugLogs) Debug.Log($"[CardDragHandler] Card {_card.name} discarded");
    }
    
    private void SnapBack()
    {
        // Ensure we're in the original parent
        if (_cardRectTransform.parent != _originalParent)
            _cardRectTransform.SetParent(_originalParent, false);
            
        StartCoroutine(AnimateSnapBack());
        
        if (enableDebugLogs) Debug.Log($"[CardDragHandler] Snapping {_card.name} back to original position");
    }
    
    private System.Collections.IEnumerator AnimateSnapBack()
    {
        float elapsed = 0f;
        Vector2 startPos = _cardRectTransform.anchoredPosition;
        Vector3 startScale = _cardRectTransform.localScale;
        
        while (elapsed < snapBackDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / snapBackDuration;
            
            _cardRectTransform.anchoredPosition = Vector2.Lerp(startPos, _originalAnchoredPosition, t);
            _cardRectTransform.localScale = Vector3.Lerp(startScale, _originalScale, t);
            
            yield return null;
        }
        
        // Ensure final position is exact
        _cardRectTransform.anchoredPosition = _originalAnchoredPosition;
        _cardRectTransform.localScale = _originalScale;
    }
    
    // Public property for external checks - updated to use lazy canvas finding
    public bool CanDrag => !_isDragging && _card != null && _card.IsInteractable;
    
    // Debug method
    [ContextMenu("Test Drag Setup")]
    public void TestDragSetup()
    {
        Debug.Log("=== CARD DRAG SETUP TEST ===");
        Debug.Log($"Card: {(_card != null ? _card.name : "NULL")}");
        Debug.Log($"RectTransform: {(_cardRectTransform != null ? "OK" : "NULL")}");
        
        // Try to find canvas
        EnsureCanvasReference();
        Debug.Log($"Parent Canvas: {(_parentCanvas != null ? _parentCanvas.name : "NULL")}");
        Debug.Log($"CanvasGroup: {(_canvasGroup != null ? "OK" : "NULL")}");
        Debug.Log($"Raycaster: {(_raycaster != null ? "OK" : "NULL")}");
        Debug.Log($"Can Drag: {CanDrag}");
        
        if (_parentCanvas != null)
        {
            Debug.Log($"Canvas Render Mode: {_parentCanvas.renderMode}");
            Debug.Log($"Canvas is Root: {_parentCanvas.isRootCanvas}");
        }
    }
}