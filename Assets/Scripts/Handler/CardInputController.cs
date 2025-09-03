// NEUE DATEI: Assets/Scripts/Handler/CardInputController.cs
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
    
    [Header("Draw Settings")]
    [SerializeField] private int baseDrawCost = 1;
    
    // Click tracking
    private float _lastClickTime = 0f;
    private Card _lastClickedCard = null;
    private GameObject _lastClickedObject = null;
    
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
        
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        if (selectionManager == null) return;
        
        // Ctrl + Arrow Keys: Move selected cards
        if (ctrl && selectionManager.HasSelection)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (shift) // Double press simulation
                    selectionManager.MoveSelectionToEnd(CardMoveDirection.Left);
                else
                    selectionManager.MoveSelection(CardMoveDirection.Left);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (shift)
                    selectionManager.MoveSelectionToEnd(CardMoveDirection.Right);
                else
                    selectionManager.MoveSelection(CardMoveDirection.Right);
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                // Highlight selected cards
                selectionManager.HighlightSelection();
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                // Deselect all
                selectionManager.ClearSelection();
            }
        }
        
        // Highlighted card controls
        if (selectionManager.HasHighlight)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                // Play highlighted cards
                PlayHighlightedCards();
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                // Return highlighted to deck
                ReturnHighlightedToDeck();
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                // Discard highlighted
                DiscardHighlightedCards();
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                // Clear highlight
                selectionManager.ClearHighlight();
            }
        }
        
        // Space: Draw card with escalating cost
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryDrawWithCost();
        }
        
        // Escape: Clear all selections
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            selectionManager.ClearSelection();
            selectionManager.ClearHighlight();
        }
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
                
            // Check for deck area
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
    }
    
    void HandleCardClick(Card card)
    {
        float timeSinceLastClick = Time.time - _lastClickTime;
        bool isDoubleClick = (timeSinceLastClick <= doubleClickTime && _lastClickedCard == card);
        
        var selectionManager = CoreExtensions.GetManager<SelectionManager>();
        if (selectionManager == null) return;
        
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        
        if (isDoubleClick)
        {
            // Double click - toggle highlight
            if (card.IsHighlighted)
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
            if (!card.IsSelected)
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
    
    // Action methods
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
    
    // Called by CombatManager on turn end
    public void OnTurnEnd()
    {
        ResetDrawCost();
    }
}