using System.Collections.Generic;
using UnityEngine;
using CardSystem.Extensions;
using GameSystem.Extensions;

public class SelectionManager : SingletonBehaviour<SelectionManager>, IGameManager
{
    [Header("Selection Settings")]
    [SerializeField] private bool allowMultiSelect = true;
    [SerializeField] private int maxSelectedCards = 7;
    [SerializeField] private bool preserveHandOrder = true;
    
    // Selection tracking
    private List<Card> _selectedCards = new List<Card>();
    private List<Card> _highlightedCards = new List<Card>();
    private List<Card> _dragGroup = new List<Card>();
    
    // State tracking - Verhindert Event-Loops
    private bool _isUpdatingSelection = false;
    private bool _isUpdatingHighlight = false;
    
    private bool _isReady = false;
    public bool IsReady => _isReady;
    
    // Events
    public static event System.Action<List<Card>> OnSelectionChanged;
    public static event System.Action<List<Card>> OnHighlightChanged;
    public static event System.Action<List<Card>> OnDragGroupChanged;
    
    // Properties
    public IReadOnlyList<Card> SelectedCards => _selectedCards.AsReadOnly();
    public IReadOnlyList<Card> HighlightedCards => _highlightedCards.AsReadOnly();
    public IReadOnlyList<Card> DragGroup => _dragGroup.AsReadOnly();
    public bool HasSelection => _selectedCards.Count > 0;
    public bool HasHighlight => _highlightedCards.Count > 0;
    public bool IsGroupDragging => _dragGroup.Count > 0;
    
    protected override void OnAwakeInitialize()
    {
        _isReady = true;
        Debug.Log("[SelectionManager] Initialized and ready");
    }
    
