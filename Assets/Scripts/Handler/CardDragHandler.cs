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
    
    // Slot system support
    private bool wasInSlotBefore = false;
    private int originalSlotIndex = -1;
    private CardSlotBehaviour originalSlot = null;
    
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
        Debug.Log("[CardDragHandler] === OnEndDrag START ===");
        
        GameObject dropTarget = null;
        bool successfulDrop = false;
        
        // Find drop target
        var raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, raycastResults);
        
        Debug.Log($"[CardDragHandler] Raycast hits: {raycastResults.Count}");
        
        foreach (var result in raycastResults)
        {
            Debug.Log($"  - Hit: {result.gameObject.name} (Layer: {result.gameObject.layer})");
            
            if (result.gameObject != gameObject)
            {
                // Check for slot drop (highest priority)
                var slotBehaviour = result.gameObject.GetComponent<CardSlotBehaviour>();
                if (slotBehaviour != null)
                {
                    Debug.Log($"  -> Found Slot: {slotBehaviour.SlotIndex + 1}");
                    dropTarget = result.gameObject;
                    successfulDrop = HandleSlotDrop(slotBehaviour);
                    break;
                }
                
                // Check for drop areas
                var dropArea = result.gameObject.GetComponent<DropAreaHandler>();
                if (dropArea != null)
                {
                    Debug.Log($"  -> Found DropArea");
                    dropTarget = result.gameObject;
                    successfulDrop = HandleDropAreaDrop(dropArea);
                    break;
                }
                
                // Legacy tag-based drop areas
                if (result.gameObject.CompareTag("PlayArea"))
                {
                    Debug.Log($"  -> Found PlayArea (Tag)");
                    dropTarget = result.gameObject;
                    successfulDrop = HandlePlayAreaDrop(dropTarget);
                    break;
                }
                else if (result.gameObject.CompareTag("DiscardArea"))
                {
                    Debug.Log($"  -> Found DiscardArea (Tag)");
                    dropTarget = result.gameObject;
                    successfulDrop = HandleDiscardAreaDrop(dropTarget);
                    break;
                }
            }
        }
        
        Debug.Log($"[CardDragHandler] Drop result: {(successfulDrop ? "SUCCESS" : "FAILED")}");
        
        // Handle drop result
        if (!successfulDrop)
        {
            ReturnToOriginalPosition();
        }
        
        // Always reset visual feedback
        ResetVisualFeedback();
        
        OnCardDragEnd?.Invoke(gameObject);
        
        Debug.Log("[CardDragHandler] === OnEndDrag END ===");
    }
    
    private void ResetVisualFeedback()
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
    }
    
    // === SLOT SYSTEM SUPPORT ===
    
    private void CheckIfInSlot()
    {
        wasInSlotBefore = false;
        originalSlotIndex = -1;
        originalSlot = null;
        
        // Check if parent has a CardSlotBehaviour
        var parentSlot = GetComponentInParent<CardSlotBehaviour>();
        
        Debug.Log($"[CardDragHandler] CheckIfInSlot - Parent: {transform.parent?.name}, ParentSlot: {parentSlot?.name}");
        
        if (parentSlot != null && parentSlot.OccupyingCard == GetComponent<Card>())
        {
            wasInSlotBefore = true;
            originalSlotIndex = parentSlot.SlotIndex;
            originalSlot = parentSlot;
            
            Debug.Log($"[CardDragHandler] Card WAS in slot {originalSlotIndex + 1}");
            
            // Clear the slot's reference but keep our reference
            parentSlot.RemoveCard(false); // WICHTIG: Slot muss geleert werden!
        }
        else
        {
            Debug.Log($"[CardDragHandler] Card was NOT in a slot");
        }
    }
    
    private bool HandleSlotDrop(CardSlotBehaviour targetSlot)
    {
        Card cardComponent = GetComponent<Card>();
        
        Debug.Log($"[CardDragHandler] HandleSlotDrop - Target Slot: {targetSlot.SlotIndex + 1}");
        Debug.Log($"  - Target IsEmpty: {targetSlot.IsEmpty}");
        Debug.Log($"  - Target IsEnabled: {targetSlot.IsEnabled}");
        Debug.Log($"  - Card IsPlayable: {cardComponent.IsPlayable()}");
        Debug.Log($"  - Card IsInteractable: {cardComponent.IsInteractable}");
        Debug.Log($"  - Card CardData: {cardComponent.CardData?.name ?? "NULL"}");
        
        if (!cardComponent.IsPlayable())
        {
            Debug.LogWarning("[CardDragHandler] Card not playable!");
            return false;
        }
        
        // Wenn die Karte vom gleichen Slot kommt, abbrechen
        if (originalSlot == targetSlot)
        {
            Debug.Log("[CardDragHandler] Same slot - canceling drop");
            return false;
        }
        
        bool success = targetSlot.TryPlaceCard(cardComponent);
        
        if (success)
        {
            Debug.Log($"[CardDragHandler] SUCCESS - Card placed in slot {targetSlot.SlotIndex + 1}");
            
            // Update original references für nächsten Drag
            originalParent = transform.parent;
            originalPosition = rectTransform.anchoredPosition;
            originalSiblingIndex = transform.GetSiblingIndex();
            
            // Update slot tracking
            wasInSlotBefore = true;
            originalSlot = targetSlot;
            originalSlotIndex = targetSlot.SlotIndex;
            
            // Clear old slot reference if needed
            if (originalSlot != null && originalSlot != targetSlot)
            {
                originalSlot.RemoveCard(false);
            }
            
            // Remove from hand
            CoreExtensions.TryWithManager<CardManager>(this, cm => 
            {
                cm.RemoveCardFromHand(cardComponent);
            });
        }
        else
        {
            Debug.LogError($"[CardDragHandler] FAILED - Could not place card in slot {targetSlot.SlotIndex + 1}");
        }
        
        return success;
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
        Debug.Log($"[CardDragHandler] Returning card to original position");
        
        // Erst parent zurücksetzen
        transform.SetParent(originalParent);
        transform.SetSiblingIndex(originalSiblingIndex);
        
        // Dann position zurücksetzen
        rectTransform.anchoredPosition = originalPosition;
        
        // Scale zurücksetzen falls verändert
        rectTransform.localScale = Vector3.one;
        
        // Anchors zurücksetzen für Hand-Layout
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        
        // Wenn Card ursprünglich in einem Slot war, versuche sie zurückzusetzen
        if (wasInSlotBefore && originalSlot != null)
        {
            Debug.Log($"[CardDragHandler] Attempting to restore card to original slot {originalSlotIndex + 1}");
            
            Card cardComponent = GetComponent<Card>();
            bool restored = originalSlot.TryPlaceCard(cardComponent);
            
            if (restored)
            {
                Debug.Log($"[CardDragHandler] Successfully restored card to original slot {originalSlotIndex + 1}");
            }
            else
            {
                Debug.LogWarning($"[CardDragHandler] Failed to restore card to slot {originalSlotIndex + 1}, leaving in hand");
                EnsureCardInHand();
            }
        }
        else
        {
            // Normale Hand-Rückkehr
            EnsureCardInHand();
        }
    }
    
    private void EnsureCardInHand()
    {
        Card cardComponent = GetComponent<Card>();
        
        // Sicherstellen dass Card im CardManager registriert ist
        CoreExtensions.TryWithManager<CardManager>(this, cm => 
        {
            if (!cm.GetHandCards().Contains(cardComponent))
            {
                Debug.Log("[CardDragHandler] Re-adding card to hand");
                cm.AddCardToHand(cardComponent);
            }
        });
        
        // Hand Layout Update triggern
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
        Debug.Log($"  Was in slot before: {wasInSlotBefore}");
        Debug.Log($"  Original slot index: {originalSlotIndex}");
        Debug.Log($"  Original slot ref: {originalSlot?.name ?? "null"}");
        Debug.Log($"  Original parent: {originalParent?.name ?? "null"}");
        Debug.Log($"  Current parent: {transform.parent?.name ?? "null"}");
        Debug.Log($"  Original position: {originalPosition}");
        Debug.Log($"  Current position: {rectTransform.anchoredPosition}");
        Debug.Log($"  Current scale: {rectTransform.localScale}");
        
        Card cardComponent = GetComponent<Card>();
        Debug.Log($"  Card IsPlayable: {cardComponent.IsPlayable()}");
        Debug.Log($"  Card IsSelected: {cardComponent.IsSelected}");
        Debug.Log($"  Card State: {cardComponent.CurrentState}");
    }
    
    [ContextMenu("Force Reset Card")]
    private void ForceResetCard()
    {
        ReturnToOriginalPosition();
        ResetVisualFeedback();
    }
    
    [ContextMenu("Test Slot Placement")]
    private void TestSlotPlacement()
    {
        if (!CardSlotManager.HasInstance)
        {
            Debug.LogError("[CardDragHandler] CardSlotManager not available for testing");
            return;
        }
        
        Card cardComponent = GetComponent<Card>();
        if (cardComponent == null)
        {
            Debug.LogError("[CardDragHandler] No Card component found");
            return;
        }
        
        var csm = CardSlotManager.Instance;
        bool placed = csm.TryPlaceCardInSlot(cardComponent);
        
        Debug.Log($"[CardDragHandler] Test slot placement: {(placed ? "Success" : "Failed")}");
        if (placed)
        {
            Debug.Log($"  Card placed in first available slot");
        }
    }
#endif
}