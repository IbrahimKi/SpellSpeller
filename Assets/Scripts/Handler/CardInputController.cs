using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using CardSystem.Extensions;
using GameSystem.Extensions;
using Handler;

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
    
    // Last selection tracking
    private List<Card> _lastSelection = new List<Card>();
    private bool _hasStoredSelection = false;
    
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
    
    void Start()
    {
        // Subscribe to selection change events to apply hover effects
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        if (selectionManager != null)
        {
            SelectionManager.OnSelectionChanged += OnSelectionChangedForHover;
        }
        
        // Initialize last selection tracking
        _lastSelection = new List<Card>();
    }
    
    void Update()
    {
        if (enableKeyboardShortcuts)
            HandleKeyboardInput();
        
        HandleMouseInput();
    }

    // === HOVER EFFECT MANAGEMENT ===
    private void OnSelectionChangedForHover(List<Card> selectedCards)
    {
        // Apply hover effect to all selected cards
        if (selectedCards != null)
        {
            foreach (var card in selectedCards)
            {
                if (card != null)
                {
                    var hoverHandler = card.GetComponent<CardHoverHandler>();
                    if (hoverHandler != null)
                    {
                        hoverHandler.UpdateVisualForState();
                    }
                    else
                    {
                        // Fallback: Apply visual effect directly
                        card.Highlight();
                    }
                }
            }
        }
    }

    // === LAST SELECTION TRACKING ===
    private void StoreLastSelection()
    {
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        if (selectionManager == null) return;
        
        _lastSelection.Clear();
        foreach (var card in selectionManager.SelectedCards)
        {
            _lastSelection.Add(card);
        }
        _hasStoredSelection = _lastSelection.Count > 0;
        
        if (_hasStoredSelection)
        {
            Debug.Log($"[CardInputController] Stored last selection: {_lastSelection.Count} cards");
        }
    }
    
    private void RestoreLastSelection()
    {
        if (!_hasStoredSelection || _lastSelection.Count == 0) 
        {
            Debug.Log("[CardInputController] No stored selection to restore");
            return;
        }
        
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        if (selectionManager == null) return;
        
        selectionManager.ClearSelection();
        
        int restoredCount = 0;
        foreach (var card in _lastSelection)
        {
            if (card != null && card.IsInHand())
            {
                selectionManager.AddToSelection(card);
                restoredCount++;
            }
        }
        
        Debug.Log($"[CardInputController] Restored {restoredCount}/{_lastSelection.Count} cards from last selection");
    }

