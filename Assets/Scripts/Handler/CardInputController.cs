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
    
    // === PRIORITY 1: REORDERING CONTROLS (Alt + Arrow Keys) ===
    if (enableKeyboardReordering && alt && selectionManager.HasSelection)
    {
        if (anyArrowDown)
        {
            Debug.Log("[CardInputController] >>> ALT REORDERING MODE <<<");
        }
        
        if (CanReorder())
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Debug.Log("[CardInputController] ALT+LEFT: Move selection left");
                if (shift) // Move to far left
                    MoveSelectionToEnd(CardMoveDirection.Left);
                else // Move one step left
                    MoveSelection(CardMoveDirection.Left);
                return; // WICHTIG: Verhindert weitere Verarbeitung
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                Debug.Log("[CardInputController] ALT+RIGHT: Move selection right");
                if (shift) // Move to far right
                    MoveSelectionToEnd(CardMoveDirection.Right);
                else // Move one step right
                    MoveSelection(CardMoveDirection.Right);
                return;
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                Debug.Log("[CardInputController] ALT+UP: Move to center");
                MoveSelectionToCenter();
                return;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                Debug.Log("[CardInputController] ALT+DOWN: Shuffle selection");
                ShuffleSelection();
                return;
            }
        }
        else if (anyArrowDown)
        {
            Debug.Log($"[CardInputController] ALT reordering blocked - Cooldown: {Time.time - _lastReorderTime < reorderCooldown}, IsReordering: {_isReordering}");
            return; // Block weitere Verarbeitung auch wenn Cooldown aktiv
        }
    }
    
    // === PRIORITY 2: SELECTION EXTENSION (Ctrl + Arrow Keys) ===
    else if (ctrl && !alt && selectionManager.HasSelection)
    {
        if (anyArrowDown)
        {
            Debug.Log("[CardInputController] >>> CTRL SELECTION EXTENSION MODE <<<");
        }
        
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            if (shift) // Extend selection left
            {
                Debug.Log("[CardInputController] CTRL+SHIFT+LEFT: Extend selection left");
                ExtendSelectionLeft();
            }
            else // Contract selection from right
            {
                Debug.Log("[CardInputController] CTRL+LEFT: Contract selection from right");
                ContractSelectionRight();
            }
            return;
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (shift) // Extend selection right
            {
                Debug.Log("[CardInputController] CTRL+SHIFT+RIGHT: Extend selection right");
                ExtendSelectionRight();
            }
            else // Contract selection from left
            {
                Debug.Log("[CardInputController] CTRL+RIGHT: Contract selection from left");
                ContractSelectionLeft();
            }
            return;
        }
        // CTRL+UP/DOWN mit Selection - ignorieren oder andere Aktionen
        else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            Debug.Log("[CardInputController] CTRL+UP/DOWN with selection - ignoring");
            return;
        }
    }
    
    // === PRIORITY 3: MAIN LOGIC (Kein Alt, Kein Ctrl) ===
    else if (!alt && !ctrl)
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
                // Down arrow deselects all
                Debug.Log("[CardInputController] DOWN: Clearing selection");
                selectionManager.ClearSelection();
                return;
            }
            // LEFT/RIGHT mit Selection - Navigation mit Shift Support
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (shift)
                {
                    Debug.Log("[CardInputController] SHIFT+LEFT: Extend selection left (navigation style)");
                    SelectAdjacentCard(CardMoveDirection.Left, true);
                }
                else
                {
                    Debug.Log("[CardInputController] LEFT: Navigate left from selection");
                    SelectAdjacentCard(CardMoveDirection.Left, false);
                }
                return;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (shift)
                {
                    Debug.Log("[CardInputController] SHIFT+RIGHT: Extend selection right (navigation style)");
                    SelectAdjacentCard(CardMoveDirection.Right, true);
                }
                else
                {
                    Debug.Log("[CardInputController] RIGHT: Navigate right from selection");
                    SelectAdjacentCard(CardMoveDirection.Right, false);
                }
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
                Debug.Log("[CardInputController] DOWN: Highlighted → Cleared");
                selectionManager.ClearHighlight();
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
                if (shift) // Select range left
                {
                    Debug.Log("[CardInputController] SHIFT+LEFT: Start range selection left");
                    SelectAdjacentCard(CardMoveDirection.Left, true);
                }
                else // Select single card left
                {
                    Debug.Log("[CardInputController] LEFT: Select single card left");
                    SelectAdjacentCard(CardMoveDirection.Left, false);
                }
                return;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (shift) // Select range right
                {
                    Debug.Log("[CardInputController] SHIFT+RIGHT: Start range selection right");
                    SelectAdjacentCard(CardMoveDirection.Right, true);
                }
                else // Select single card right
                {
                    Debug.Log("[CardInputController] RIGHT: Select single card right");
                    SelectAdjacentCard(CardMoveDirection.Right, false);
                }
                return;
            }
            // UP/DOWN ohne Selection - ignorieren oder zukünftige Features
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                Debug.Log("[CardInputController] UP/DOWN without selection - ignoring");
                return;
            }
        }
    }
    
    // === PRIORITY 4: SPECIAL CASES (Alt/Ctrl ohne Selection) ===
    else if (alt && !selectionManager.HasSelection && anyArrowDown)
    {
        Debug.Log("[CardInputController] ALT without selection - ignoring");
        return;
    }
    else if (ctrl && !selectionManager.HasSelection && anyArrowDown)
    {
        Debug.Log("[CardInputController] CTRL without selection - ignoring");
        return;
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
        if (selectionManager == null)
        {
            Debug.LogError("[CardInputController] SelectionManager is null!");
            _isReordering = false;
            return;
        }
    
        try
        {
            selectionManager.MoveSelection(direction);
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
        if (selectionManager == null)
        {
            Debug.LogError("[CardInputController] SelectionManager is null!");
            _isReordering = false;
            return;
        }
    
        try
        {
            selectionManager.MoveSelectionToEnd(direction);
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
    
    // SELECTION EXTENSION METHODS
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

private void ContractSelectionLeft()
{
    var selectionManager = CoreExtensions.GetManager<SelectionManager>();
    var selectedCards = GetSelectedCardsList(selectionManager);
    
    if (selectedCards.Count <= 1) 
    {
        Debug.Log("[CardInputController] Cannot contract - only one or no cards selected");
        return;
    }
    
    Debug.Log($"[CardInputController] Contracting selection from left, current count: {selectedCards.Count}");
    
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
    
    Debug.Log($"[CardInputController] Contracting selection from right, current count: {selectedCards.Count}");
    
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
    
    // FIXED: HandleCardClick - Bessere Selection Logic
    void HandleCardClick(Card card)
    {
        float timeSinceLastClick = Time.time - _lastClickTime;
        bool isDoubleClick = (timeSinceLastClick <= doubleClickTime && _lastClickedCard == card);
        
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        if (selectionManager == null) return;
        
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        
        Debug.Log($"[CardInputController] Card clicked: {card.GetCardName()}, DoubleClick: {isDoubleClick}, Ctrl: {ctrl}, Shift: {shift}");
        
        if (isDoubleClick)
        {
            // Double click - toggle highlight DIRECTLY
            Debug.Log("[CardInputController] Double-click: Toggle highlight directly");
            if (IsCardHighlighted(card))
                selectionManager.RemoveFromHighlight(card);
            else
                selectionManager.AddToHighlight(card);
        }
        else if (shift && enableMultiSelect && _lastClickedCard != null)
        {
            // Shift click - select range
            Debug.Log("[CardInputController] Shift-click: Select range");
            selectionManager.SelectRange(_lastClickedCard, card);
        }
        else if (ctrl && enableMultiSelect)
        {
            // Ctrl click - toggle selection
            Debug.Log("[CardInputController] Ctrl-click: Toggle selection");
            selectionManager.ToggleCardSelection(card);
        }
        else
        {
            // Normal click - single select/deselect
            bool isSelected = GetCardIsSelected(card);
            
            if (!isSelected)
            {
                Debug.Log("[CardInputController] Normal click: Select card");
                selectionManager.ClearSelection();
                selectionManager.AddToSelection(card);
            }
            else
            {
                Debug.Log("[CardInputController] Normal click: Deselect card");
                selectionManager.RemoveFromSelection(card);
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
        int index = card.HandIndex;
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
        int index = card.HandIndex;
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
        int index = card.HandIndex;
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
        if (card.HandIndex == targetIndex)
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
    
    [ContextMenu("Test Alt Detection")]
    private void TestAltDetection()
    {
        Debug.Log($"Alt Keys: Left={Input.GetKey(KeyCode.LeftAlt)}, Right={Input.GetKey(KeyCode.RightAlt)}");
        Debug.Log($"Selection Manager Ready: {CoreExtensions.IsManagerReady<SelectionManager>()}");
        
        var sm = CoreExtensions.GetManager<SelectionManager>();
        if (sm != null)
        {
            Debug.Log($"Has Selection: {sm.HasSelection}, Count: {sm.SelectedCards.Count}");
        }
    }
#endif
}