    private void OnEnable()
    {
        // FIXED: Safe Event subscription
        try
        {
            Card.OnCardSelected += HandleCardSelected;
            Card.OnCardDeselected += HandleCardDeselected;
            Card.OnCardHighlighted += HandleCardHighlighted;
            Card.OnCardUnhighlighted += HandleCardUnhighlighted;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SelectionManager] Event subscription failed: {ex.Message}");
        }
    }
    
    private void OnDisable()
    {
        try
        {
            Card.OnCardSelected -= HandleCardSelected;
            Card.OnCardDeselected -= HandleCardDeselected;
            Card.OnCardHighlighted -= HandleCardHighlighted;
            Card.OnCardUnhighlighted -= HandleCardUnhighlighted;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SelectionManager] Event unsubscription failed: {ex.Message}");
        }
    }
    
    // === SELECTION MANAGEMENT ===
    
    public void AddToSelection(Card card, bool preserveOrder = true)
    {
        if (card == null || !card.IsPlayable() || _selectedCards.Contains(card)) return;
        if (_isUpdatingSelection) return;
        
        Debug.Log($"[SelectionManager] Adding card to selection: {card.GetCardName()}");
        
        if (!allowMultiSelect)
        {
            ClearSelection();
        }
        else if (_selectedCards.Count >= maxSelectedCards)
        {
            RemoveFromSelection(_selectedCards[0]);
        }
        
        _selectedCards.Add(card);
        
        if (preserveOrder)
            SortSelectionByHandIndex();
        
        _isUpdatingSelection = true;
        try
        {
            if (!GetCardIsSelected(card))
                SetCardSelected(card, true);
        }
        finally
        {
            _isUpdatingSelection = false;
        }
        
        OnSelectionChanged?.Invoke(_selectedCards);
    }
    
    public void RemoveFromSelection(Card card)
    {
        if (!_selectedCards.Remove(card)) return;
        if (_isUpdatingSelection) return;
        
        Debug.Log($"[SelectionManager] Removing card from selection: {card.GetCardName()}");
        
        _isUpdatingSelection = true;
        try
        {
            if (GetCardIsSelected(card))
                SetCardSelected(card, false);
        }
        finally
        {
            _isUpdatingSelection = false;
        }
        
        OnSelectionChanged?.Invoke(_selectedCards);
    }
    
    public void ToggleCardSelection(Card card)
    {
        if (_selectedCards.Contains(card))
            RemoveFromSelection(card);
        else
            AddToSelection(card);
    }
    
    public void SelectRange(Card fromCard, Card toCard)
    {
        var handCards = CoreExtensions.GetManager<CardManager>()?.GetHandCards();
        if (handCards == null) return;
        
        int fromIndex = fromCard.HandIndex();  // FIXED: Use Extension
        int toIndex = toCard.HandIndex();      // FIXED: Use Extension
        
        if (fromIndex == -1 || toIndex == -1) return;
        
        int start = Mathf.Min(fromIndex, toIndex);
        int end = Mathf.Max(fromIndex, toIndex);
        
        Debug.Log($"[SelectionManager] Selecting range from index {start} to {end}");
        
        ClearSelection();
        
        for (int i = start; i <= end; i++)
        {
            var card = FindCardByHandIndex(handCards, i);
            if (card != null)
                AddToSelection(card, false);
        }
        
        SortSelectionByHandIndex();
    }
    
    public void ClearSelection()
    {
        if (_isUpdatingSelection) return;
        if (_selectedCards.Count == 0) return;
        
        Debug.Log("[SelectionManager] Clearing all selection");
        
        var cardsToDeselect = new List<Card>(_selectedCards);
        _selectedCards.Clear();
        
        _isUpdatingSelection = true;
        try
        {
            foreach (var card in cardsToDeselect)
            {
                if (card != null && GetCardIsSelected(card))
                    SetCardSelected(card, false);
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
        
        OnSelectionChanged?.Invoke(_selectedCards);
    }
    
    // === HIGHLIGHT MANAGEMENT ===
    
    public void AddToHighlight(Card card)
    {
        if (card == null || _highlightedCards.Contains(card)) return;
        if (_isUpdatingHighlight) return;
        
        Debug.Log($"[SelectionManager] Adding card to highlight: {card.GetCardName()}");
        
        _highlightedCards.Add(card);
        
        _isUpdatingHighlight = true;
        try
        {
            if (!card.IsHighlighted())  // FIXED: Use Extension
                card.Highlight();       // FIXED: Use Extension
        }
        finally
        {
            _isUpdatingHighlight = false;
        }
        
        OnHighlightChanged?.Invoke(_highlightedCards);
    }
    
    public void RemoveFromHighlight(Card card)
    {
        if (!_highlightedCards.Remove(card)) return;
        if (_isUpdatingHighlight) return;
        
        Debug.Log($"[SelectionManager] Removing card from highlight: {card.GetCardName()}");
        
        _isUpdatingHighlight = true;
        try
        {
            if (card.IsHighlighted())   // FIXED: Use Extension
                card.Unhighlight();     // FIXED: Use Extension
        }
        finally
        {
            _isUpdatingHighlight = false;
        }
        
        OnHighlightChanged?.Invoke(_highlightedCards);
    }
    
    public void HighlightSelection()
    {
        if (_selectedCards.Count == 0) return;
        
        Debug.Log($"[SelectionManager] Highlighting {_selectedCards.Count} selected cards");
        
        var cardsToHighlight = new List<Card>(_selectedCards);
        
        _isUpdatingHighlight = true;
        try
        {
            foreach (var card in cardsToHighlight)
            {
                if (!_highlightedCards.Contains(card))
                {
                    _highlightedCards.Add(card);
                    card.Highlight();  // FIXED: Use Extension
                }
            }
        }
        finally
        {
            _isUpdatingHighlight = false;
        }
        
        OnHighlightChanged?.Invoke(_highlightedCards);
        ClearSelection(); // Clear selection after highlighting
    }
    
    public void ClearHighlight()
    {
        if (_isUpdatingHighlight) return;
        if (_highlightedCards.Count == 0) return;
        
        Debug.Log("[SelectionManager] Clearing all highlights");
        
        var cardsToUnhighlight = new List<Card>(_highlightedCards);
        _highlightedCards.Clear();
        
        _isUpdatingHighlight = true;
        try
        {
            foreach (var card in cardsToUnhighlight)
            {
                if (card != null && card.IsHighlighted())
                    card.Unhighlight();  // FIXED: Use Extension
            }
        }
        finally
        {
            _isUpdatingHighlight = false;
        }
        
        OnHighlightChanged?.Invoke(_highlightedCards);
    }
    
    // === DRAG GROUP MANAGEMENT ===
    
    public void StartGroupDrag(List<Card> cards)
    {
        _dragGroup = new List<Card>(cards);
        Debug.Log($"[SelectionManager] Starting group drag with {_dragGroup.Count} cards");
        
        foreach (var card in _dragGroup)
        {
            card.StartDrag();  // FIXED: Use Extension
        }
        OnDragGroupChanged?.Invoke(_dragGroup);
    }
    
    public void EndGroupDrag()
    {
        Debug.Log($"[SelectionManager] Ending group drag");
        
        foreach (var card in _dragGroup)
        {
            card.EndDrag();  // FIXED: Use Extension
        }
        _dragGroup.Clear();
        OnDragGroupChanged?.Invoke(_dragGroup);
    }
    
    // === MOVEMENT METHODS ===
    
    public void MoveSelection(CardMoveDirection direction)
    {
        if (!HasSelection) return;
        
        var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
        if (handLayoutManager == null) return;
        
        Debug.Log($"[SelectionManager] Moving selection {direction}");
        
        SortSelectionByHandIndex();
        
        if (direction == CardMoveDirection.Left)
        {
            var leftmost = _selectedCards[0];
            int targetIndex = Mathf.Max(0, leftmost.HandIndex() - 1);  // FIXED: Use Extension
            handLayoutManager.MoveCardsToPosition(_selectedCards, targetIndex);
        }
        else if (direction == CardMoveDirection.Right)
        {
            var rightmost = _selectedCards[_selectedCards.Count - 1];
            var handSize = CoreExtensions.GetManager<CardManager>()?.HandSize ?? 0;
            int targetIndex = Mathf.Min(handSize - 1, rightmost.HandIndex() + 1);  // FIXED: Use Extension
            handLayoutManager.MoveCardsToPosition(_selectedCards, targetIndex);
        }
    }
    
    public void MoveSelectionToEnd(CardMoveDirection direction)
    {
        if (!HasSelection) return;
        
        var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
        if (handLayoutManager == null) return;
        
        Debug.Log($"[SelectionManager] Moving selection to {direction} end");
        
        SortSelectionByHandIndex();
        
        if (direction == CardMoveDirection.Left)
        {
            handLayoutManager.MoveCardsToPosition(_selectedCards, 0);
        }
        else if (direction == CardMoveDirection.Right)
        {
            var handSize = CoreExtensions.GetManager<CardManager>()?.HandSize ?? 0;
            handLayoutManager.MoveCardsToPosition(_selectedCards, handSize - _selectedCards.Count);
        }
    }
    
    // === SMART SELECTION ===
    
    public void SelectCardsByPattern(SelectionPattern pattern)
    {
        var cardManager = CoreExtensions.GetManager<CardManager>();
        var handCards = cardManager?.GetHandCards();
        if (handCards == null || handCards.Count == 0) return;
        
        Debug.Log($"[SelectionManager] Selecting by pattern: {pattern}");
        
        ClearSelection();
        
        switch (pattern)
        {
            case SelectionPattern.EveryOther:
                for (int i = 0; i < handCards.Count; i += 2)
                    AddToSelection(handCards[i], false);
                break;
            case SelectionPattern.FirstHalf:
                int halfCount = handCards.Count / 2;
                for (int i = 0; i < halfCount; i++)
                    AddToSelection(handCards[i], false);
                break;
            case SelectionPattern.SecondHalf:
                int startIndex = handCards.Count / 2;
                for (int i = startIndex; i < handCards.Count; i++)
                    AddToSelection(handCards[i], false);
                break;
            case SelectionPattern.Edges:
                if (handCards.Count > 0)
                {
                    AddToSelection(handCards[0], false);
                    if (handCards.Count > 1)
                        AddToSelection(handCards[handCards.Count - 1], false);
                }
                break;
            case SelectionPattern.Center:
                if (handCards.Count > 0)
                    AddToSelection(handCards[handCards.Count / 2], false);
                break;
        }
        
        SortSelectionByHandIndex();
    }
    
    public void ReorderSelectionBy(ReorderCriteria criteria)
    {
        if (!HasSelection) return;
        
        var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
        if (handLayoutManager == null) return;
        
        Debug.Log($"[SelectionManager] Reordering selection by: {criteria}");
        
        var selectedPositions = new List<int>();
        foreach (var card in _selectedCards)
            selectedPositions.Add(card.HandIndex());  // FIXED: Use Extension
        selectedPositions.Sort();
        
        if (selectedPositions.Count == 0) return;
        
        var sortedCards = SortCardsByCriteria(_selectedCards, criteria);
        
        for (int i = 0; i < sortedCards.Count && i < selectedPositions.Count; i++)
        {
            handLayoutManager.MoveCardToPosition(sortedCards[i], selectedPositions[i]);
        }
    }
    
    public void SelectBySpellPotential(float minScore = 0.5f)
    {
        var cardManager = CoreExtensions.GetManager<CardManager>();
        if (cardManager == null) return;
        
        var potential = cardManager.GetHandSpellPotential();
        if (potential.OverallScore >= minScore)
        {
            var handCards = cardManager.GetHandCards();
            var vowelCards = FilterCardsByType(handCards, CardType.Vowel);
            var consonantCards = FilterCardsByType(handCards, CardType.Consonant);
            
            ClearSelection();
            
            for (int i = 0; i < vowelCards.Count && i < 2; i++)
                AddToSelection(vowelCards[i], false);
            
            for (int i = 0; i < consonantCards.Count && i < 3; i++)
                AddToSelection(consonantCards[i], false);
            
            SortSelectionByHandIndex();
        }
    }
    
    public void SelectLongestWords()
    {
        var cardManager = CoreExtensions.GetManager<CardManager>();
        var handCards = cardManager?.GetHandCards();
        if (handCards == null) return;
        
        Debug.Log("[SelectionManager] Selecting cards with most letters");
        
        ClearSelection();
        var sortedByLetters = SortCardsByLetterCount(handCards);
        
        for (int i = 0; i < sortedByLetters.Count && i < 3; i++)
        {
            AddToSelection(sortedByLetters[i], false);
        }
        SortSelectionByHandIndex();
    }
    
    // === CARD STATE HELPERS ===
    
    private bool GetCardIsSelected(Card card)
    {
        if (card == null) return false;
        
        // Try reflection-based approach for compatibility
        var isSelectedProperty = card.GetType().GetProperty("IsSelected");
        if (isSelectedProperty != null && isSelectedProperty.CanRead)
        {
            var value = isSelectedProperty.GetValue(card);
            return value is bool boolValue && boolValue;
        }
        
        var currentStateProperty = card.GetType().GetProperty("CurrentState");
        if (currentStateProperty != null && currentStateProperty.CanRead)
        {
            var state = currentStateProperty.GetValue(card);
            return state != null && state.ToString() == "Selected";
        }
        
        return false;
    }
    
    private void SetCardSelected(Card card, bool selected)
    {
        if (card == null) return;
        
        try
        {
            if (selected)
            {
                var selectMethod = card.GetType().GetMethod("Select");
                selectMethod?.Invoke(card, null);
            }
            else
            {
                var deselectMethod = card.GetType().GetMethod("Deselect");
                deselectMethod?.Invoke(card, null);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SelectionManager] Failed to set card selection: {ex.Message}");
        }
    }
    
    // === EVENT HANDLERS ===
    
    private void HandleCardSelected(Card card)
    {
        if (_isUpdatingSelection) return;
        if (!_selectedCards.Contains(card))
            AddToSelection(card);
    }
    
    private void HandleCardDeselected(Card card)
    {
        if (_isUpdatingSelection) return;
        RemoveFromSelection(card);
    }
    
    private void HandleCardHighlighted(Card card)
    {
        if (_isUpdatingHighlight) return;
        if (!_highlightedCards.Contains(card))
            AddToHighlight(card);
    }
    
    private void HandleCardUnhighlighted(Card card)
    {
        if (_isUpdatingHighlight) return;
        RemoveFromHighlight(card);
    }
    
    // === HELPER METHODS ===
    
    private void SortSelectionByHandIndex()
    {
        if (!preserveHandOrder) return;
        _selectedCards.Sort((card1, card2) => 
            card1.HandIndex().CompareTo(card2.HandIndex()));  // FIXED: Use Extension
    }
    
    private List<Card> SortCardsByCriteria(List<Card> cards, ReorderCriteria criteria)
    {
        var result = new List<Card>(cards);
        
        switch (criteria)
        {
            case ReorderCriteria.Name:
                result.Sort((a, b) => string.Compare(a.GetCardName(), b.GetCardName()));
                break;
            case ReorderCriteria.Tier:
                result.Sort((a, b) => a.GetTier().CompareTo(b.GetTier()));
                break;
            case ReorderCriteria.Type:
                result.Sort((a, b) => a.GetCardType().CompareTo(b.GetCardType()));
                break;
            case ReorderCriteria.LetterCount:
                result.Sort((a, b) => b.GetLetters().Length.CompareTo(a.GetLetters().Length));
                break;
            case ReorderCriteria.Random:
                for (int i = 0; i < result.Count; i++)
                {
                    var temp = result[i];
                    int randomIndex = Random.Range(i, result.Count);
                    result[i] = result[randomIndex];
                    result[randomIndex] = temp;
                }
                break;
        }
        
        return result;
    }
    
    private List<Card> FilterCardsByType(List<Card> cards, CardType cardType)
    {
        var result = new List<Card>();
        foreach (var card in cards)
        {
            if (card.GetCardType() == cardType)
                result.Add(card);
        }
        return result;
    }
    
    private List<Card> SortCardsByLetterCount(List<Card> cards)
    {
        var result = new List<Card>(cards);
        result.Sort((a, b) => b.GetLetters().Length.CompareTo(a.GetLetters().Length));
        return result;
    }
    
    private Card FindCardByHandIndex(List<Card> cards, int index)
    {
        foreach (var card in cards)
        {
            if (card.HandIndex() == index)  // FIXED: Use Extension
                return card;
        }
        return null;
    }
    
    // === PUBLIC PROCESSING METHODS ===
    
    public void ProcessSelectedInOrder(System.Action<Card> action)
    {
        SortSelectionByHandIndex();
        var cardsToProcess = new List<Card>(_selectedCards);
        foreach (var card in cardsToProcess)
        {
            action?.Invoke(card);
        }
    }
    
    public void ProcessHighlightedInOrder(System.Action<Card> action)
    {
        var sorted = new List<Card>(_highlightedCards);
        sorted.Sort((a, b) => a.HandIndex().CompareTo(b.HandIndex()));  // FIXED: Use Extension
        
        foreach (var card in sorted)
        {
            action?.Invoke(card);
        }
    }
    
    public void BatchOperation(System.Action<List<Card>> operation)
    {
        if (!HasSelection) return;
        var cardsToProcess = new List<Card>(_selectedCards);
        operation(cardsToProcess);
    }
    
#if UNITY_EDITOR
    [ContextMenu("Debug Selection State")]
    public void DebugSelectionState()
    {
        Debug.Log($"[SelectionManager] Selected: {_selectedCards.Count}, Highlighted: {_highlightedCards.Count}");
        
        var selectedNames = new List<string>();
        foreach (var card in _selectedCards)
            selectedNames.Add(card.GetCardName());
        
        var highlightedNames = new List<string>();
        foreach (var card in _highlightedCards)
            highlightedNames.Add(card.GetCardName());
        
        Debug.Log($"Selected Cards: {string.Join(", ", selectedNames.ToArray())}");
        Debug.Log($"Highlighted Cards: {string.Join(", ", highlightedNames.ToArray())}");
    }
#endif
}

// Enums
public enum SelectionPattern
{
    EveryOther,
    FirstHalf,
    SecondHalf,
    Edges,
    Center,
    Random
}

public enum ReorderCriteria
{
    Name,
    Tier,
    Type,
    LetterCount,
    Random
}