void HandleKeyboardInput()
{
    // Input Detection mit kontinuierlicher Prüfung
    bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);  
    bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
    
    var selectionManager = CoreExtensions.GetManager<SelectionManager>();
    if (selectionManager == null) return;
    
    // Nur bei GetKeyDOWN verarbeiten, nicht bei GetKey
    bool anyArrowDown = Input.GetKeyDown(KeyCode.LeftArrow) || 
                       Input.GetKeyDown(KeyCode.RightArrow) || 
                       Input.GetKeyDown(KeyCode.UpArrow) || 
                       Input.GetKeyDown(KeyCode.DownArrow);
    
    // Debug nur bei tatsächlichem Input
    if (anyArrowDown)
    {
        Debug.Log($"[CardInputController] === INPUT DETECTED ===");
        Debug.Log($"[CardInputController] Modifiers - Alt: {alt}, Ctrl: {ctrl}, Shift: {shift}");
        Debug.Log($"[CardInputController] State - Selection: {selectionManager.HasSelection} ({selectionManager.SelectedCards.Count}), Highlight: {selectionManager.HasHighlight} ({selectionManager.HighlightedCards.Count})");
    }
    
    // === PRIORITY 1: REORDERING CONTROLS (Ctrl + Arrow Keys) ===
    if (enableKeyboardReordering && ctrl && selectionManager.HasSelection)
    {
        if (anyArrowDown)
        {
            Debug.Log("[CardInputController] >>> CTRL REORDERING MODE <<<");
        }
        
        if (CanReorder())
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Debug.Log("[CardInputController] CTRL+LEFT: Move selection left");
                if (shift) // Move to far left
                    MoveSelectionToEnd(CardMoveDirection.Left);
                else // Move one step left
                    MoveSelection(CardMoveDirection.Left);
                return; // WICHTIG: Verhindert weitere Verarbeitung
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                Debug.Log("[CardInputController] CTRL+RIGHT: Move selection right");
                if (shift) // Move to far right
                    MoveSelectionToEnd(CardMoveDirection.Right);
                else // Move one step right
                    MoveSelection(CardMoveDirection.Right);
                return;
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                Debug.Log("[CardInputController] CTRL+UP: Move to center");
                MoveSelectionToCenter();
                return;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                Debug.Log("[CardInputController] CTRL+DOWN: Shuffle selection");
                ShuffleSelection();
                return;
            }
        }
        else if (anyArrowDown)
        {
            Debug.Log($"[CardInputController] CTRL reordering blocked - Cooldown: {Time.time - _lastReorderTime < reorderCooldown}, IsReordering: {_isReordering}");
            return; // Block weitere Verarbeitung auch wenn Cooldown aktiv
        }
    }
    
    // === PRIORITY 2: SHIFT-MODIFIER (Range selection & extension) ===
    else if (shift && !ctrl)
    {
        if (anyArrowDown)
        {
            Debug.Log("[CardInputController] >>> SHIFT RANGE SELECTION MODE <<<");
        }
        
        if (selectionManager.HasSelection)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Debug.Log("[CardInputController] SHIFT+LEFT: Extend selection left");
                ExtendSelectionLeft();
                return;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                Debug.Log("[CardInputController] SHIFT+RIGHT: Extend selection right");
                ExtendSelectionRight();
                return;
            }
        }
        else
        {
            // Ohne bestehende Selection - Range Selection starten
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Debug.Log("[CardInputController] SHIFT+LEFT: Start range selection left");
                SelectAdjacentCard(CardMoveDirection.Left, true);
                return;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                Debug.Log("[CardInputController] SHIFT+RIGHT: Start range selection right");
                SelectAdjacentCard(CardMoveDirection.Right, true);
                return;
            }
        }
    }
    
    // === PRIORITY 3: OHNE MODIFIER - NAVIGATION UND SELECTION MOVEMENT ===
    else if (!ctrl && !shift && !alt)
    {
        // === 3A: WENN KARTEN SELECTED SIND (aber NICHT highlighted) ===
        if (selectionManager.HasSelection && !selectionManager.HasHighlight)
        {
            if (anyArrowDown)
            {
                Debug.Log($"[CardInputController] >>> SELECTED MODE ({selectionManager.SelectedCards.Count} cards) <<<");
            }
            
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                // Up arrow HIGHLIGHTS selected cards (spielt sie NICHT!)
                Debug.Log("[CardInputController] UP: Selected → Highlighted");
                selectionManager.HighlightSelection();
                return;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                // Down arrow deselects all and stores selection
                Debug.Log("[CardInputController] DOWN: Storing and clearing selection");
                StoreLastSelection();
                selectionManager.ClearSelection();
                return;
            }
            // LEFT/RIGHT ohne Modifier - "Move Selection" (behält Anzahl bei)
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Debug.Log("[CardInputController] LEFT: Move selection left (same count)");
                MoveSelectionLeft();
                return;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                Debug.Log("[CardInputController] RIGHT: Move selection right (same count)");
                MoveSelectionRight();
                return;
            }
        }
        
        // === 3B: WENN KARTEN HIGHLIGHTED SIND ===
        else if (selectionManager.HasHighlight)
        {
            if (anyArrowDown)
            {
                Debug.Log($"[CardInputController] >>> HIGHLIGHTED MODE ({selectionManager.HighlightedCards.Count} cards) <<<");
            }
            
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                // Up arrow PLAYS highlighted cards
                Debug.Log("[CardInputController] UP: Playing highlighted cards");
                PlayHighlightedCards();
                return;
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                // Left arrow returns highlighted to deck
                Debug.Log("[CardInputController] LEFT: Returning highlighted to deck");
                ReturnHighlightedToDeck();
                return;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                // Right arrow discards highlighted
                Debug.Log("[CardInputController] RIGHT: Discarding highlighted cards");
                DiscardHighlightedCards();
                return;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                // Down arrow clears highlight (zurück zu selected)
                Debug.Log("[CardInputController] DOWN: Highlighted → Selected (restored)");
                var highlightedCards = new List<Card>();
                foreach (var card in selectionManager.HighlightedCards)
                {
                    highlightedCards.Add(card);
                }
                
                selectionManager.ClearHighlight();
                
                // Restore as selected
                foreach (var card in highlightedCards)
                {
                    if (card != null && card.IsInHand())
                    {
                        selectionManager.AddToSelection(card);
                    }
                }
                
                return;
            }
        }
        
        // === 3C: NAVIGATION CONTROLS (Keine selection/highlight) ===
        else if (!selectionManager.HasSelection && !selectionManager.HasHighlight)
        {
            if (anyArrowDown)
            {
                Debug.Log("[CardInputController] >>> NAVIGATION MODE (no selection) <<<");
            }
            
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Debug.Log("[CardInputController] LEFT: Select single card left");
                SelectAdjacentCard(CardMoveDirection.Left, false);
                return;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                Debug.Log("[CardInputController] RIGHT: Select single card right");
                SelectAdjacentCard(CardMoveDirection.Right, false);
                return;
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                // UP without selection - restore last selection
                Debug.Log("[CardInputController] UP: Restoring last selection");
                RestoreLastSelection();
                return;
            }
            // DOWN ohne Selection - ignorieren
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                Debug.Log("[CardInputController] DOWN without selection - ignoring");
                return;
            }
        }
    }
    
    // === PRIORITY 4: ALT-MODIFIER (Selection contraction) ===
    else if (alt && !shift && !ctrl && selectionManager.HasSelection)
    {
        if (anyArrowDown)
        {
            Debug.Log("[CardInputController] >>> ALT CONTRACTION MODE <<<");
            Debug.Log($"[CardInputController] Alt detected: {alt}, Shift: {shift}, Ctrl: {ctrl}");
        }
        
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            Debug.Log("[CardInputController] ALT+LEFT: Contract from right side");
            ContractSelectionRight();
            return;
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            Debug.Log("[CardInputController] ALT+RIGHT: Contract from left side");
            ContractSelectionLeft();
            return;
        }
    }
    
    // === ADDITIONAL ALT HANDLING (Debug) ===
    else if (alt && anyArrowDown)
    {
        Debug.Log($"[CardInputController] ALT detected but not processed - Alt: {alt}, HasSelection: {selectionManager.HasSelection}, Shift: {shift}, Ctrl: {ctrl}");
    }
    
    // === SPECIAL ACTIONS (unabhängig von Modifier, außer wenn schon verarbeitet) ===
    if (Input.GetKeyDown(KeyCode.Space))
    {
        Debug.Log("[CardInputController] SPACE: Draw card");
        TryDrawWithCost();
    }
    
    if (Input.GetKeyDown(KeyCode.Tab))
    {
        Debug.Log("[CardInputController] TAB: Quick spell select");
        QuickSelectForSpellBuilding();
    }
    
    // Escape: Clear all selections
    if (Input.GetKeyDown(KeyCode.Escape))
    {
        Debug.Log("[CardInputController] ESCAPE: Clear all");
        selectionManager.ClearSelection();
        selectionManager.ClearHighlight();
    }
}

    // === NEW: SELECTION MOVEMENT WITHOUT REORDERING ===
    
    private void MoveSelectionLeft()
    {
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        var cardManager = CoreExtensions.GetManager<CardManager>();
        
        if (selectionManager == null || cardManager == null) return;
        
        var handCards = cardManager.GetHandCards();
        if (handCards == null || handCards.Count == 0) return;
        
        var selectedCards = GetSelectedCardsList(selectionManager);
        int selectionCount = selectedCards.Count;
        
        if (selectionCount == 0) return;
        
        int leftmostIndex = GetMinHandIndex(selectedCards);
        int newStartIndex = Mathf.Max(0, leftmostIndex - 1);
        
        Debug.Log($"[CardInputController] Moving selection left: from {leftmostIndex} to {newStartIndex}, count: {selectionCount}");
        
        // Clear current selection
        selectionManager.ClearSelection();
        
        // Select new range
        SelectRangeAtIndex(newStartIndex, selectionCount);
    }
    
    private void MoveSelectionRight()
    {
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        var cardManager = CoreExtensions.GetManager<CardManager>();
        
        if (selectionManager == null || cardManager == null) return;
        
        var handCards = cardManager.GetHandCards();
        if (handCards == null || handCards.Count == 0) return;
        
        var selectedCards = GetSelectedCardsList(selectionManager);
        int selectionCount = selectedCards.Count;
        
        if (selectionCount == 0) return;
        
        int leftmostIndex = GetMinHandIndex(selectedCards);
        int maxPossibleStart = handCards.Count - selectionCount;
        int newStartIndex = Mathf.Min(maxPossibleStart, leftmostIndex + 1);
        
        Debug.Log($"[CardInputController] Moving selection right: from {leftmostIndex} to {newStartIndex}, count: {selectionCount}");
        
        // Clear current selection
        selectionManager.ClearSelection();
        
        // Select new range
        SelectRangeAtIndex(newStartIndex, selectionCount);
    }
    
    private void SelectRangeAtIndex(int startIndex, int count)
    {
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        var cardManager = CoreExtensions.GetManager<CardManager>();
        
        if (selectionManager == null || cardManager == null) return;
        
        var handCards = cardManager.GetHandCards();
        if (handCards == null) return;
        
        int actualCount = Mathf.Min(count, handCards.Count - startIndex);
        
        for (int i = 0; i < actualCount; i++)
        {
            var card = FindCardByHandIndex(handCards, startIndex + i);
            if (card != null)
            {
                selectionManager.AddToSelection(card);
                Debug.Log($"[CardInputController] Selected card at index {startIndex + i}: {card.GetCardName()}");
            }
        }
    }
    
    // REORDERING HELPER METHODS
    private bool CanReorder()
    {
        float timeSinceLastReorder = Time.time - _lastReorderTime;
        bool cooldownPassed = timeSinceLastReorder >= reorderCooldown;
    
        if (!cooldownPassed)
        {
            Debug.Log($"[CardInputController] Reorder on cooldown: {timeSinceLastReorder:F2}s / {reorderCooldown}s");
        }
    
        return cooldownPassed && !_isReordering;
    }

    
    private void MoveSelection(CardMoveDirection direction)
{
    if (!CanReorder()) 
    {
        Debug.Log("[CardInputController] MoveSelection blocked by cooldown");
        return;
    }

    Debug.Log($"[CardInputController] Starting move selection {direction}");

    _lastReorderTime = Time.time;
    _isReordering = true;

    var selectionManager = CoreExtensions.GetManager<SelectionManager>();
    var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
    var cardManager = CoreExtensions.GetManager<CardManager>();
    
    if (selectionManager == null || handLayoutManager == null || cardManager == null)
    {
        Debug.LogError("[CardInputController] Missing managers for MoveSelection!");
        _isReordering = false;
        return;
    }

    try
    {
        var selectedCards = GetSelectedCardsList(selectionManager);
        var handCards = cardManager.GetHandCards();
        
        if (selectedCards.Count == 0 || handCards == null || handCards.Count <= 1)
        {
            Debug.Log("[CardInputController] Nothing to move");
            return;
        }

        // FIXED: Manuelle Implementierung statt SelectionManager.MoveSelection()
        if (direction == CardMoveDirection.Left)
        {
            MoveSelectionOneStepLeft(selectedCards, handCards, handLayoutManager);
        }
        else if (direction == CardMoveDirection.Right)
        {
            MoveSelectionOneStepRight(selectedCards, handCards, handLayoutManager);
        }

        Debug.Log($"[CardInputController] Successfully moved selection {direction}");
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"[CardInputController] Failed to move selection: {ex.Message}");
    }
    finally
    {
        _isReordering = false;
    }

    if (visualReorderFeedback)
    {
        StartCoroutine(ReorderFeedback(direction));
    }
}

