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
    
    // FIXED: Slot system support - korrigierte Variablen
    private bool wasInSlotBefore = false;
    private int originalSlotIndex = -1;
    private CardSlotBehaviour originalSlot = null; // FIXED: Direkte Slot-Referenz
    
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
        
        // FIXED: Check if card was in a slot before dragging
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
        
        // FIXED: Immer zurücksetzen wenn kein erfolgreicher Drop
        if (!successfulDrop)
        {
            ReturnToOriginalPosition();
        }
        
        // FIXED: Visual feedback IMMER zurücksetzen
        ResetVisualFeedback();
        
        OnCardDragEnd?.Invoke(gameObject);
    }
    
    // FIXED: Neue Methode für Visual Reset
    private void ResetVisualFeedback()
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
    }
    
    // === SLOT SYSTEM SUPPORT - FIXED ===
    
    private void CheckIfInSlot()
    {
        wasInSlotBefore = false;
        originalSlotIndex = -1;
        originalSlot = null;
        
        // FIXED: Check if parent has a CardSlotBehaviour
        var parentSlot = GetComponentInParent<CardSlotBehaviour>();
        if (parentSlot != null && parentSlot.OccupyingCard == GetComponent<Card>())
        {
            wasInSlotBefore = true;
            originalSlotIndex = parentSlot.SlotIndex;
            originalSlot = parentSlot; // FIXED: Direkte Referenz speichern
            
            Debug.Log($"[CardDragHandler] Card was in slot {originalSlotIndex + 1}, removing from slot");
            
            // Clear the slot since we're dragging out
            parentSlot.RemoveCard(false);
        }
    }
    
    private bool HandleSlotDrop(CardSlotBehaviour targetSlot)
    {
        Card cardComponent = GetComponent<Card>();
        
        // FIXED: Bessere Debugging
        Debug.Log($"[CardDragHandler] Attempting to place {cardComponent.GetCardName()} in slot {targetSlot.SlotIndex + 1}");
        Debug.Log($"  Card IsPlayable: {cardComponent.IsPlayable()}");
        Debug.Log($"  Slot IsEnabled: {targetSlot.IsEnabled}");
        Debug.Log($"  Slot IsEmpty: {targetSlot.IsEmpty}");
        Debug.Log($"  Slot CanAcceptCard: {targetSlot.CanAcceptCard(cardComponent)}");
        
        if (!cardComponent.IsPlayable())
        {
            Debug.LogWarning("[CardDragHandler] Card not playable for slot");
            return false;
        }
        
        bool success = targetSlot.TryPlaceCard(cardComponent);
        if (success)
        {
            Debug.Log($"[CardDragHandler] Card {cardComponent.GetCardName()} successfully placed in slot {targetSlot.SlotIndex + 1}");
            
            // FIXED: Clear original slot state since we placed successfully
            wasInSlotBefore = false;
            originalSlot = null;
        }
        else
        {
            Debug.LogWarning($"[CardDragHandler] Failed to place card in slot {targetSlot.SlotIndex + 1}");
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

    // FIXED: Komplett überarbeitete ReturnToOriginalPosition
    private void ReturnToOriginalPosition()
    {
        Debug.Log($"[CardDragHandler] Returning card to original position");
        
        // FIXED: Erst parent zurücksetzen
        transform.SetParent(originalParent);
        transform.SetSiblingIndex(originalSiblingIndex);
        
        // FIXED: Dann position zurücksetzen
        rectTransform.anchoredPosition = originalPosition;
        
        // FIXED: Scale zurücksetzen falls verändert
        rectTransform.localScale = Vector3.one;
        
        // FIXED: Anchors zurücksetzen für Hand-Layout
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        
        // FIXED: Wenn Card ursprünglich in einem Slot war, versuche sie zurückzusetzen
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
                // Falls Slot-Restore fehlschlägt, trotzdem zur Hand zurückkehren
                EnsureCardInHand();
            }
        }
        else
        {
            // Normale Hand-Rückkehr
            EnsureCardInHand();
        }
    }
    
    // FIXED: Neue Methode um sicherzustellen dass Card in Hand ist
    private void EnsureCardInHand()
    {
        Card cardComponent = GetComponent<Card>();
        
        // FIXED: Sicherstellen dass Card im CardManager registriert ist
        CoreExtensions.TryWithManager<CardManager>(this, cm => 
        {
            if (!cm.GetHandCards().Contains(cardComponent))
            {
                Debug.Log("[CardDragHandler] Re-adding card to hand");
                cm.AddCardToHand(cardComponent);
            }
        });
        
        // FIXED: Hand Layout Update triggern
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