using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;
using CardSystem.Extensions;
using GameSystem.Extensions;

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
        private Dictionary<Card, Vector2> _originalPositions = new Dictionary<Card, Vector2>();
        private Dictionary<Card, Transform> _originalParents = new Dictionary<Card, Transform>();
        private Dictionary<Card, int> _originalSiblingIndices = new Dictionary<Card, int>();
        
        // FIX 1: Besseres Drag-Offset Management
        private Vector2 _groupCenterOffset;
        private Vector2 _lastMousePosition;
        
        private Canvas _canvas;
        private bool _isGroupDragging = false;
        
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
        
        public void StartGroupDrag(List<Card> cards, Vector2 mousePosition)
        {
            if (cards == null || cards.Count == 0) return;
            
            _isGroupDragging = true;
            _draggedCards = new List<Card>(cards.Where(c => c != null));
            _lastMousePosition = mousePosition;
            
            // Sort by hand index using CardExtensions
            _draggedCards = _draggedCards.OrderBy(c => c.HandIndex()).ToList();
            
            // FIX 1: Calculate group center offset from mouse
            CalculateGroupCenterOffset(mousePosition);
            
            // Store original state
            foreach (var card in _draggedCards)
            {
                var rectTransform = card.GetComponent<RectTransform>();
                if (rectTransform == null) continue;
                
                _originalPositions[card] = rectTransform.anchoredPosition;
                _originalParents[card] = rectTransform.parent;
                _originalSiblingIndices[card] = rectTransform.GetSiblingIndex();
                
                // Move to canvas
                rectTransform.SetParent(_canvas.transform);
                rectTransform.SetAsLastSibling();
                
                // Set visual state using CardExtensions
                card.StartDrag();
                var canvasGroup = card.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = dragAlpha;
                    canvasGroup.blocksRaycasts = false;
                }
            }
            
            // Initial positioning
            UpdateGroupDrag(mousePosition);
            
            // Notify selection manager
            CoreExtensions.TryWithManagerStatic<SelectionManager>(sm => sm?.StartGroupDrag(_draggedCards));
        }
        
        // FIX 1: Proper offset calculation
        private void CalculateGroupCenterOffset(Vector2 mousePosition)
        {
            if (_draggedCards.Count == 0) return;
            
            // Get mouse position in canvas space
            Vector2 localMousePosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform,
                mousePosition,
                null,
                out localMousePosition
            );
            
            // Calculate center of selected cards
            Vector2 groupCenter = Vector2.zero;
            foreach (var card in _draggedCards)
            {
                var rect = card.GetComponent<RectTransform>();
                if (rect != null)
                    groupCenter += rect.anchoredPosition;
            }
            groupCenter /= _draggedCards.Count;
            
            // Store offset from mouse to group center
            _groupCenterOffset = groupCenter - localMousePosition;
        }
        
        public void UpdateGroupDrag(Vector2 mousePosition)
        {
            if (!_isGroupDragging) return;
            
            // FIX 1: Convert mouse to canvas space properly
            Vector2 localMousePosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform,
                mousePosition,
                null,
                out localMousePosition
            );
            
            // Apply offset so cards follow mouse naturally
            Vector2 groupPosition = localMousePosition + _groupCenterOffset;
            
            // Position each card relative to group center
            float totalWidth = (_draggedCards.Count - 1) * cardSpacing;
            float startX = -totalWidth / 2f;
            
            for (int i = 0; i < _draggedCards.Count; i++)
            {
                var card = _draggedCards[i];
                if (card == null) continue;
                
                var rectTransform = card.GetComponent<RectTransform>();
                if (rectTransform == null) continue;
                
                // Position relative to group center
                Vector2 cardPosition = groupPosition + new Vector2(startX + (i * cardSpacing), 0);
                rectTransform.anchoredPosition = cardPosition;
            }
            
            _lastMousePosition = mousePosition;
            
            // Update drop preview
            UpdateDropPreview(mousePosition);
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
                    // Check for reordering
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
            
            // Clean up visual state using CardExtensions
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
            
            // Notify managers
            CoreExtensions.TryWithManagerStatic<SelectionManager>(sm => sm?.EndGroupDrag());
            CoreExtensions.TryWithManagerStatic<HandLayoutManager>(hlm => hlm?.UpdateLayout());
        }
        
        bool HandlePlayAreaDrop()
        {
            // Return cards to hand first
            ReturnCardsToOriginalPositions();
            
            // Process play using SpellcastManager
            return CoreExtensions.TryWithManagerStatic<SpellcastManager, bool>(this, sm => 
            { 
                sm.ProcessCardPlay(_draggedCards); 
                return true; 
            });
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
            
            for (int i = 0; i < cardsReturned; i++)
            {
                deckManager.TryDrawCard();
            }
            
            return true;
        }
        
        bool HandleCardReorder(Card targetCard)
        {
            // FIX 2&3: Use HandLayoutManager for reordering
            return CoreExtensions.TryWithManagerStatic<HandLayoutManager, bool>(this, hlm =>
            {
                ReturnCardsToOriginalPositions();

                int targetIndex = targetCard.HandIndex(); // Use CardExtensions
                hlm.MoveCardsToPosition(_draggedCards, targetIndex);
                return true;
            });
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
                    rectTransform.anchoredPosition = _originalPositions[card];
                }
            }
        }
        
        void UpdateDropPreview(Vector2 position)
        {
            CoreExtensions.TryWithManagerStatic<HandLayoutManager>(hlm =>
            {
                int dropIndex = hlm.GetDropIndexFromScreenPosition(position);
                if (dropIndex >= 0)
                    hlm.ShowDropPreview(dropIndex);
                else
                    hlm.HideDropPreview();
            });
        }
    }
}