// === NEUE HILFSMETHODEN ===

    private void MoveSelectionOneStepLeft(List<Card> selectedCards, List<Card> handCards, HandLayoutManager handLayoutManager)
    {
        // Sortiere selected cards nach Hand-Index (aufsteigend)
        var sortedSelectedCards = new List<Card>(selectedCards);
        sortedSelectedCards.Sort((a, b) => a.HandIndex().CompareTo(b.HandIndex()));
    
        int leftmostIndex = sortedSelectedCards[0].HandIndex();
    
        if (leftmostIndex <= 0)
        {
            Debug.Log("[CardInputController] Already at leftmost position");
            return;
        }
    
        Debug.Log($"[CardInputController] Moving {selectedCards.Count} cards left from index {leftmostIndex}");
    
        // Finde die Karte, die verdrängt werden soll
        var cardToDisplace = FindCardByHandIndex(handCards, leftmostIndex - 1);
        if (cardToDisplace == null)
        {
            Debug.LogWarning("[CardInputController] No card to displace found");
            return;
        }
    
        // KORREKTE REIHENFOLGE: Von rechts nach links bewegen um Überschreibungen zu vermeiden
    
        // 1. Verdrängte Karte temporär "parken"
        int rightmostSelectedIndex = sortedSelectedCards[sortedSelectedCards.Count - 1].HandIndex();
    
        // 2. Alle selected cards um 1 nach links
        for (int i = 0; i < sortedSelectedCards.Count; i++)
        {
            var selectedCard = sortedSelectedCards[i];
            int newIndex = selectedCard.HandIndex() - 1;
        
            Debug.Log($"[CardInputController] Moving {selectedCard.GetCardName()} from {selectedCard.HandIndex()} to {newIndex}");
            handLayoutManager.MoveCardToPosition(selectedCard, newIndex);
        }
    
        // 3. Verdrängte Karte an rightmost position
        Debug.Log($"[CardInputController] Moving displaced card {cardToDisplace.GetCardName()} to {rightmostSelectedIndex}");
        handLayoutManager.MoveCardToPosition(cardToDisplace, rightmostSelectedIndex);
    }

    private void MoveSelectionOneStepRight(List<Card> selectedCards, List<Card> handCards, HandLayoutManager handLayoutManager)
    {
        // Sortiere selected cards nach Hand-Index (absteigend für rechts)
        var sortedSelectedCards = new List<Card>(selectedCards);
        sortedSelectedCards.Sort((a, b) => b.HandIndex().CompareTo(a.HandIndex()));
    
        int rightmostIndex = sortedSelectedCards[0].HandIndex();
    
        if (rightmostIndex >= handCards.Count - 1)
        {
            Debug.Log("[CardInputController] Already at rightmost position");
            return;
        }
    
        Debug.Log($"[CardInputController] Moving {selectedCards.Count} cards right from index {rightmostIndex}");
    
        // Finde die Karte, die verdrängt werden soll
        var cardToDisplace = FindCardByHandIndex(handCards, rightmostIndex + 1);
        if (cardToDisplace == null)
        {
            Debug.LogWarning("[CardInputController] No card to displace found");
            return;
        }
    
        // Finde leftmost selected index
        int leftmostSelectedIndex = GetMinHandIndex(selectedCards);
    
        // KORREKTE REIHENFOLGE: Von links nach rechts bewegen
    
        // 1. Alle selected cards um 1 nach rechts (von rechts nach links iterieren)
        for (int i = 0; i < sortedSelectedCards.Count; i++)
        {
            var selectedCard = sortedSelectedCards[i];
            int newIndex = selectedCard.HandIndex() + 1;
        
            Debug.Log($"[CardInputController] Moving {selectedCard.GetCardName()} from {selectedCard.HandIndex()} to {newIndex}");
            handLayoutManager.MoveCardToPosition(selectedCard, newIndex);
        }
    
        // 2. Verdrängte Karte an leftmost position
        Debug.Log($"[CardInputController] Moving displaced card {cardToDisplace.GetCardName()} to {leftmostSelectedIndex}");
        handLayoutManager.MoveCardToPosition(cardToDisplace, leftmostSelectedIndex);
    }    
    private void MoveSelectionToEnd(CardMoveDirection direction)
{
    if (!CanReorder()) 
    {
        Debug.Log("[CardInputController] MoveSelectionToEnd blocked by cooldown");
        return;
    }

    Debug.Log($"[CardInputController] Starting move selection to {direction} end");

    _lastReorderTime = Time.time;
    _isReordering = true;

    var selectionManager = CoreExtensions.GetManager<SelectionManager>();
    var handLayoutManager = CoreExtensions.GetManager<HandLayoutManager>();
    var cardManager = CoreExtensions.GetManager<CardManager>();
    
    if (selectionManager == null || handLayoutManager == null || cardManager == null)
    {
        Debug.LogError("[CardInputController] Missing managers for MoveSelectionToEnd!");
        _isReordering = false;
        return;
    }

    try
    {
        var selectedCards = GetSelectedCardsList(selectionManager);
        var handCards = cardManager.GetHandCards();
        
        if (selectedCards.Count == 0 || handCards == null)
        {
            Debug.Log("[CardInputController] Nothing to move to end");
            return;
        }

        // FIXED: Direkte Implementierung
        if (direction == CardMoveDirection.Left)
        {
            // Move to far left (position 0)
            Debug.Log("[CardInputController] Moving selection to far left");
            handLayoutManager.MoveCardsToPosition(selectedCards, 0);
        }
        else if (direction == CardMoveDirection.Right)
        {
            // Move to far right
            int targetIndex = handCards.Count - selectedCards.Count;
            Debug.Log($"[CardInputController] Moving selection to far right (index {targetIndex})");
            handLayoutManager.MoveCardsToPosition(selectedCards, targetIndex);
        }

        Debug.Log($"[CardInputController] Successfully moved selection to {direction} end");
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"[CardInputController] Failed to move selection to end: {ex.Message}");
    }
    finally
    {
        _isReordering = false;
    }

    if (visualReorderFeedback)
    {
        StartCoroutine(ReorderFeedback(direction, true));
    }
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
    
    // === SELECTION EXTENSION METHODS (UPDATED FROM PREVIOUS VERSIONS) ===
    
