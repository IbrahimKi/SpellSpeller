using System.Collections.Generic;
using System.Linq;
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
    
    // State
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
    }
    
    private void OnEnable()
    {
        Card.OnCardSelected += HandleCardSelected;
        Card.OnCardDeselected += HandleCardDeselected;
        Card.OnCardHighlighted += HandleCardHighlighted;
        Card.OnCardUnhighlighted += HandleCardUnhighlighted;
    }
    
    private void OnDisable()
    {
        Card.OnCardSelected -= HandleCardSelected;
        Card.OnCardDeselected -= HandleCardDeselected;
        Card.OnCardHighlighted -= HandleCardHighlighted;
        Card.OnCardUnhighlighted -= HandleCardUnhighlighted;
    }
    
    // Selection Management
    public void AddToSelection(Card card, bool preserveOrder = true)
    {
        if (card == null || !card.IsPlayable() || _selectedCards.Contains(card)) return;
        
        if (!allowMultiSelect)
            ClearSelection();
        else if (_selectedCards.Count >= maxSelectedCards)
            _selectedCards.RemoveAt(0);
        
        _selectedCards.Add(card);
        
        if (preserveOrder)
            SortSelectionByHandIndex();
        
        if (!card.IsSelected)
            card.Select();
        
        OnSelectionChanged?.Invoke(_selectedCards);
    }
    
    public void RemoveFromSelection(Card card)
    {
        if (_selectedCards.Remove(card))
        {
            if (card.IsSelected)
                card.Deselect();
            OnSelectionChanged?.Invoke(_selectedCards);
        }
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
        
        int fromIndex = GetCardHandIndex(fromCard);
        int toIndex = GetCardHandIndex(toCard);
        
        if (fromIndex == -1 || toIndex == -1) return;
        
        int start = Mathf.Min(fromIndex, toIndex);
        int end = Mathf.Max(fromIndex, toIndex);
        
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
        // FIX: Kopie erstellen um Modifikation während Iteration zu vermeiden
        var cardsToDeselect = new List<Card>(_selectedCards);
        foreach (var card in cardsToDeselect)
        {
            if (card != null && card.IsSelected)
                card.Deselect();
        }
        _selectedCards.Clear();
        OnSelectionChanged?.Invoke(_selectedCards);
    }
    
    // Highlight Management
    public void AddToHighlight(Card card)
    {
        if (card == null || _highlightedCards.Contains(card)) return;
        
        _highlightedCards.Add(card);
        if (!GetCardIsHighlighted(card))
            SetCardHighlighted(card, true);
        
        OnHighlightChanged?.Invoke(_highlightedCards);
    }
    
    public void RemoveFromHighlight(Card card)
    {
        if (_highlightedCards.Remove(card))
        {
            if (GetCardIsHighlighted(card))
                SetCardHighlighted(card, false);
            OnHighlightChanged?.Invoke(_highlightedCards);
        }
    }
    
    public void HighlightSelection()
    {
        // FIX: Kopie der selected cards erstellen BEVOR clear
        var cardsToHighlight = new List<Card>(_selectedCards);
        
        ClearHighlight();
        
        // Erst alle highlighten
        foreach (var card in cardsToHighlight)
        {
            AddToHighlight(card);
        }
        
        // Dann selection clearen
        ClearSelection();
    }
    
    public void ClearHighlight()
    {
        // FIX: Kopie erstellen für sichere Iteration
        var cardsToUnhighlight = new List<Card>(_highlightedCards);
        foreach (var card in cardsToUnhighlight)
        {
            if (card != null && GetCardIsHighlighted(card))
                SetCardHighlighted(card, false);
        }
        _highlightedCards.Clear();
        OnHighlightChanged?.Invoke(_highlightedCards);
    }
    
    // Drag Group Management
    public void StartGroupDrag(List<Card> cards)
    {
        _dragGroup = new List<Card>(cards);
        foreach (var card in _dragGroup)
        {
            SetCardDragging(card, true);
        }
        OnDragGroupChanged?.Invoke(_dragGroup);
    }
    
    public void EndGroupDrag()
    {
        foreach (var card in _dragGroup)
        {
            SetCardDragging(card, false);
        }
        _dragGroup.Clear();
        OnDragGroupChanged?.Invoke(_dragGroup);
    }
    
    // Movement Methods für Reordering
    public void MoveSelection(CardMoveDirection direction)
    {
        if (!HasSelection) return;
        
        var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
        if (handLayoutManager == null) return;
        
        SortSelectionByHandIndex();
        
        if (direction == CardMoveDirection.Left)
        {
            var leftmost = GetFirstCard(_selectedCards);
            if (leftmost != null)
            {
                int targetIndex = Mathf.Max(0, GetCardHandIndex(leftmost) - 1);
                handLayoutManager.MoveCardsToPosition(_selectedCards, targetIndex);
            }
        }
        else if (direction == CardMoveDirection.Right)
        {
            var rightmost = GetLastCard(_selectedCards);
            if (rightmost != null)
            {
                var handSize = CoreExtensions.GetManager<CardManager>()?.HandSize ?? 0;
                int targetIndex = Mathf.Min(handSize - 1, GetCardHandIndex(rightmost) + 1);
                handLayoutManager.MoveCardsToPosition(_selectedCards, targetIndex);
            }
        }
    }
    
    public void MoveSelectionToEnd(CardMoveDirection direction)
    {
        if (!HasSelection) return;
        
        var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
        if (handLayoutManager == null) return;
        
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
    
    // Smart Selection Methods
    public void SelectCardsByPattern(SelectionPattern pattern)
    {
        var cardManager = CoreExtensions.GetManager<CardManager>();
        var handCards = cardManager?.GetHandCards();
        if (handCards == null || handCards.Count == 0) return;
        
        ClearSelection();
        
        switch (pattern)
        {
            case SelectionPattern.EveryOther:
                for (int i = 0; i < handCards.Count; i += 2)
                {
                    AddToSelection(handCards[i], false);
                }
                break;
                
            case SelectionPattern.FirstHalf:
                int halfCount = handCards.Count / 2;
                for (int i = 0; i < halfCount; i++)
                {
                    AddToSelection(handCards[i], false);
                }
                break;
                
            case SelectionPattern.SecondHalf:
                int startIndex = handCards.Count / 2;
                for (int i = startIndex; i < handCards.Count; i++)
                {
                    AddToSelection(handCards[i], false);
                }
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
                {
                    int centerIndex = handCards.Count / 2;
                    AddToSelection(handCards[centerIndex], false);
                }
                break;
        }
        
        SortSelectionByHandIndex();
    }
    
    // Smart Reordering Methods
    public void ReorderSelectionBy(ReorderCriteria criteria)
    {
        if (!HasSelection) return;
        
        var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
        if (handLayoutManager == null) return;
        
        // Get the current positions of selected cards
        var selectedPositions = new List<int>();
        foreach (var card in _selectedCards)
        {
            selectedPositions.Add(GetCardHandIndex(card));
        }
        selectedPositions.Sort();
        
        if (selectedPositions.Count == 0) return;
        
        // Sort selected cards by criteria
        var sortedCards = SortCardsByCriteria(_selectedCards, criteria);
        
        // Place sorted cards back in the same positions
        for (int i = 0; i < sortedCards.Count && i < selectedPositions.Count; i++)
        {
            handLayoutManager.MoveCardToPosition(sortedCards[i], selectedPositions[i]);
        }
    }
    
    // Advanced Selection Queries
    public void SelectBySpellPotential(float minScore = 0.5f)
    {
        var cardManager = CoreExtensions.GetManager<CardManager>();
        if (cardManager == null) return;
        
        var potential = cardManager.GetHandSpellPotential();
        if (potential.OverallScore >= minScore)
        {
            // Select cards that contribute most to spell building
            var handCards = cardManager.GetHandCards();
            var vowelCards = FilterCardsByType(handCards, CardType.Vowel);
            var consonantCards = FilterCardsByType(handCards, CardType.Consonant);
            
            ClearSelection();
            
            // Take first 2 vowels
            for (int i = 0; i < vowelCards.Count && i < 2; i++)
            {
                AddToSelection(vowelCards[i], false);
            }
            
            // Take first 3 consonants
            for (int i = 0; i < consonantCards.Count && i < 3; i++)
            {
                AddToSelection(consonantCards[i], false);
            }
            
            SortSelectionByHandIndex();
        }
    }
    
    public void SelectLongestWords()
    {
        var cardManager = CoreExtensions.GetManager<CardManager>();
        var handCards = cardManager?.GetHandCards();
        if (handCards == null) return;
        
        ClearSelection();
        var sortedByLetters = SortCardsByLetterCount(handCards);
        
        // Take top 3
        for (int i = 0; i < sortedByLetters.Count && i < 3; i++)
        {
            AddToSelection(sortedByLetters[i], false);
        }
        SortSelectionByHandIndex();
    }
    
    // Helper Methods - FIX: Explicit implementations to avoid Linq issues
    private void SortSelectionByHandIndex()
    {
        if (!preserveHandOrder) return;
        
        // Manual sort to avoid Linq issues
        _selectedCards.Sort((card1, card2) => 
        {
            int index1 = GetCardHandIndex(card1);
            int index2 = GetCardHandIndex(card2);
            return index1.CompareTo(index2);
        });
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
                result.Sort((a, b) => b.GetLetters().Length.CompareTo(a.GetLetters().Length)); // Descending
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
        result.Sort((a, b) => b.GetLetters().Length.CompareTo(a.GetLetters().Length)); // Descending
        return result;
    }
    
    private Card FindCardByHandIndex(List<Card> cards, int index)
    {
        foreach (var card in cards)
        {
            if (GetCardHandIndex(card) == index)
                return card;
        }
        return null;
    }
    
    private Card GetFirstCard(List<Card> cards)
    {
        return cards.Count > 0 ? cards[0] : null;
    }
    
    private Card GetLastCard(List<Card> cards)
    {
        return cards.Count > 0 ? cards[cards.Count - 1] : null;
    }
    
    // Card Helper Methods - Compatible with both Extension and Direct methods
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
        
        // Use built-in HandIndex property
        return card.HandIndex;
    }
    
    private bool GetCardIsHighlighted(Card card)
    {
        if (card == null) return false;
        
        // Try direct property first
        var isHighlightedProperty = card.GetType().GetProperty("IsHighlighted");
        if (isHighlightedProperty != null && isHighlightedProperty.CanRead)
        {
            var value = isHighlightedProperty.GetValue(card);
            return value is bool boolValue && boolValue;
        }
        
        // Fallback to extension method approach
        // Since Card already has IsHighlighted property, use it directly
        return card.IsHighlighted;
    }
    
    private void SetCardHighlighted(Card card, bool highlighted)
    {
        if (card == null) return;
        
        // Try direct method first
        var highlightMethod = card.GetType().GetMethod(highlighted ? "Highlight" : "Unhighlight");
        if (highlightMethod != null)
        {
            highlightMethod.Invoke(card, null);
            return;
        }
        
        // Use methods to change state since we can't directly set CurrentState
        if (highlighted)
            card.Highlight();
        else
            card.Unhighlight();
        
        // Apply visual effect
        var outline = card.GetComponent<UnityEngine.UI.Outline>();
        if (outline == null && highlighted)
        {
            outline = card.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Color.yellow;
            outline.effectDistance = new Vector2(2, 2);
        }
        else if (outline != null)
        {
            outline.enabled = highlighted;
        }
        
        // Scale effect
        card.transform.localScale = highlighted ? Vector3.one * 1.02f : Vector3.one;
    }
    
    private void SetCardDragging(Card card, bool dragging)
    {
        if (card == null) return;
        
        // Try direct method first
        var dragMethod = card.GetType().GetMethod(dragging ? "StartDrag" : "EndDrag");
        if (dragMethod != null)
        {
            dragMethod.Invoke(card, null);
            return;
        }
        
        // Fallback implementation
        var canvasGroup = card.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = card.gameObject.AddComponent<CanvasGroup>();
        
        if (dragging)
        {
            canvasGroup.alpha = 0.8f;
            canvasGroup.blocksRaycasts = false;
            card.transform.localScale = Vector3.one * 1.05f;
        }
        else
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            card.transform.localScale = Vector3.one;
        }
    }
    
    private void HandleCardSelected(Card card)
    {
        if (!_selectedCards.Contains(card))
            AddToSelection(card);
    }
    
    private void HandleCardDeselected(Card card)
    {
        RemoveFromSelection(card);
    }
    
    private void HandleCardHighlighted(Card card)
    {
        if (!_highlightedCards.Contains(card))
            AddToHighlight(card);
    }
    
    private void HandleCardUnhighlighted(Card card)
    {
        RemoveFromHighlight(card);
    }
    
    // Process actions in order - FIX: Sichere Iteration mit ToList()
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
        sorted.Sort((a, b) => GetCardHandIndex(a).CompareTo(GetCardHandIndex(b)));
        
        foreach (var card in sorted)
        {
            action?.Invoke(card);
        }
    }
    
    // Batch operations
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
        {
            selectedNames.Add(card.GetCardName());
        }
        
        var highlightedNames = new List<string>();
        foreach (var card in _highlightedCards)
        {
            highlightedNames.Add(card.GetCardName());
        }
        
        Debug.Log($"Selected Cards: {string.Join(", ", selectedNames.ToArray())}");
        Debug.Log($"Highlighted Cards: {string.Join(", ", highlightedNames.ToArray())}");
    }
    
    [ContextMenu("Test Smart Selection")]
    public void TestSmartSelection()
    {
        Debug.Log("[SelectionManager] Testing smart selection patterns...");
        SelectCardsByPattern(SelectionPattern.EveryOther);
        DebugSelectionState();
    }
    
    [ContextMenu("Test Spell Potential Selection")]
    public void TestSpellPotentialSelection()
    {
        SelectBySpellPotential(0.3f);
        DebugSelectionState();
    }
#endif
}

// Enums für erweiterte Funktionalität
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