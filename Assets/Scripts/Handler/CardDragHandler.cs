using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    
        // Drag state
        private Vector2 dragOffset;
        private Camera eventCamera;
        private bool _isPartOfGroupDrag = false; // FIX: Track if part of group drag
    
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
            
            // Check if part of selection for group drag
            var selectionManager = CoreExtensions.GetManager<SelectionManager>();
            var card = GetComponent<Card>();
            
            if (selectionManager != null && selectionManager.HasSelection && 
                selectionManager.SelectedCards.Contains(card))
            {
                // FIX: Mark as group drag and let GroupDragHandler handle it
                _isPartOfGroupDrag = true;
                var groupHandler = GroupDragHandler.Instance;
                groupHandler.StartGroupDrag(selectionManager.SelectedCards.ToList(), eventData.position);
                OnCardDragStart?.Invoke(gameObject);
                return;
            }
            
            // Single card drag
            _isPartOfGroupDrag = false;
            originalPosition = rectTransform.anchoredPosition;
            originalParent = transform.parent;
            originalSiblingIndex = transform.GetSiblingIndex();
            
            eventCamera = eventData.pressEventCamera;
            
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
            
            canvasGroup.alpha = 0.8f;
            canvasGroup.blocksRaycasts = false;
            
            OnCardDragStart?.Invoke(gameObject);
        }

        public void OnDrag(PointerEventData eventData)
        {
            // FIX: Check if group dragging
            if (_isPartOfGroupDrag)
            {
                GroupDragHandler.Instance.UpdateGroupDrag(eventData.position);
                return;
            }
            
            // Single drag
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
            // FIX: Check if group dragging
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
            
            // Single card drop logic
            bool successfulDrop = false;
            
            Debug.Log("[CardDragHandler] OnEndDrag - Checking for drop targets...");
            
            // Find drop target
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
                        successfulDrop = HandleDropAreaDrop(dropArea);
                        if (successfulDrop) break;
                    }
                    
                    // Check for PlayArea tag
                    if (result.gameObject.CompareTag("PlayArea"))
                    {
                        Debug.Log("  -> Found PlayArea!");
                        successfulDrop = HandlePlayAreaDrop(result.gameObject);
                        if (successfulDrop) break;
                    }
                    // Check for DiscardArea tag
                    else if (result.gameObject.CompareTag("DiscardArea"))
                    {
                        Debug.Log("  -> Found DiscardArea!");
                        successfulDrop = HandleDiscardAreaDrop(result.gameObject);
                        if (successfulDrop) break;
                    }
                }
            }
            
            Debug.Log($"[CardDragHandler] Drop result: {(successfulDrop ? "SUCCESS" : "FAILED")}");
            
            // Return to original position if drop failed
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
    }
}