private void ExtendSelectionLeft()
{
    var selectionManager = CoreExtensions.GetManager<SelectionManager>();
    var cardManager = CoreExtensions.GetManager<CardManager>();
    
    if (selectionManager == null || cardManager == null) 
    {
        Debug.LogError("[CardInputController] Missing managers for ExtendSelectionLeft");
        return;
    }
    
    var handCards = cardManager.GetHandCards();
    if (handCards == null || handCards.Count == 0)
    {
        Debug.Log("[CardInputController] No hand cards available");
        return;
    }
    
    var selectedCards = GetSelectedCardsList(selectionManager);
    if (selectedCards.Count == 0) 
    {
        Debug.Log("[CardInputController] No cards selected to extend from");
        return;
    }
    
    Debug.Log($"[CardInputController] Extending selection left from {selectedCards.Count} cards");
    
    int leftmostIndex = GetMinHandIndex(selectedCards);
    Debug.Log($"[CardInputController] Leftmost selected index: {leftmostIndex}");
    
    if (leftmostIndex > 0)
    {
        var cardToAdd = FindCardByHandIndex(handCards, leftmostIndex - 1);
        if (cardToAdd != null)
        {
            Debug.Log($"[CardInputController] Adding card to left: {cardToAdd.GetCardName()} at index {leftmostIndex - 1}");
            selectionManager.AddToSelection(cardToAdd);
        }
        else
        {
            Debug.LogWarning($"[CardInputController] No card found at index {leftmostIndex - 1}");
        }
    }
    else
    {
        Debug.Log("[CardInputController] Already at leftmost position, cannot extend further");
    }
}

