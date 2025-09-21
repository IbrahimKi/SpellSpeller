using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using CardSystem.Extensions;
using GameSystem.Extensions;

public class CardInputController : MonoBehaviour
{
    [Header("Input Settings")]
    [SerializeField] private float doubleClickTime = 0.3f;
    [SerializeField] private bool enableKeyboardShortcuts = true;
    [SerializeField] private bool enableMultiSelect = true;
    
    [Header("Reordering Settings")]
    [SerializeField] private bool enableKeyboardReordering = true;
    [SerializeField] private float reorderCooldown = 0.1f;
    [SerializeField] private bool visualReorderFeedback = true;
    
    [Header("Draw Settings")]
    [SerializeField] private int baseDrawCost = 1;
    
    // Click tracking
    private float _lastClickTime = 0f;
    private Card _lastClickedCard = null;
    private GameObject _lastClickedObject = null;
    
    // Reordering tracking
    private float _lastReorderTime = 0f;
    private bool _isReordering = false;
    
    // Draw cost tracking
    private int _currentDrawMultiplier = 0;
    
    // Singleton pattern
    private static CardInputController _instance;
    public static CardInputController Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<CardInputController>();
            return _instance;
        }
    }
    
    void Awake()
    {
        if (_instance == null)
            _instance = this;
        else if (_instance != this)
            Destroy(gameObject);
    }
    
    void Update()
    {
        if (enableKeyboardShortcuts)
            HandleKeyboardInput();
        
        HandleMouseInput();
    }
    
    void HandleKeyboardInput()
    {
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        if (selectionManager == null) return;
        
        // REORDERING CONTROLS (Alt + Arrow Keys)
        if (enableKeyboardReordering && alt && selectionManager.HasSelection)
        {
            if (CanReorder())
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    if (shift) // Move to far left
                        MoveSelectionToEnd(CardMoveDirection.Left);
                    else // Move one step left
                        MoveSelection(CardMoveDirection.Left);
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    if (shift) // Move to far right
                        MoveSelectionToEnd(CardMoveDirection.Right);
                    else // Move one step right
                        MoveSelection(CardMoveDirection.Right);
                }
                else if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    // Move to center
                    MoveSelectionToCenter();
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    // Random shuffle selected cards
                    ShuffleSelection();
                }
            }
        }
        // SELECTION CONTROLS (Ctrl + Arrow Keys) - when NOT using Alt
        else if (ctrl && !alt && selectionManager.HasSelection)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (shift) // Extend selection left
                    ExtendSelectionLeft();
                else // Contract selection from right
                    ContractSelectionRight();
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (shift) // Extend selection right
                    ExtendSelectionRight();
                else // Contract selection from left
                    ContractSelectionLeft();
            }
        }
        
        // MAIN SELECTION LOGIC (not alt, not ctrl)
        if (!alt && !ctrl)
        {
            // When cards are SELECTED (not highlighted)
            if (selectionManager.HasSelection && !selectionManager.HasHighlight)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    // Up arrow HIGHLIGHTS selected cards
                    selectionManager.HighlightSelection();
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    // Down arrow deselects all
                    selectionManager.ClearSelection();
                }
            }
            
            // When cards are HIGHLIGHTED
            if (selectionManager.HasHighlight)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    // Up arrow PLAYS highlighted cards
                    PlayHighlightedCards();
                }
                else if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    // Left arrow returns highlighted to deck
                    ReturnHighlightedToDeck();
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    // Right arrow discards highlighted
                    DiscardHighlightedCards();
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    // Down arrow clears highlight
                    selectionManager.ClearHighlight();
                }
            }
            
            // NAVIGATION CONTROLS (Arrow Keys only, no selection)
            if (!selectionManager.HasHighlight)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    if (shift) // Select range left
                        SelectAdjacentCard(CardMoveDirection.Left, true);
                    else // Select single card left
                        SelectAdjacentCard(CardMoveDirection.Left, false);
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    if (shift) // Select range right
                        SelectAdjacentCard(CardMoveDirection.Right, true);
                    else // Select single card right
                        SelectAdjacentCard(CardMoveDirection.Right, false);
                }
            }
        }
        
        // SPECIAL ACTIONS
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryDrawWithCost();
        }
        
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            QuickSelectForSpellBuilding();
        }
        
        // Escape: Clear all selections
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            selectionManager.ClearSelection();
            selectionManager.ClearHighlight();
        }
    }
    
    // REORDERING HELPER METHODS
    private bool CanReorder()
    {
        return Time.time - _lastReorderTime >= reorderCooldown && !_isReordering;
    }
    
    private void MoveSelection(CardMoveDirection direction)
    {
        if (!CanReorder()) return;
        
        _lastReorderTime = Time.time;
        _isReordering = true;
        
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        selectionManager?.MoveSelection(direction);
        
        if (visualReorderFeedback)
        {
            StartCoroutine(ReorderFeedback(direction));
        }
        
        _isReordering = false;
    }
    
    private void MoveSelectionToEnd(CardMoveDirection direction)
    {
        if (!CanReorder()) return;
        
        _lastReorderTime = Time.time;
        _isReordering = true;
        
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        selectionManager?.MoveSelectionToEnd(direction);
        
        if (visualReorderFeedback)
        {
            StartCoroutine(ReorderFeedback(direction, true));
        }
        
        _isReordering = false;
    }
    
    private void MoveSelectionToCenter()
    {
        if (!CanReorder()) return;
        
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
        var cardManager = CoreExtensions.GetManager<CardManager>();
        
        if (selectionManager == null || handLayoutManager == null || cardManager == null) return;
        
        _lastReorderTime = Time.time;
        _isReordering = true;
        
        int handSize = cardManager.HandSize;
        int centerIndex = handSize / 2;
        
        var selectedCards = GetSelectedCardsList(selectionManager);
        handLayoutManager.MoveCardsToPosition(selectedCards, centerIndex);
        
        if (visualReorderFeedback)
        {
            StartCoroutine(ReorderFeedback(CardMoveDirection.None));
        }
        
        _isReordering = false;
    }
    
    private void ShuffleSelection()
    {
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
        
        if (selectionManager == null || handLayoutManager == null) return;
        
        var selectedCards = GetSelectedCardsList(selectionManager);
        if (selectedCards.Count <= 1) return;
        
        // Shuffle the selected cards among themselves
        for (int i = 0; i < selectedCards.Count; i++)
        {
            var temp = selectedCards[i];
            int randomIndex = Random.Range(i, selectedCards.Count);
            selectedCards[i] = selectedCards[randomIndex];
            selectedCards[randomIndex] = temp;
        }
        
        // Find the leftmost position of selected cards
        int minIndex = GetMinHandIndex(selectedCards);
        
        // Reorder them
        handLayoutManager.MoveCardsToPosition(selectedCards, minIndex);
    }
    
    // SELECTION EXTENSION METHODS
    private void ExtendSelectionLeft()
    {
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        var cardManager = CoreExtensions.GetManager<CardManager>();
        
        if (selectionManager == null || cardManager == null) return;
        
        var handCards = cardManager.GetHandCards();
        var selectedCards = GetSelectedCardsList(selectionManager);
        
        if (selectedCards.Count == 0) return;
        
        int leftmostIndex = GetMinHandIndex(selectedCards);
        if (leftmostIndex > 0)
        {
            var cardToAdd = FindCardByHandIndex(handCards, leftmostIndex - 1);
            if (cardToAdd != null)
                selectionManager.AddToSelection(cardToAdd);
        }
    }
    
    private void ExtendSelectionRight()
    {
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        var cardManager = CoreExtensions.GetManager<CardManager>();
        
        if (selectionManager == null || cardManager == null) return;
        
        var handCards = cardManager.GetHandCards();
        var selectedCards = GetSelectedCardsList(selectionManager);
        
        if (selectedCards.Count == 0) return;
        
        int rightmostIndex = GetMaxHandIndex(selectedCards);
        if (rightmostIndex < handCards.Count - 1)
        {
            var cardToAdd = FindCardByHandIndex(handCards, rightmostIndex + 1);
            if (cardToAdd != null)
                selectionManager.AddToSelection(cardToAdd);
        }
    }
    
    private void ContractSelectionLeft()
    {
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        var selectedCards = GetSelectedCardsList(selectionManager);
        
        if (selectedCards.Count <= 1) return;
        
        var leftmostCard = FindCardWithMinHandIndex(selectedCards);
        if (leftmostCard != null)
            selectionManager.RemoveFromSelection(leftmostCard);
    }
    
    private void ContractSelectionRight()
    {
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        var selectedCards = GetSelectedCardsList(selectionManager);
        
        if (selectedCards.Count <= 1) return;
        
        var rightmostCard = FindCardWithMaxHandIndex(selectedCards);
        if (rightmostCard != null)
            selectionManager.RemoveFromSelection(rightmostCard);
    }
    
    private void SelectAdjacentCard(CardMoveDirection direction, bool addToSelection)
    {
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        var cardManager = CoreExtensions.GetManager<CardManager>();
        
        if (selectionManager == null || cardManager == null) return;
        
        var handCards = cardManager.GetHandCards();
        var selectedCards = GetSelectedCardsList(selectionManager);
        
        if (handCards.Count == 0) return;
        
        int targetIndex = 0;
        
        if (selectedCards.Count > 0)
        {
            int currentIndex = direction == CardMoveDirection.Left ? 
                GetMinHandIndex(selectedCards) : 
                GetMaxHandIndex(selectedCards);
            
            targetIndex = direction == CardMoveDirection.Left ? 
                Mathf.Max(0, currentIndex - 1) : 
                Mathf.Min(handCards.Count - 1, currentIndex + 1);
        }
        
        var targetCard = FindCardByHandIndex(handCards, targetIndex);
        if (targetCard != null)
        {
            if (addToSelection)
            {
                selectionManager.AddToSelection(targetCard);
            }
            else
            {
                selectionManager.ClearSelection();
                selectionManager.AddToSelection(targetCard);
            }
        }
    }
    
    // VISUAL FEEDBACK
    private IEnumerator ReorderFeedback(CardMoveDirection direction, bool toEnd = false)
    {
        string directionText = direction switch
        {
            CardMoveDirection.Left => toEnd ? "Far Left" : "Left",
            CardMoveDirection.Right => toEnd ? "Far Right" : "Right",
            _ => "Center"
        };
        
        Debug.Log($"[CardInputController] Reordered to {directionText}");
        yield return new WaitForSeconds(0.1f);
    }
    
    private void QuickSelectForSpellBuilding()
    {
        var cardManager = CoreExtensions.GetManager<CardManager>();
        cardManager?.AutoSelectForSpellBuilding();
    }
    
    // MOUSE INPUT
    void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleLeftClick();
        }
        else if (Input.GetMouseButtonDown(1))
        {
            HandleRightClick();
        }
        else if (Input.GetMouseButtonDown(2))
        {
            HandleMiddleClick();
        }
    }
    
    void HandleMiddleClick()
    {
        var cardManager = CoreExtensions.GetManager<CardManager>();
        if (cardManager != null)
        {
            var potential = cardManager.GetHandSpellPotential();
            Debug.Log($"[CardInputController] Spell Building Potential: {potential.OverallScore:P0}");
            
            if (potential.OverallScore > 0.5f)
            {
                cardManager.AutoSelectForSpellBuilding();
            }
        }
    }
    
    void HandleLeftClick()
    {
        var eventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };
        
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        
        Card clickedCard = null;
        GameObject clickedObject = null;
        
        foreach (var result in results)
        {
            clickedObject = result.gameObject;
            clickedCard = result.gameObject.GetComponent<Card>();
            
            if (clickedCard != null)
                break;
                
            if (result.gameObject.CompareTag("DeckArea"))
            {
                HandleDeckClick();
                return;
            }
        }
        
        if (clickedCard != null)
        {
            HandleCardClick(clickedCard);
        }
        else
        {
            // Click on empty space - clear selection
            var selectionManager = CoreExtensions.GetManager<SelectionManager>();
            selectionManager?.ClearSelection();
        }
    }
    
    void HandleCardClick(Card card)
    {
        float timeSinceLastClick = Time.time - _lastClickTime;
        bool isDoubleClick = (timeSinceLastClick <= doubleClickTime && _lastClickedCard == card);
        
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        if (selectionManager == null) return;
        
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        
        if (isDoubleClick)
        {
            // Double click - toggle highlight
            if (IsCardHighlighted(card))
                selectionManager.RemoveFromHighlight(card);
            else
                selectionManager.AddToHighlight(card);
        }
        else if (shift && enableMultiSelect && _lastClickedCard != null)
        {
            // Shift click - select range
            selectionManager.SelectRange(_lastClickedCard, card);
        }
        else if (ctrl && enableMultiSelect)
        {
            // Ctrl click - toggle selection
            selectionManager.ToggleCardSelection(card);
        }
        else
        {
            // Normal click - single select
            bool isSelected = GetCardIsSelected(card);
            if (!isSelected)
            {
                selectionManager.ClearSelection();
                selectionManager.AddToSelection(card);
            }
        }
        
        _lastClickTime = Time.time;
        _lastClickedCard = card;
    }
    
    void HandleDeckClick()
    {
        float timeSinceLastClick = Time.time - _lastClickTime;
        bool isDoubleClick = (timeSinceLastClick <= doubleClickTime && 
                             _lastClickedObject?.CompareTag("DeckArea") == true);
        
        if (isDoubleClick)
        {
            TryDrawWithCost();
        }
        
        _lastClickTime = Time.time;
        _lastClickedObject = GameObject.FindWithTag("DeckArea");
    }
    
    void HandleRightClick()
    {
        // Right click to deselect all
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        selectionManager?.ClearSelection();
        selectionManager?.ClearHighlight();
    }
    
    // ACTION METHODS
    internal void TryDrawWithCost()
    {
        var combatManager = CoreExtensions.GetManager<CombatManager>();
        var deckManager = CoreExtensions.GetManager<DeckManager>();
        
        if (combatManager == null || deckManager == null) return;
        
        int cost = baseDrawCost + _currentDrawMultiplier;
        
        if (combatManager.CanSpendResource(ResourceType.Creativity, cost))
        {
            combatManager.TryModifyResource(ResourceType.Creativity, -cost);
            deckManager.TryDrawCard();
            _currentDrawMultiplier++;
            
            Debug.Log($"[CardInputController] Drew card for {cost} creativity. Next cost: {baseDrawCost + _currentDrawMultiplier}");
        }
        else
        {
            Debug.Log($"[CardInputController] Not enough creativity ({cost} required)");
        }
    }
    
    public void ResetDrawCost()
    {
        _currentDrawMultiplier = 0;
    }
    
    void PlayHighlightedCards()
    {
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        var spellcastManager = CoreExtensions.GetManager<SpellcastManager>();
        
        if (selectionManager == null || spellcastManager == null) return;
        
        // Process highlighted cards
        selectionManager.ProcessHighlightedInOrder(card =>
        {
            var cardList = new List<Card> { card };
            spellcastManager.ProcessCardPlay(cardList);
        });
        
        selectionManager.ClearHighlight();
    }
    
    void DiscardHighlightedCards()
    {
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        var combatManager = CoreExtensions.GetManager<CombatManager>();
        var deckManager = CoreExtensions.GetManager<DeckManager>();
        var cardManager = CoreExtensions.GetManager<CardManager>();
        
        if (selectionManager == null || combatManager == null) return;
        
        selectionManager.ProcessHighlightedInOrder(card =>
        {
            if (combatManager.CanSpendResource(ResourceType.Creativity, 1))
            {
                combatManager.TryModifyResource(ResourceType.Creativity, -1);
                
                if (card.CardData != null)
                    deckManager?.DiscardCard(card.CardData);
                    
                cardManager?.RemoveCardFromHand(card);
                cardManager?.DestroyCard(card);
                deckManager?.TryDrawCard();
            }
        });
        
        selectionManager.ClearHighlight();
    }
    
    void ReturnHighlightedToDeck()
    {
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        var deckManager = CoreExtensions.GetManager<DeckManager>();
        var cardManager = CoreExtensions.GetManager<CardManager>();
        
        if (selectionManager == null || deckManager == null) return;
        
        int cardsReturned = 0;
        
        selectionManager.ProcessHighlightedInOrder(card =>
        {
            if (card.CardData != null)
            {
                deckManager.AddCardToBottom(card.CardData);
                cardManager?.RemoveCardFromHand(card);
                cardManager?.DestroyCard(card);
                cardsReturned++;
            }
        });
        
        // Draw equal number of cards
        for (int i = 0; i < cardsReturned; i++)
        {
            deckManager.TryDrawCard();
        }
        
        selectionManager.ClearHighlight();
    }
    
    // HELPER METHODS
    private List<Card> GetSelectedCardsList(SelectionManager selectionManager)
    {
        var result = new List<Card>();
        foreach (var card in selectionManager.SelectedCards)
        {
            result.Add(card);
        }
        return result;
    }
    
    private bool GetCardIsSelected(Card card)
    {
        if (card == null) return false;
        
        // Try direct property first
        var isSelectedProperty = card.GetType().GetProperty("IsSelected");
        if (isSelectedProperty != null && isSelectedProperty.CanRead)
        {
            var value = isSelectedProperty.GetValue(card);
            return value is bool boolValue && boolValue;
        }
        
        // Fallback: check if card is in selection manager
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        if (selectionManager != null)
        {
            foreach (var selectedCard in selectionManager.SelectedCards)
            {
                if (selectedCard == card) return true;
            }
        }
        
        return false;
    }
    
    private int GetMinHandIndex(List<Card> cards)
    {
        if (cards.Count == 0) return -1;
        
        int min = int.MaxValue;
        foreach (var card in cards)
        {
            int index = card.HandIndex;
            if (index >= 0 && index < min)
                min = index;
        }
        return min == int.MaxValue ? -1 : min;
    }
    
    private int GetMaxHandIndex(List<Card> cards)
    {
        if (cards.Count == 0) return -1;
        
        int max = -1;
        foreach (var card in cards)
        {
            int index = card.HandIndex;
            if (index > max)
                max = index;
        }
        return max;
    }
    
    private Card FindCardWithMinHandIndex(List<Card> cards)
    {
        if (cards.Count == 0) return null;
        
        Card minCard = null;
        int minIndex = int.MaxValue;
        
        foreach (var card in cards)
        {
            int index = card.HandIndex;
            if (index >= 0 && index < minIndex)
            {
                minIndex = index;
                minCard = card;
            }
        }
        
        return minCard;
    }
    
    private Card FindCardWithMaxHandIndex(List<Card> cards)
    {
        if (cards.Count == 0) return null;
        
        Card maxCard = null;
        int maxIndex = -1;
        
        foreach (var card in cards)
        {
            int index = card.HandIndex;
            if (index > maxIndex)
            {
                maxIndex = index;
                maxCard = card;
            }
        }
        
        return maxCard;
    }
    
    private Card FindCardByHandIndex(List<Card> cards, int targetIndex)
    {
        foreach (var card in cards)
        {
            if (card.HandIndex == targetIndex)
                return card;
        }
        return null;
    }
    
    private bool IsCardHighlighted(Card card)
    {
        return card.IsHighlighted(); // Verwendet jetzt CardSystem.Extensions
    }
    
    private bool GetCardIsPlayable(Card card)  
    {
        return card.IsPlayable(); // Verwendet jetzt GameSystem.Extensions
    }
    // Called by CombatManager on turn end
    public void OnTurnEnd()
    {
        ResetDrawCost();
    }
    
#if UNITY_EDITOR
    [ContextMenu("Debug Input State")]
    public void DebugInputState()
    {
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        if (selectionManager != null)
        {
            Debug.Log($"[CardInputController] Has Selection: {selectionManager.HasSelection}");
            Debug.Log($"[CardInputController] Has Highlight: {selectionManager.HasHighlight}");
            Debug.Log($"[CardInputController] Selected Count: {selectionManager.SelectedCards.Count}");
            Debug.Log($"[CardInputController] Highlighted Count: {selectionManager.HighlightedCards.Count}");
        }
    }
    
    [ContextMenu("Test Card Reordering")]
    public void TestCardReordering()
    {
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        if (selectionManager?.HasSelection == true)
        {
            Debug.Log("[CardInputController] Testing card reordering...");
            MoveSelection(CardMoveDirection.Right);
        }
        else
        {
            Debug.Log("[CardInputController] No cards selected for reordering test");
        }
    }
#endif
}