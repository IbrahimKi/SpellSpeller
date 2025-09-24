using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CardSystem.Extensions;
using GameSystem.Extensions;

namespace Handler
{
    public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform rectTransform;
        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private Vector2 originalPosition;
        private Transform originalParent;
        private int originalSiblingIndex;
        private int originalHandIndex;
    
        // Drag state
        private Vector2 dragOffset;
        private Camera eventCamera;
        
        // FIXED: Group Drag Integration
        private bool _isPartOfGroupDrag = false;
        private bool _isDraggingInHand = false;
        private int _currentDropIndex = -1;
    
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
            
            var selectionManager = CoreExtensions.GetManager<SelectionManager>();
            var card = GetComponent<Card>();
            
            // FIXED: Check if part of selection for group drag
            if (selectionManager != null && selectionManager.HasSelection && 
                selectionManager.SelectedCards.Contains(card))
            {
                _isPartOfGroupDrag = true;
                var groupHandler = GroupDragHandler.Instance;
                groupHandler.StartGroupDrag(selectionManager.SelectedCards.ToList(), eventData.position);
                OnCardDragStart?.Invoke(gameObject);
                return; // WICHTIG: Return hier um weitere Verarbeitung zu stoppen
            }
            
            // Single card drag - store original state
            _isPartOfGroupDrag = false;
            StartSingleCardDrag(eventData);
        }
        
        private void StartSingleCardDrag(PointerEventData eventData)
        {
            originalPosition = rectTransform.anchoredPosition;
            originalParent = transform.parent;
            originalSiblingIndex = transform.GetSiblingIndex();
            originalHandIndex = GetCardHandIndex(GetComponent<Card>());
            
            eventCamera = eventData.pressEventCamera;
            
            // Check if dragging within hand container
            var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
            _isDraggingInHand = (handLayoutManager != null && transform.parent == handLayoutManager.transform);
            
            if (_isDraggingInHand)
            {
                // For hand reordering, calculate offset relative to parent
                Vector2 localPointerPosition;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        transform.parent as RectTransform,
                        eventData.position,
                        eventCamera,
                        out localPointerPosition))
                {
                    dragOffset = rectTransform.anchoredPosition - localPointerPosition;
                }
            }
            else
            {
                // External drag - move to canvas
                transform.SetParent(canvas.transform);
                transform.SetAsLastSibling();
                
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
            }
            
            canvasGroup.alpha = 0.8f;
            canvasGroup.blocksRaycasts = false;
            
            OnCardDragStart?.Invoke(gameObject);
        }

        public void OnDrag(PointerEventData eventData)
        {
            // FIXED: If part of group drag, delegate to GroupDragHandler
            if (_isPartOfGroupDrag)
            {
                GroupDragHandler.Instance.UpdateGroupDrag(eventData.position);
                return; // Don't process individual movement
            }
            
            if (canvas == null) return;
            
            Vector2 localPointerPosition;
            RectTransform parentRect = _isDraggingInHand ? 
                transform.parent as RectTransform : 
                canvas.transform as RectTransform;
            
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    eventData.position,
                    eventCamera,
                    out localPointerPosition))
            {
                rectTransform.anchoredPosition = localPointerPosition + dragOffset;
                
                // Update drop preview for hand reordering
                if (_isDraggingInHand)
                {
                    UpdateHandDropPreview(eventData.position);
                }
            }
        }

        private void UpdateHandDropPreview(Vector2 screenPosition)
        {
            var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
            if (handLayoutManager == null) return;
            
            int newDropIndex = handLayoutManager.GetDropIndexFromScreenPosition(screenPosition);
            
            if (newDropIndex != _currentDropIndex)
            {
                _currentDropIndex = newDropIndex;
                
                if (_currentDropIndex >= 0)
                {
                    handLayoutManager.ShowDropPreview(_currentDropIndex);
                }
                else
                {
                    handLayoutManager.HideDropPreview();
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // FIXED: Handle group drag end
            if (_isPartOfGroupDrag)
            {
                // Find drop target for group
                var groupRaycastResults = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventData, groupRaycastResults);

                GameObject dropTarget = null;
                foreach (var result in groupRaycastResults)
                {
                    if (result.gameObject != gameObject)
                    {
                        dropTarget = result.gameObject;
                        break;
                    }
                }
                
                GroupDragHandler.Instance.EndGroupDrag(dropTarget);
                OnCardDragEnd?.Invoke(gameObject);
                _isPartOfGroupDrag = false;
                return;
            }
            
            // Single card drag handling
            bool successfulDrop = false;
            
            if (_isDraggingInHand)
            {
                successfulDrop = HandleHandReordering();
            }
            else
            {
                successfulDrop = HandleExternalDrop(eventData);
            }
            
            Debug.Log($"[CardDragHandler] Drop result: {(successfulDrop ? "SUCCESS" : "FAILED")}");
            
            // Return to original position if drop failed
            if (!successfulDrop)
            {
                ReturnToOriginalPosition();
            }
            
            // Cleanup
            ResetVisualFeedback();
            HideDropPreview();
            
            OnCardDragEnd?.Invoke(gameObject);
        }
        
        private bool HandleExternalDrop(PointerEventData eventData)
        {
            var raycastResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, raycastResults);
            
            Debug.Log($"[CardDragHandler] Found {raycastResults.Count} raycast hits");
            
            foreach (var result in raycastResults)
            {
                Debug.Log($"  - Hit: {result.gameObject.name} (Tag: {result.gameObject.tag})");
                
                if (result.gameObject != gameObject)
                {
                    // Check for drop areas
                    var dropArea = result.gameObject.GetComponent<DropAreaHandler>();
                    if (dropArea != null)
                    {
                        Debug.Log("  -> Found DropAreaHandler!");
                        if (HandleDropAreaDrop(dropArea))
                            return true;
                    }
                    
                    // Check for PlayArea tag
                    if (result.gameObject.CompareTag("PlayArea"))
                    {
                        Debug.Log("  -> Found PlayArea!");
                        if (HandlePlayAreaDrop(result.gameObject))
                            return true;
                    }
                    // Check for DiscardArea tag
                    else if (result.gameObject.CompareTag("DiscardArea"))
                    {
                        Debug.Log("  -> Found DiscardArea!");
                        if (HandleDiscardAreaDrop(result.gameObject))
                            return true;
                    }
                    // Check for card-to-card reordering
                    else
                    {
                        var targetCard = result.gameObject.GetComponent<Card>();
                        if (targetCard != null && IsCardInHand(targetCard))
                        {
                            if (HandleCardToCardReorder(targetCard))
                                return true;
                        }
                    }
                }
            }
            
            return false;
        }

        private bool HandleHandReordering()
        {
            if (_currentDropIndex < 0) return false;
            
            var card = GetComponent<Card>();
            var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
            
            if (card == null || handLayoutManager == null) return false;
            
            // Adjust drop index if needed (don't drop on self)
            int adjustedIndex = _currentDropIndex;
            if (originalHandIndex >= 0 && _currentDropIndex > originalHandIndex)
            {
                adjustedIndex = _currentDropIndex - 1;
            }
            
            // Only move if position actually changed
            if (adjustedIndex != originalHandIndex)
            {
                handLayoutManager.MoveCardToPosition(card, adjustedIndex);
                return true;
            }
            
            return false;
        }

        private bool HandleCardToCardReorder(Card targetCard)
        {
            var card = GetComponent<Card>();
            var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
            
            if (card == null || handLayoutManager == null) return false;
            
            // Return to original position first
            ReturnToOriginalPosition();
            
            // Move to target position
            int targetIndex = GetCardHandIndex(targetCard);
            handLayoutManager.MoveCardToPosition(card, targetIndex);
            
            return true;
        }

        private bool IsCardInHand(Card card)
        {
            var cardManager = CoreExtensions.GetManager<CardManager>();
            var handCards = cardManager?.GetHandCards();
            
            if (handCards != null)
            {
                foreach (var handCard in handCards)
                {
                    if (handCard == card)
                        return true;
                }
            }
            
            return false;
        }

        private int GetCardHandIndex(Card card)
        {
            if (card == null) return -1;
            
            // Try direct property first
            var handIndexProperty = card.GetType().GetProperty("HandIndex");
            if (handIndexProperty != null && handIndexProperty.CanRead)
            {
                var value = handIndexProperty.GetValue(card);
                return value is int intValue ? intValue : -1;
            }
            
            // Fallback: Use transform sibling index
            var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
            if (handLayoutManager != null && card.transform.parent == handLayoutManager.transform)
            {
                return card.transform.GetSiblingIndex();
            }
            
            return -1;
        }

        private void HideDropPreview()
        {
            var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
            handLayoutManager?.HideDropPreview();
            _currentDropIndex = -1;
        }
    
        private void ResetVisualFeedback()
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            _isDraggingInHand = false;
        }
    
        private bool HandleDropAreaDrop(DropAreaHandler dropArea)
        {
            return false; // Let tag-based logic handle it
        }
    
        private bool HandlePlayAreaDrop(GameObject playArea)
        {
            Debug.Log("[CardDragHandler] Handling PlayArea drop...");
            
            Card cardComponent = GetComponent<Card>();
            if (cardComponent == null)
            {
                Debug.LogError("No Card component found!");
                return false;
            }
            
            if (!cardComponent.IsPlayable())
            {
                Debug.Log("Card is not playable");
                return false;
            }
            
            var cardList = new List<Card> { cardComponent };
            if (!SpellcastManager.CheckCanPlayCards(cardList))
            {
                Debug.Log("SpellcastManager says cannot play cards");
                return false;
            }
            
            Debug.Log("[CardDragHandler] Playing card...");
            
            // Return to original position first
            ReturnToOriginalPosition();
            
            // Select if not selected
            if (!cardComponent.IsSelected)
            {
                cardComponent.TrySelect();
            }
            
            // Play the card
            bool played = false;
            CoreExtensions.TryWithManager<SpellcastManager>(this, sm => 
            {
                sm.ProcessCardPlay(cardList);
                played = true;
            });
            
            Debug.Log($"[CardDragHandler] Card play result: {played}");
            
            return played;
        }
    
        private bool HandleDiscardAreaDrop(GameObject discardArea)
        {
            Debug.Log("[CardDragHandler] Handling DiscardArea drop...");
            
            Card cardComponent = GetComponent<Card>();
            if (cardComponent == null)
            {
                Debug.LogError("No Card component found!");
                return false;
            }
            
            if (!cardComponent.IsPlayable())
            {
                Debug.Log("Card is not playable");
                return false;
            }
            
            if (!SpellcastManager.CheckCanDiscardCard(cardComponent))
            {
                Debug.Log("Cannot discard card");
                return false;
            }
            
            bool discarded = false;
            
            CoreExtensions.TryWithManager<CombatManager>(this, cm => 
            {
                if (cm.CanSpendResource(ResourceType.Creativity, 1))
                {
                    Debug.Log("[CardDragHandler] Discarding card...");
                    
                    // Spend creativity
                    cm.TryModifyResource(ResourceType.Creativity, -1);
                    
                    // Add to discard pile
                    CoreExtensions.TryWithManager<DeckManager>(this, dm => 
                    {
                        if (cardComponent.CardData != null)
                            dm.DiscardCard(cardComponent.CardData);
                    });
                    
                    // Remove from hand and destroy
                    CoreExtensions.TryWithManager<CardManager>(this, cardManager => 
                    {
                        cardManager.RemoveCardFromHand(cardComponent);
                        cardManager.DestroyCard(cardComponent);
                    });
                    
                    // Draw new card
                    CoreExtensions.TryWithManager<DeckManager>(this, dm => dm.TryDrawCard());
                    
                    discarded = true;
                }
                else
                {
                    Debug.Log("Not enough creativity to discard");
                }
            });
            
            Debug.Log($"[CardDragHandler] Discard result: {discarded}");
            
            return discarded;
        }

        private void ReturnToOriginalPosition()
        {
            Debug.Log("[CardDragHandler] Returning to original position");
            
            // Return to original parent
            transform.SetParent(originalParent);
            transform.SetSiblingIndex(originalSiblingIndex);
            
            // Reset position
            rectTransform.anchoredPosition = originalPosition;
            
            // Reset scale and anchors
            rectTransform.localScale = Vector3.one;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            
            // Ensure card is in hand
            Card cardComponent = GetComponent<Card>();
            CoreExtensions.TryWithManager<CardManager>(this, cm => 
            {
                var handCards = cm.GetHandCards();
                bool cardInHand = false;
                foreach (var handCard in handCards)
                {
                    if (handCard == cardComponent)
                    {
                        cardInHand = true;
                        break;
                    }
                }
                
                if (!cardInHand)
                {
                    Debug.Log("[CardDragHandler] Re-adding card to hand");
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
        [ContextMenu("Debug Drop Areas")]
        private void DebugDropAreas()
        {
            var playArea = GameObject.FindGameObjectWithTag("PlayArea");
            var discardArea = GameObject.FindGameObjectWithTag("DiscardArea");
            
            Debug.Log($"PlayArea found: {playArea?.name ?? "NOT FOUND"}");
            Debug.Log($"DiscardArea found: {discardArea?.name ?? "NOT FOUND"}");
            Debug.Log($"In Hand Container: {_isDraggingInHand}");
            Debug.Log($"Original Hand Index: {originalHandIndex}");
            Debug.Log($"Current Drop Index: {_currentDropIndex}");
            Debug.Log($"Is Part of Group Drag: {_isPartOfGroupDrag}");
        }
        
        [ContextMenu("Debug Card State")]
        private void DebugCardState()
        {
            var card = GetComponent<Card>();
            var selectionManager = CoreExtensions.GetManager<SelectionManager>();
            
            if (card != null && selectionManager != null)
            {
                bool isSelected = selectionManager.SelectedCards.Contains(card);
                bool isHighlighted = selectionManager.HighlightedCards.Contains(card);
                
                Debug.Log($"[CardDragHandler] Card: {card.GetCardName()}");
                Debug.Log($"  Selected: {isSelected}");
                Debug.Log($"  Highlighted: {isHighlighted}");
                Debug.Log($"  Hand Index: {GetCardHandIndex(card)}");
                Debug.Log($"  Is Playable: {card.IsPlayable()}");
            }
        }
#endif
    }
}