private void ExtendSelectionRight()
{
    var selectionManager = CoreExtensions.GetManager<SelectionManager>();
    var cardManager = CoreExtensions.GetManager<CardManager>();
    
    if (selectionManager == null || cardManager == null) 
    {
        Debug.LogError("[CardInputController] Missing managers for ExtendSelectionRight");
        return;
    }
    
    var handCards = cardManager.GetHandCards();
    if (handCards == null || handCards.Count == 0)
    {
        Debug.Log("[CardInputController] No hand cards available");
        return;
    }
    
    var selectedCards = GetSelectedCardsList(selectionManager);
    if (selectedCards.Count == 0) 
    {
        Debug.Log("[CardInputController] No cards selected to extend from");
        return;
    }
    
    Debug.Log($"[CardInputController] Extending selection right from {selectedCards.Count} cards");
    
    int rightmostIndex = GetMaxHandIndex(selectedCards);
    Debug.Log($"[CardInputController] Rightmost selected index: {rightmostIndex}");
    
    if (rightmostIndex < handCards.Count - 1)
    {
        var cardToAdd = FindCardByHandIndex(handCards, rightmostIndex + 1);
        if (cardToAdd != null)
        {
            Debug.Log($"[CardInputController] Adding card to right: {cardToAdd.GetCardName()} at index {rightmostIndex + 1}");
            selectionManager.AddToSelection(cardToAdd);
        }
        else
        {
            Debug.LogWarning($"[CardInputController] No card found at index {rightmostIndex + 1}");
        }
    }
    else
    {
        Debug.Log("[CardInputController] Already at rightmost position, cannot extend further");
    }
}

