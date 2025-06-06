using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Drag Settings")]
    [SerializeField] private float dragScaleMultiplier = 1.1f;
    [SerializeField] private int dragSortOrder = 100;
    [SerializeField] private float snapBackDuration = 0.2f;
    
    // Cached components
    private Card _card;
    private RectTransform _rectTransform;
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private GraphicRaycaster _raycaster;
    
    // Drag state
    private Transform _originalParent;
    private Vector3 _originalPosition;
    private Vector3 _originalScale;
    private int _originalSortOrder;
    private bool _isDragging;
    
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
        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        
        _raycaster = GetComponentInParent<GraphicRaycaster>();
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_card.IsInteractable || eventData.button != PointerEventData.InputButton.Left) 
            return;
        
        _isDragging = true;
        
        // Store original state
        _originalParent = transform.parent;
        _originalPosition = _rectTransform.localPosition;
        _originalScale = _rectTransform.localScale;
        _originalSortOrder = _canvas.sortingOrder;
        
        // Setup for dragging
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 0.8f;
        
        // Move to top layer
        transform.SetParent(_canvas.transform);
        _canvas.sortingOrder = dragSortOrder;
        _rectTransform.localScale = _originalScale * dragScaleMultiplier;
        
        OnCardDragStart?.Invoke(_card);
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;
        
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint
        );
        
        _rectTransform.localPosition = localPoint;
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;
        
        _isDragging = false;
        
        // Check what we're over
        GameObject dropTarget = GetDropTarget(eventData);
        
        if (dropTarget != null)
        {
            // Check drop area type
            if (dropTarget.CompareTag("PlayArea"))
            {
                HandlePlayAreaDrop();
            }
            else if (dropTarget.CompareTag("DiscardArea"))
            {
                HandleDiscardAreaDrop();
            }
            else
            {
                SnapBack();
            }
            
            OnCardDropped?.Invoke(_card, dropTarget);
        }
        else
        {
            SnapBack();
        }
        
        // Reset visual state
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.alpha = 1f;
        _canvas.sortingOrder = _originalSortOrder;
        
        OnCardDragEnd?.Invoke(_card);
    }
    
    private GameObject GetDropTarget(PointerEventData eventData)
    {
        var results = new System.Collections.Generic.List<RaycastResult>();
        _raycaster.Raycast(eventData, results);
        
        foreach (var result in results)
        {
            if (result.gameObject != gameObject && 
                (result.gameObject.CompareTag("PlayArea") || 
                 result.gameObject.CompareTag("DiscardArea")))
            {
                return result.gameObject;
            }
        }
        
        return null;
    }
    
    private void HandlePlayAreaDrop()
    {
        // Select card if not selected
        if (!_card.IsSelected)
            _card.Select();
        
        // Trigger play
        SpellcastManager.Instance?.TryPlayCards(new System.Collections.Generic.List<Card> { _card });
        
        // Card will be destroyed by play logic, no need to snap back
    }
    
    private void HandleDiscardAreaDrop()
    {
        // Select card if not selected
        if (!_card.IsSelected)
            _card.Select();
        
        // Check if we can spend creativity for discard
        if (!CombatManager.HasInstance || !CombatManager.Instance.CanSpendCreativity(1))
        {
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
        
        // Draw new card if possible
        if (DeckManager.HasInstance && !DeckManager.Instance.IsDeckEmpty)
        {
            var newCardData = DeckManager.Instance.DrawCard();
            if (newCardData != null && CardManager.HasInstance)
            {
                CardManager.Instance.SpawnCard(newCardData, null, true);
            }
        }
    }
    
    private void SnapBack()
    {
        transform.SetParent(_originalParent);
        StartCoroutine(AnimateSnapBack());
    }
    
    private System.Collections.IEnumerator AnimateSnapBack()
    {
        float elapsed = 0f;
        Vector3 startPos = _rectTransform.localPosition;
        Vector3 startScale = _rectTransform.localScale;
        
        while (elapsed < snapBackDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / snapBackDuration;
            
            _rectTransform.localPosition = Vector3.Lerp(startPos, _originalPosition, t);
            _rectTransform.localScale = Vector3.Lerp(startScale, _originalScale, t);
            
            yield return null;
        }
        
        _rectTransform.localPosition = _originalPosition;
        _rectTransform.localScale = _originalScale;
    }
    
    // Prevent drag if card is animating
    public bool CanDrag => !_isDragging && _card != null && _card.IsInteractable;
}