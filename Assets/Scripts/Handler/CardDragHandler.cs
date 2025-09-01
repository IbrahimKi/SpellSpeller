using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Vector2 originalPosition;
    private Transform originalParent;
    private int originalSiblingIndex;
    
    // Drag state
    private Vector2 dragOffset;
    private Camera eventCamera;
    
    // Events
    public static UnityEvent<GameObject> OnCardDragStart = new UnityEvent<GameObject>();
    public static UnityEvent<GameObject> OnCardDragEnd = new UnityEvent<GameObject>();

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
    
    void Start()
    {
        FindCanvas();
    }
    
    private void FindCanvas()
    {
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Canvas rootCanvas = canvas.rootCanvas;
            if (rootCanvas != null)
                canvas = rootCanvas;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (canvas == null) FindCanvas();
        if (canvas == null) return;
        
        // Save original state
        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        
        eventCamera = eventData.pressEventCamera;
        
        // Change parent for proper rendering
        transform.SetParent(canvas.transform);
        transform.SetAsLastSibling();
        
        // Calculate offset
        Vector2 localPointerPosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            eventCamera,
            out localPointerPosition))
        {
            dragOffset = rectTransform.anchoredPosition - localPointerPosition;
        }
        else
        {
            dragOffset = Vector2.zero;
        }
        
        // Visual feedback
        canvasGroup.alpha = 0.8f;
        canvasGroup.blocksRaycasts = false;
        
        OnCardDragStart?.Invoke(gameObject);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (canvas == null) return;
        
        Vector2 localPointerPosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            eventCamera,
            out localPointerPosition))
        {
            rectTransform.anchoredPosition = localPointerPosition + dragOffset;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        bool successfulDrop = false;
        
        // Find drop target
        var raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, raycastResults);
        
        foreach (var result in raycastResults)
        {
            if (result.gameObject != gameObject)
            {
                // Check for drop areas
                var dropArea = result.gameObject.GetComponent<DropAreaHandler>();
                if (dropArea != null)
                {
                    successfulDrop = HandleDropAreaDrop(dropArea);
                    break;
                }
                
                // Legacy tag-based drop areas
                if (result.gameObject.CompareTag("PlayArea"))
                {
                    successfulDrop = HandlePlayAreaDrop(result.gameObject);
                    break;
                }
                else if (result.gameObject.CompareTag("DiscardArea"))
                {
                    successfulDrop = HandleDiscardAreaDrop(result.gameObject);
                    break;
                }
            }
        }
        
        // Always return to original position if no successful drop
        if (!successfulDrop)
        {
            ReturnToOriginalPosition();
        }
        
        // Reset visual feedback
        ResetVisualFeedback();
        
        OnCardDragEnd?.Invoke(gameObject);
    }
    
    private void ResetVisualFeedback()
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
    }
    
    private bool HandleDropAreaDrop(DropAreaHandler dropArea)
    {
        // The DropAreaHandler will handle the specific logic
        return false; // Let drop fail so card returns to hand
    }
    
    private bool HandlePlayAreaDrop(GameObject playArea)
    {
        Card cardComponent = GetComponent<Card>();
        if (!cardComponent.IsPlayable()) return false;
        
        var cardList = new List<Card> { cardComponent };
        if (!SpellcastManager.CheckCanPlayCards(cardList)) return false;
        
        // Return to position first
        ReturnToOriginalPosition();
        
        // Select if not selected
        if (!cardComponent.IsSelected)
            cardComponent.TrySelect();
        
        // Process card play
        return CoreExtensions.TryWithManager<SpellcastManager, bool>(this, sm => 
        {
            sm.ProcessCardPlay(cardList);
            return true;
        });
    }
    
    private bool HandleDiscardAreaDrop(GameObject discardArea)
    {
        Card cardComponent = GetComponent<Card>();
        if (!cardComponent.IsPlayable()) return false;
        
        if (!SpellcastManager.CheckCanDiscardCard(cardComponent)) return false;
        
        return CoreExtensions.TryWithManager<CombatManager, bool>(this, cm => 
        {
            if (cm.CanSpendResource(ResourceType.Creativity, 1))
            {
                cm.TryModifyResource(ResourceType.Creativity, -1);
                
                CoreExtensions.TryWithManager<DeckManager>(this, dm => 
                {
                    if (cardComponent.CardData != null)
                        dm.DiscardCard(cardComponent.CardData);
                });
                
                CoreExtensions.TryWithManager<CardManager>(this, cardManager => 
                {
                    cardManager.RemoveCardFromHand(cardComponent);
                    cardManager.DestroyCard(cardComponent);
                });
                
                // Draw new card
                CoreExtensions.TryWithManager<DeckManager>(this, dm => dm.TryDrawCard());
                
                return true;
            }
            return false;
        });
    }

    private void ReturnToOriginalPosition()
    {
        // Return to original parent
        transform.SetParent(originalParent);
        transform.SetSiblingIndex(originalSiblingIndex);
        
        // Reset position
        rectTransform.anchoredPosition = originalPosition;
        
        // Reset scale
        rectTransform.localScale = Vector3.one;
        
        // Reset anchors for hand layout
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        
        // Ensure card is in hand
        Card cardComponent = GetComponent<Card>();
        CoreExtensions.TryWithManager<CardManager>(this, cm => 
        {
            if (!cm.GetHandCards().Contains(cardComponent))
            {
                cm.AddCardToHand(cardComponent);
            }
        });
        
        // Update hand layout
        CoreExtensions.TryWithManager<HandLayoutManager>(this, hlm => 
        {
            hlm.UpdateLayout();
        });
    }

#if UNITY_EDITOR
    [ContextMenu("Debug Card State")]
    private void DebugCardState()
    {
        Debug.Log($"[CardDragHandler] Card Debug Info:");
        Debug.Log($"  Original parent: {originalParent?.name ?? "null"}");
        Debug.Log($"  Current parent: {transform.parent?.name ?? "null"}");
        Debug.Log($"  Original position: {originalPosition}");
        Debug.Log($"  Current position: {rectTransform.anchoredPosition}");
        
        Card cardComponent = GetComponent<Card>();
        Debug.Log($"  Card IsPlayable: {cardComponent.IsPlayable()}");
        Debug.Log($"  Card IsSelected: {cardComponent.IsSelected}");
    }
#endif
}