using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using GameCore.Enums;
using GameCore.Data;

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
    
    // Slot system support
    private bool wasInSlotBefore = false;
    private int originalSlotIndex = -1;
    
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
        
        // Check if card was in a slot before dragging
        CheckIfInSlot();
        
        eventCamera = eventData.pressEventCamera;
        
        // Change parent for proper rendering
        transform.SetParent(canvas.transform);
        transform.SetAsLastSibling();
        
        // Calculate offset AFTER parent change
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
        GameObject dropTarget = null;
        bool successfulDrop = false;
        
        // Find drop target
        var raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, raycastResults);
        
        foreach (var result in raycastResults)
        {
            if (result.gameObject != gameObject)
            {
                // Check for slot drop (highest priority)
                var slotBehaviour = result.gameObject.GetComponent<CardSlotBehaviour>();
                if (slotBehaviour != null)
                {
                    dropTarget = result.gameObject;
                    successfulDrop = HandleSlotDrop(slotBehaviour);
                    break;
                }
                
                // Check for drop areas
                var dropArea = result.gameObject.GetComponent<DropAreaHandler>();
                if (dropArea != null)
                {
                    dropTarget = result.gameObject;
                    successfulDrop = HandleDropAreaDrop(dropArea);
                    break;
                }
                
                // Legacy tag-based drop areas
                if (result.gameObject.CompareTag("PlayArea"))
                {
                    dropTarget = result.gameObject;
                    successfulDrop = HandlePlayAreaDrop(dropTarget);
                    break;
                }
                else if (result.gameObject.CompareTag("DiscardArea"))
                {
                    dropTarget = result.gameObject;
                    successfulDrop = HandleDiscardAreaDrop(dropTarget);
                    break;
                }
            }
        }
        
        // Return to original position if no valid target
        if (!successfulDrop)
        {
            ReturnToOriginalPosition();
        }
        
        // Reset visual feedback
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        
        OnCardDragEnd?.Invoke(gameObject);
    }
    
    // === SLOT SYSTEM SUPPORT ===
    
    private void CheckIfInSlot()
    {
        wasInSlotBefore = false;
        originalSlotIndex = -1;
        
        // Check if parent has a CardSlotBehaviour
        var parentSlot = GetComponentInParent<CardSlotBehaviour>();
        if (parentSlot != null && parentSlot.OccupyingCard == GetComponent<Card>())
        {
            wasInSlotBefore = true;
            originalSlotIndex = parentSlot.SlotIndex;
            
            // Clear the slot since we're dragging out
            parentSlot.RemoveCard(false);
        }
    }
    
    private bool HandleSlotDrop(CardSlotBehaviour targetSlot)
    {
        Card cardComponent = GetComponent<Card>();
        
        if (!cardComponent.IsPlayable())
        {
            Debug.LogWarning("[CardDragHandler] Card not playable for slot");
            return false;
        }
        
        return targetSlot.TryPlaceCard(cardComponent);
    }
    
    private bool HandleDropAreaDrop(DropAreaHandler dropArea)
    {
        // The DropAreaHandler will handle the specific logic based on its type
        return true; // Let the drop area handle it
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
        transform.SetParent(originalParent);
        transform.SetSiblingIndex(originalSiblingIndex);
        rectTransform.anchoredPosition = originalPosition;
        
        // If card was originally in a slot, try to restore it
        if (wasInSlotBefore && originalSlotIndex >= 0)
        {
            CoreExtensions.TryWithManager<CardSlotManager>(this, csm => 
            {
                Card cardComponent = GetComponent<Card>();
                csm.TryPlaceCardInSlot(cardComponent, originalSlotIndex);
                Debug.Log($"[CardDragHandler] Restored card to original slot {originalSlotIndex + 1}");
            });
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Debug Card State")]
    private void DebugCardState()
    {
        Debug.Log($"[CardDragHandler] Card Debug Info:");
        Debug.Log($"  Was in slot before: {wasInSlotBefore}");
        Debug.Log($"  Original slot index: {originalSlotIndex}");
        Debug.Log($"  Original parent: {originalParent?.name ?? "null"}");
        Debug.Log($"  Current parent: {transform.parent?.name ?? "null"}");
    }
#endif
}