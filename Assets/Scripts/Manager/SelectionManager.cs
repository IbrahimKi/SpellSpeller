// FIXED VERSION: Assets/Scripts/Manager/SelectionManager.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
        
        int fromIndex = fromCard.HandIndex;
        int toIndex = toCard.HandIndex;
        
        if (fromIndex == -1 || toIndex == -1) return;
        
        int start = Mathf.Min(fromIndex, toIndex);
        int end = Mathf.Max(fromIndex, toIndex);
        
        ClearSelection();
        
        for (int i = start; i <= end; i++)
        {
            var card = handCards.FirstOrDefault(c => c.HandIndex == i);
            if (card != null)
                AddToSelection(card, false);
        }
        
        SortSelectionByHandIndex();
    }
    
    public void ClearSelection()
    {
        // FIX: Kopie erstellen um Modifikation während Iteration zu vermeiden
        var cardsToDeselect = _selectedCards.ToList();
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
        if (!card.IsHighlighted)
            card.Highlight();
        
        OnHighlightChanged?.Invoke(_highlightedCards);
    }
    
    public void RemoveFromHighlight(Card card)
    {
        if (_highlightedCards.Remove(card))
        {
            if (card.IsHighlighted)
                card.Unhighlight();
            OnHighlightChanged?.Invoke(_highlightedCards);
        }
    }
    
    public void HighlightSelection()
    {
        // FIX: Kopie der selected cards erstellen BEVOR clear
        var cardsToHighlight = _selectedCards.ToList();
        
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
        var cardsToUnhighlight = _highlightedCards.ToList();
        foreach (var card in cardsToUnhighlight)
        {
            if (card != null && card.IsHighlighted)
                card.Unhighlight();
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
            card?.StartDrag();
        }
        OnDragGroupChanged?.Invoke(_dragGroup);
    }
    
    public void EndGroupDrag()
    {
        foreach (var card in _dragGroup)
        {
            card?.EndDrag();
        }
        _dragGroup.Clear();
        OnDragGroupChanged?.Invoke(_dragGroup);
    }
    
    // Movement
    public void MoveSelection(CardMoveDirection direction)
    {
        if (!HasSelection) return;
        
        var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
        if (handLayoutManager == null) return;
        
        SortSelectionByHandIndex();
        
        if (direction == CardMoveDirection.Left)
        {
            var leftmost = _selectedCards.First();
            int targetIndex = Mathf.Max(0, leftmost.HandIndex - 1);
            handLayoutManager.MoveCardsToPosition(_selectedCards, targetIndex);
        }
        else if (direction == CardMoveDirection.Right)
        {
            var rightmost = _selectedCards.Last();
            var handSize = CoreExtensions.GetManager<CardManager>()?.HandSize ?? 0;
            int targetIndex = Mathf.Min(handSize - 1, rightmost.HandIndex + 1);
            handLayoutManager.MoveCardsToPosition(_selectedCards, targetIndex);
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
    
    // Helpers
    private void SortSelectionByHandIndex()
    {
        if (preserveHandOrder)
            _selectedCards = _selectedCards.OrderBy(c => c.HandIndex).ToList();
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
        var cardsToProcess = _selectedCards.ToList();
        foreach (var card in cardsToProcess)
        {
            action?.Invoke(card);
        }
    }
    
    public void ProcessHighlightedInOrder(System.Action<Card> action)
    {
        var sorted = _highlightedCards.OrderBy(c => c.HandIndex).ToList();
        foreach (var card in sorted)
        {
            action?.Invoke(card);
        }
    }
    
#if UNITY_EDITOR
    [ContextMenu("Debug Selection State")]
    public void DebugSelectionState()
    {
        Debug.Log($"[SelectionManager] Selected: {_selectedCards.Count}, Highlighted: {_highlightedCards.Count}");
        Debug.Log($"Selected Cards: {string.Join(", ", _selectedCards.Select(c => c.GetCardName()))}");
        Debug.Log($"Highlighted Cards: {string.Join(", ", _highlightedCards.Select(c => c.GetCardName()))}");
    }
#endif
}