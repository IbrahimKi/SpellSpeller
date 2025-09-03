// NEUE DATEI: Assets/Scripts/Handler/GroupDragHandler.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;

namespace Handler
{
    public class GroupDragHandler : MonoBehaviour
    {
        private static GroupDragHandler _instance;
        public static GroupDragHandler Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("GroupDragHandler");
                    _instance = go.AddComponent<GroupDragHandler>();
                }
                return _instance;
            }
        }
        
        [Header("Visual Settings")]
        [SerializeField] private float cardSpacing = 30f;
        [SerializeField] private float dragAlpha = 0.8f;
        
        // Drag state
        private List<Card> _draggedCards = new List<Card>();
        private Dictionary<Card, Vector2> _originalPositions = new Dictionary<Card, Vector2>();  // GEÄNDERT: Vector3 -> Vector2
        private Dictionary<Card, Transform> _originalParents = new Dictionary<Card, Transform>();
        private Dictionary<Card, int> _originalSiblingIndices = new Dictionary<Card, int>();
        private Vector2 _dragStartPosition;
        private Canvas _canvas;
        private bool _isGroupDragging = false;
        
        // Properties
        public bool IsGroupDragging => _isGroupDragging;
        public List<Card> DraggedCards => new List<Card>(_draggedCards);
        
        void Awake()
        {
            if (_instance == null)
                _instance = this;
            else if (_instance != this)
                Destroy(gameObject);
                
            FindCanvas();
        }
        
        void FindCanvas()
        {
            _canvas = FindObjectOfType<Canvas>();
        }
        
        public void StartGroupDrag(List<Card> cards, Vector2 startPosition)
        {
            if (cards == null || cards.Count == 0) return;
            
            _isGroupDragging = true;
            _draggedCards = new List<Card>(cards.Where(c => c != null));
            _dragStartPosition = startPosition;
            
            // Sort by hand index for proper visual ordering
            _draggedCards = _draggedCards.OrderBy(c => c.HandIndex).ToList();
            
            // Store original state and prepare for drag
            foreach (var card in _draggedCards)
            {
                var rectTransform = card.GetComponent<RectTransform>();
                if (rectTransform == null) continue;
                
                _originalPositions[card] = rectTransform.anchoredPosition;  // Bereits Vector2
                _originalParents[card] = rectTransform.parent;
                _originalSiblingIndices[card] = rectTransform.GetSiblingIndex();
                
                // Move to canvas for proper rendering
                rectTransform.SetParent(_canvas.transform);
                rectTransform.SetAsLastSibling();
                
                // Set visual state
                card.StartDrag();
                var canvasGroup = card.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = dragAlpha;
                    canvasGroup.blocksRaycasts = false;
                }
            }
            
            // Notify selection manager
            var selectionManager = CoreExtensions.GetManager<SelectionManager>();
            selectionManager?.StartGroupDrag(_draggedCards);
        }
        
        public void UpdateGroupDrag(Vector2 currentPosition)
        {
            if (!_isGroupDragging) return;
            
            Vector2 delta = currentPosition - _dragStartPosition;
            
            for (int i = 0; i < _draggedCards.Count; i++)
            {
                var card = _draggedCards[i];
                if (card == null) continue;
                
                var rectTransform = card.GetComponent<RectTransform>();
                if (rectTransform == null) continue;
                
                // Position cards with spacing
                Vector2 offset = new Vector2(i * cardSpacing, 0);
                
                // KORRIGIERT: Explizit Vector2 Addition
                if (_originalPositions.ContainsKey(card))
                {
                    Vector2 originalPos = _originalPositions[card];
                    Vector2 newPosition = originalPos + delta + offset;
                    rectTransform.anchoredPosition = newPosition;
                }
            }
            
            // Update drop preview
            UpdateDropPreview(currentPosition);
        }
        
        public void EndGroupDrag(GameObject dropTarget)
        {
            if (!_isGroupDragging) return;
            
            bool successfulDrop = false;
            
            // Check drop target
            if (dropTarget != null)
            {
                if (dropTarget.CompareTag("PlayArea"))
                {
                    successfulDrop = HandlePlayAreaDrop();
                }
                else if (dropTarget.CompareTag("DiscardArea"))
                {
                    successfulDrop = HandleDiscardAreaDrop();
                }
                else if (dropTarget.CompareTag("DeckArea"))
                {
                    successfulDrop = HandleDeckAreaDrop();
                }
                else
                {
                    // Check if dropped on another card (reordering)
                    var targetCard = dropTarget.GetComponent<Card>();
                    if (targetCard != null && !_draggedCards.Contains(targetCard))
                    {
                        successfulDrop = HandleCardReorder(targetCard);
                    }
                }
            }
            
            // Return cards if drop failed
            if (!successfulDrop)
            {
                ReturnCardsToOriginalPositions();
            }
            
            // Clean up
            foreach (var card in _draggedCards)
            {
                if (card != null)
                {
                    card.EndDrag();
                    var canvasGroup = card.GetComponent<CanvasGroup>();
                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = 1f;
                        canvasGroup.blocksRaycasts = true;
                    }
                }
            }
            
            // Clear state
            _draggedCards.Clear();
            _originalPositions.Clear();
            _originalParents.Clear();
            _originalSiblingIndices.Clear();
            _isGroupDragging = false;
            
            // Notify selection manager
            var selectionManager = CoreExtensions.GetManager<SelectionManager>();
            selectionManager?.EndGroupDrag();
            
            // Update layout
            var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
            handLayoutManager?.UpdateLayout();
        }
        
        bool HandlePlayAreaDrop()
        {
            var spellcastManager = CoreExtensions.GetManager<SpellcastManager>();
            if (spellcastManager == null) return false;
            
            // Return cards to hand first
            ReturnCardsToOriginalPositions();
            
            // Process in order
            spellcastManager.ProcessCardPlay(_draggedCards);
            return true;
        }
        
        bool HandleDiscardAreaDrop()
        {
            var combatManager = CoreExtensions.GetManager<CombatManager>();
            var deckManager = CoreExtensions.GetManager<DeckManager>();
            var cardManager = CoreExtensions.GetManager<CardManager>();
            
            if (combatManager == null) return false;
            
            int cost = _draggedCards.Count;
            if (!combatManager.CanSpendResource(ResourceType.Creativity, cost))
                return false;
            
            // Return cards to hand first
            ReturnCardsToOriginalPositions();
            
            combatManager.TryModifyResource(ResourceType.Creativity, -cost);
            
            foreach (var card in _draggedCards)
            {
                if (card.CardData != null)
                    deckManager?.DiscardCard(card.CardData);
                    
                cardManager?.RemoveCardFromHand(card);
                cardManager?.DestroyCard(card);
                deckManager?.TryDrawCard();
            }
            
            return true;
        }
        
        bool HandleDeckAreaDrop()
        {
            var deckManager = CoreExtensions.GetManager<DeckManager>();
            var cardManager = CoreExtensions.GetManager<CardManager>();
            
            if (deckManager == null) return false;
            
            // Return cards to hand first
            ReturnCardsToOriginalPositions();
            
            int cardsReturned = 0;
            
            foreach (var card in _draggedCards)
            {
                if (card.CardData != null)
                {
                    deckManager.AddCardToBottom(card.CardData);
                    cardManager?.RemoveCardFromHand(card);
                    cardManager?.DestroyCard(card);
                    cardsReturned++;
                }
            }
            
            // Draw equal number
            for (int i = 0; i < cardsReturned; i++)
            {
                deckManager.TryDrawCard();
            }
            
            return true;
        }
        
        bool HandleCardReorder(Card targetCard)
        {
            var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
            if (handLayoutManager == null) return false;
            
            // Return cards to hand first
            ReturnCardsToOriginalPositions();
            
            // Insert at target position
            int targetIndex = targetCard.HandIndex;
            handLayoutManager.MoveCardsToIndex(_draggedCards, targetIndex);
            
            return true;
        }
        
        void ReturnCardsToOriginalPositions()
        {
            foreach (var card in _draggedCards)
            {
                if (card == null) continue;
                
                var rectTransform = card.GetComponent<RectTransform>();
                if (rectTransform == null) continue;
                
                if (_originalParents.ContainsKey(card) && _originalPositions.ContainsKey(card))
                {
                    rectTransform.SetParent(_originalParents[card]);
                    rectTransform.SetSiblingIndex(_originalSiblingIndices[card]);
                    rectTransform.anchoredPosition = _originalPositions[card];  // Bereits Vector2
                }
            }
        }
        
        void UpdateDropPreview(Vector2 position)
        {
            // Visual feedback for drop position
            var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
            if (handLayoutManager == null) return;
            
            int dropIndex = handLayoutManager.GetDropIndex(position);
            if (dropIndex >= 0)
            {
                handLayoutManager.ShowDropPreview(dropIndex);
            }
            else
            {
                handLayoutManager.HideDropPreview();
            }
        }
    }
}