// === UPDATED: SELECTION CONTRACTION METHODS ===
private void ContractSelectionLeft()
{
    var selectionManager = CoreExtensions.GetManager<SelectionManager>();
    var selectedCards = GetSelectedCardsList(selectionManager);
    
    if (selectedCards.Count <= 1) 
    {
        Debug.Log("[CardInputController] Cannot contract - only one or no cards selected");
        return;
    }
    
    Debug.Log($"[CardInputController] Contracting selection from LEFT side, current count: {selectedCards.Count}");
    
    // NEUE LOGIK: Von LINKER Seite reduzieren (Remove leftmost)
    var leftmostCard = FindCardWithMinHandIndex(selectedCards);
    if (leftmostCard != null)
    {
        Debug.Log($"[CardInputController] Removing leftmost card: {leftmostCard.GetCardName()}");
        selectionManager.RemoveFromSelection(leftmostCard);
    }
    else
    {
        Debug.LogWarning("[CardInputController] Could not find leftmost card to contract");
    }
}

private void ContractSelectionRight()
{
    var selectionManager = CoreExtensions.GetManager<SelectionManager>();
    var selectedCards = GetSelectedCardsList(selectionManager);
    
    if (selectedCards.Count <= 1) 
    {
        Debug.Log("[CardInputController] Cannot contract - only one or no cards selected");
        return;
    }
    
    Debug.Log($"[CardInputController] Contracting selection from RIGHT side, current count: {selectedCards.Count}");
    
    // NEUE LOGIK: Von RECHTER Seite reduzieren (Remove rightmost)
    var rightmostCard = FindCardWithMaxHandIndex(selectedCards);
    if (rightmostCard != null)
    {
        Debug.Log($"[CardInputController] Removing rightmost card: {rightmostCard.GetCardName()}");
        selectionManager.RemoveFromSelection(rightmostCard);
    }
    else
    {
        Debug.LogWarning("[CardInputController] Could not find rightmost card to contract");
    }
}
    
    private void SelectAdjacentCard(CardMoveDirection direction, bool addToSelection)
{
    var selectionManager = CoreExtensions.GetManager<SelectionManager>();
    var cardManager = CoreExtensions.GetManager<CardManager>();
    
    if (selectionManager == null || cardManager == null) 
    {
        Debug.LogError("[CardInputController] Missing managers for SelectAdjacentCard");
        return;
    }
    
    var handCards = cardManager.GetHandCards();
    if (handCards == null || handCards.Count == 0)
    {
        Debug.Log("[CardInputController] No hand cards available for navigation");
        return;
    }
    
    Debug.Log($"[CardInputController] Navigating {direction}, addToSelection: {addToSelection}");
    
    var selectedCards = GetSelectedCardsList(selectionManager);
    int targetIndex = 0;
    
    if (selectedCards.Count > 0)
    {
        // Wenn bereits Karten selected sind, navigiere von der äußersten Karte
        int currentIndex = direction == CardMoveDirection.Left ? 
            GetMinHandIndex(selectedCards) : 
            GetMaxHandIndex(selectedCards);
        
        Debug.Log($"[CardInputController] Current reference index: {currentIndex}");
        
        targetIndex = direction == CardMoveDirection.Left ? 
            Mathf.Max(0, currentIndex - 1) : 
            Mathf.Min(handCards.Count - 1, currentIndex + 1);
    }
    else
    {
        // Keine Selection - starte bei erstem/letztem Index
        targetIndex = direction == CardMoveDirection.Left ? 0 : handCards.Count - 1;
        Debug.Log($"[CardInputController] No selection, starting at index: {targetIndex}");
    }
    
    var targetCard = FindCardByHandIndex(handCards, targetIndex);
    if (targetCard != null)
    {
        Debug.Log($"[CardInputController] Target card found: {targetCard.GetCardName()} at index {targetIndex}");
        
        if (addToSelection)
        {
            // Shift+Arrow: Zur aktuellen Selection hinzufügen (Range extend)
            Debug.Log("[CardInputController] Adding to selection (Shift+Arrow)");
            selectionManager.AddToSelection(targetCard);
        }
        else
        {
            // Nur Arrow: Neue Single-Selection
            Debug.Log("[CardInputController] New single selection (Arrow only)");
            selectionManager.ClearSelection();
            selectionManager.AddToSelection(targetCard);
        }
    }
    else
    {
        Debug.LogWarning($"[CardInputController] No card found at target index {targetIndex}");
        
        // Debug: Liste alle verfügbaren Indices
        Debug.Log("[CardInputController] Available card indices:");
        for (int i = 0; i < handCards.Count; i++)
        {
            var card = handCards[i];
            Debug.Log($"  Index {i}: {card.GetCardName()} (HandIndex: {card.HandIndex})");
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
    
        Debug.Log($"[CardInputController] Card clicked: {card.GetCardName()}, Selected: {GetCardIsSelected(card)}");
    
        if (isDoubleClick)
        {
            // Double click - highlight
            Debug.Log("[CardInputController] Double-click: Toggle highlight");
            if (IsCardHighlighted(card))
                selectionManager.RemoveFromHighlight(card);
            else
                selectionManager.AddToHighlight(card);
        }
        else if (shift && enableMultiSelect && _lastClickedCard != null)
        {
            // Shift click - range select
            Debug.Log("[CardInputController] Shift-click: Select range");
            selectionManager.SelectRange(_lastClickedCard, card);
        }
        else
        {
            // SIMPLE TOGGLE LOGIC
            bool isSelected = GetCardIsSelected(card);
        
            if (ctrl)
            {
                // Ohne Ctrl: Clear andere selections
                selectionManager.ClearSelection();
            }
        
            if (!isSelected)
            {
                Debug.Log("[CardInputController] Card not selected -> SELECT");
                selectionManager.AddToSelection(card);
            }
            else
            {
                Debug.Log("[CardInputController] Card selected -> DESELECT");
                selectionManager.RemoveFromSelection(card);
            }
        }
    
        _lastClickTime = Time.time;
        _lastClickedCard = card;
    }

    
    void HandleRightClick()
    {
        var eventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };
        
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        
        Card clickedCard = null;
        
        foreach (var result in results)
        {
            clickedCard = result.gameObject.GetComponent<Card>();
            if (clickedCard != null)
                break;
        }
        
        if (clickedCard != null)
        {
            // Right click on card - start drag without selection
            Debug.Log($"[CardInputController] Right-click drag: {clickedCard.GetCardName()}");
            StartCardDrag(clickedCard);
        }
        else
        {
            // Right click on empty space - clear all selections
            Debug.Log("[CardInputController] Right-click: Clear all selections");
            var selectionManager = CoreExtensions.GetManager<SelectionManager>();
            selectionManager?.ClearSelection();
            selectionManager?.ClearHighlight();
        }
    }
    
    private void StartCardDrag(Card card)
    {
        // Start dragging single card without selecting it
        var dragHandler = card.GetComponent<CardDragHandler>();
        if (dragHandler != null)
        {
            // Simulate drag start
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition,
                button = PointerEventData.InputButton.Right
            };
            
            dragHandler.OnBeginDrag(eventData);
        }
        else
        {
            Debug.LogWarning($"[CardInputController] No CardDragHandler found on {card.GetCardName()}");
        }
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
        int index = card.HandIndex();
        if (index >= 0 && index < min)
            min = index;
    }
    
    int result = min == int.MaxValue ? -1 : min;
    Debug.Log($"[CardInputController] GetMinHandIndex: {result} from {cards.Count} cards");
    return result;
}

