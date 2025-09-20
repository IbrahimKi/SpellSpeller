using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

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
        
        // NEU: Reordering Controls (Alt + Arrow Keys)
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
        // Selection Controls (Ctrl + Arrow Keys) - when NOT using Alt
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
        
        // When cards are SELECTED (not highlighted) - Original logic
        if (selectionManager.HasSelection && !selectionManager.HasHighlight && !alt)
        {
            // Up arrow HIGHLIGHTS selected cards, doesn't play them
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                selectionManager.HighlightSelection();
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                // Down arrow deselects all
                selectionManager.ClearSelection();
            }
        }
        
        // When cards are HIGHLIGHTED - Original logic unchanged
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
        
        // NEU: Navigation Controls (Arrow Keys only, no modifiers)
        if (!ctrl && !alt && !selectionManager.HasHighlight)
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
        
        // Special actions (original)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryDrawWithCost();
        }
        
        // NEU: Tab for quick spell building
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            QuickSelectForSpellBuilding();
        }
        
        // Escape: Clear all selections (original)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            selectionManager.ClearSelection();
            selectionManager.ClearHighlight();
        }
    }
    
    // NEU: Reordering helper methods
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
    
    // NEU: Selection extension methods
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
    
    // NEU: Visual feedback coroutine
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
        
        // Could add screen shake, particle effects, etc.
    }
    
    private void QuickSelectForSpellBuilding()
    {
        var cardManager = CoreExtensions.GetManager<CardManager>();
        cardManager?.AutoSelectForSpellBuilding();
    }
    
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
        // NEU: Middle click for spell analysis
        else if (Input.GetMouseButtonDown(2))
        {
            HandleMiddleClick();
        }
    }
    
    // NEU: Middle click handler
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
            // NEU: Click on empty space - clear selection
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