private int GetMaxHandIndex(List<Card> cards)
{
    if (cards.Count == 0) return -1;
    
    int max = -1;
    foreach (var card in cards)
    {
        int index = card.HandIndex();
        if (index > max)
            max = index;
    }
    
    Debug.Log($"[CardInputController] GetMaxHandIndex: {max} from {cards.Count} cards");
    return max;
}

private Card FindCardWithMinHandIndex(List<Card> cards)
{
    if (cards.Count == 0) return null;
    
    Card minCard = null;
    int minIndex = int.MaxValue;
    
    foreach (var card in cards)
    {
        int index = card.HandIndex();
        if (index >= 0 && index < minIndex)
        {
            minIndex = index;
            minCard = card;
        }
    }
    
    Debug.Log($"[CardInputController] FindCardWithMinHandIndex: {minCard?.GetCardName() ?? "NULL"} at index {minIndex}");
    return minCard;
}

private Card FindCardWithMaxHandIndex(List<Card> cards)
{
    if (cards.Count == 0) return null;
    
    Card maxCard = null;
    int maxIndex = -1;
    
    foreach (var card in cards)
    {
        int index = card.HandIndex();
        if (index > maxIndex)
        {
            maxIndex = index;
            maxCard = card;
        }
    }
    
    Debug.Log($"[CardInputController] FindCardWithMaxHandIndex: {maxCard?.GetCardName() ?? "NULL"} at index {maxIndex}");
    return maxCard;
}

private Card FindCardByHandIndex(List<Card> cards, int targetIndex)
{
    foreach (var card in cards)
    {
        if (card.HandIndex() == targetIndex)
        {
            Debug.Log($"[CardInputController] Found card by index {targetIndex}: {card.GetCardName()}");
            return card;
        }
    }
    
    Debug.Log($"[CardInputController] No card found at index {targetIndex}");
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

    // === CLEANUP ===
    private void OnDestroy()
    {
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        if (selectionManager != null)
        {
            SelectionManager.OnSelectionChanged -= OnSelectionChangedForHover;
        }
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
    
    [ContextMenu("Test Alt Input")]
    private void TestAltInput()
    {
        bool leftAlt = Input.GetKey(KeyCode.LeftAlt);
        bool rightAlt = Input.GetKey(KeyCode.RightAlt);
        bool anyAlt = leftAlt || rightAlt;
        
        Debug.Log($"[CardInputController] === ALT TEST ===");
        Debug.Log($"Left Alt: {leftAlt}");
        Debug.Log($"Right Alt: {rightAlt}");
        Debug.Log($"Combined Alt: {anyAlt}");
        
        if (anyAlt)
        {
            Debug.Log("ALT IS DETECTED!");
        }
        else
        {
            Debug.Log("ALT NOT DETECTED - Try holding Alt while clicking this");
        }
    }
    
    [ContextMenu("Force Test Alt Contraction")]
    private void ForceTestAltContraction()
    {
        Debug.Log("[CardInputController] Force testing Alt contraction...");
        
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        if (selectionManager?.HasSelection == true)
        {
            ContractSelectionRight();
        }
        else
        {
            Debug.Log("No selection available for testing");
        }
    }
    
    [ContextMenu("Test Selection Movement")]
    private void TestSelectionMovement()
    {
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        if (selectionManager?.HasSelection == true)
        {
            Debug.Log("[CardInputController] Testing selection movement...");
            MoveSelectionRight();
        }
        else
        {
            Debug.Log("[CardInputController] No cards selected for movement test");
        }
    }
    
    [ContextMenu("Test Last Selection")]
    private void TestLastSelection()
    {
        if (_hasStoredSelection)
        {
            Debug.Log($"[CardInputController] Restoring {_lastSelection.Count} stored cards");
            RestoreLastSelection();
        }
        else
        {
            Debug.Log("[CardInputController] No stored selection to restore");
        }
    }
#endif
}