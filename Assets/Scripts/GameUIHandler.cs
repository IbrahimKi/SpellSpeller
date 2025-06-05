using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameUIHandler : MonoBehaviour
{
    [Header("Resource Displays")]
    [SerializeField] private TextMeshProUGUI lifeText;
    [SerializeField] private Slider lifeSlider;
    [SerializeField] private TextMeshProUGUI creativityText;
    [SerializeField] private Slider creativitySlider;
    [SerializeField] private TextMeshProUGUI deckText;
    
    [Header("Card Play UI")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private Button playButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private Button drawButton;
    [SerializeField] private Button discardButton;
    
    [Header("Combat UI")]
    [SerializeField] private Button endTurnButton;
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private TextMeshProUGUI turnPhaseText; // NEW: Show current phase
    
    [Header("Settings")]
    [SerializeField] private bool showPercentages = true;
    [SerializeField] private Color healthLowColor = Color.red;
    [SerializeField] private Color healthNormalColor = Color.white;
    [SerializeField] private float healthLowThreshold = 0.25f;
    [SerializeField] private int discardCost = 1;
    
    private void Start()
    {
        StartCoroutine(WaitForManagersAndSetup());
    }
    
    private System.Collections.IEnumerator WaitForManagersAndSetup()
    {
        while (!GameManager.HasInstance || !GameManager.Instance.IsInitialized)
        {
            yield return new WaitForSeconds(0.1f);
        }
    
        SetupEventListeners();
        RefreshAllDisplays();
    }
    
    private void SetupEventListeners()
    {
        // Combat Manager Events - IMPROVED: More granular turn events
        if (CombatManager.HasInstance)
        {
            CombatManager.OnLifeChanged += UpdateLifeDisplay;
            CombatManager.OnCreativityChanged += UpdateCreativityDisplay;
            CombatManager.OnDeckSizeChanged += UpdateDeckDisplay;
            CombatManager.OnTurnChanged += UpdateTurnDisplay;
            CombatManager.OnTurnPhaseChanged += UpdateTurnPhaseDisplay; // NEW
            CombatManager.OnCombatStarted += OnCombatStarted;
            CombatManager.OnCombatEnded += OnCombatEnded;
            
            // NEW: Granular turn events for better UI responsiveness
            CombatManager.OnPlayerTurnStarted += OnPlayerTurnStarted;
            CombatManager.OnPlayerTurnEnded += OnPlayerTurnEnded;
            CombatManager.OnEnemyTurnStarted += OnEnemyTurnStarted;
            CombatManager.OnEnemyTurnEnded += OnEnemyTurnEnded;
            CombatManager.OnTurnTransitionStarted += OnTurnTransitionStarted;
            CombatManager.OnTurnTransitionCompleted += OnTurnTransitionCompleted;
        }
        
        // Card Manager Events
        if (CardManager.HasInstance)
        {
            CardManager.OnSelectionChanged += UpdateCardPlayUI;
        }
        
        // Spellcast Events
        if (SpellcastManager.HasInstance)
        {
            SpellcastManager.OnSpellFound += OnSpellFound;
            SpellcastManager.OnSpellNotFound += OnSpellNotFound;
            SpellcastManager.OnComboUpdated += UpdateComboDisplay;
        }
        
        // Setup Button Listeners - IMPROVED: Better turn button integration
        SetupButtonListeners();
    }
    
    private void SetupButtonListeners()
    {
        // Combat UI Buttons - IMPROVED: Direct integration with CombatManager's CanEndTurn
        if (endTurnButton != null && CombatManager.HasInstance)
        {
            endTurnButton.onClick.RemoveAllListeners();
            endTurnButton.onClick.AddListener(() => {
                if (CombatManager.Instance.CanEndTurn)
                {
                    Debug.Log("[GameUIHandler] End Turn button clicked - executing EndPlayerTurn");
                    CombatManager.Instance.EndPlayerTurn();
                }
                else
                {
                    Debug.LogWarning($"[GameUIHandler] Cannot end turn - Phase: {CombatManager.Instance.CurrentPhase}, Processing: {CombatManager.Instance.IsProcessingTurn}");
                }
            });
        }
        
        // Card Play Buttons
        if (playButton != null && SpellcastManager.HasInstance)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(() => SpellcastManager.Instance.PlaySelectedCards());
        }
        
        if (clearButton != null && SpellcastManager.HasInstance)
        {
            clearButton.onClick.RemoveAllListeners();
            clearButton.onClick.AddListener(() => SpellcastManager.Instance.ClearSelection());
        }
        
        if (drawButton != null && SpellcastManager.HasInstance)
        {
            drawButton.onClick.RemoveAllListeners();
            drawButton.onClick.AddListener(() => SpellcastManager.Instance.DrawCard());
        }
        
        if (discardButton != null)
        {
            discardButton.onClick.RemoveAllListeners();
            discardButton.onClick.AddListener(DiscardSelectedCard);
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (CombatManager.HasInstance)
        {
            CombatManager.OnLifeChanged -= UpdateLifeDisplay;
            CombatManager.OnCreativityChanged -= UpdateCreativityDisplay;
            CombatManager.OnDeckSizeChanged -= UpdateDeckDisplay;
            CombatManager.OnTurnChanged -= UpdateTurnDisplay;
            CombatManager.OnTurnPhaseChanged -= UpdateTurnPhaseDisplay;
            CombatManager.OnCombatStarted -= OnCombatStarted;
            CombatManager.OnCombatEnded -= OnCombatEnded;
            CombatManager.OnPlayerTurnStarted -= OnPlayerTurnStarted;
            CombatManager.OnPlayerTurnEnded -= OnPlayerTurnEnded;
            CombatManager.OnEnemyTurnStarted -= OnEnemyTurnStarted;
            CombatManager.OnEnemyTurnEnded -= OnEnemyTurnEnded;
            CombatManager.OnTurnTransitionStarted -= OnTurnTransitionStarted;
            CombatManager.OnTurnTransitionCompleted -= OnTurnTransitionCompleted;
        }
        
        if (CardManager.HasInstance)
        {
            CardManager.OnSelectionChanged -= UpdateCardPlayUI;
        }
        
        if (SpellcastManager.HasInstance)
        {
            SpellcastManager.OnSpellFound -= OnSpellFound;
            SpellcastManager.OnSpellNotFound -= OnSpellNotFound;
            SpellcastManager.OnComboUpdated -= UpdateComboDisplay;
        }
        
        // Clean up button listeners
        endTurnButton?.onClick.RemoveAllListeners();
        playButton?.onClick.RemoveAllListeners();
        clearButton?.onClick.RemoveAllListeners();
        drawButton?.onClick.RemoveAllListeners();
        discardButton?.onClick.RemoveAllListeners();
    }
    
    private void RefreshAllDisplays()
    {
        if (CombatManager.HasInstance)
        {
            var combat = CombatManager.Instance;
            UpdateLifeDisplay(combat.Life);
            UpdateCreativityDisplay(combat.Creativity);
            UpdateDeckDisplay(combat.DeckSize);
            UpdateTurnDisplay(combat.CurrentTurn);
            UpdateTurnPhaseDisplay(combat.CurrentPhase);
        }
        
        if (CardManager.HasInstance)
            UpdateCardPlayUI(CardManager.Instance.SelectedCards);
        
        UpdateAllButtons();
    }
    
    // IMPROVED: Consolidated button update logic
    private void UpdateAllButtons()
    {
        UpdateDrawButton();
        UpdateDiscardButton();
        UpdateEndTurnButton();
        UpdateCardPlayButtons();
    }
    
    private void UpdateLifeDisplay(Resource life)
    {
        if (lifeText != null)
        {
            lifeText.text = showPercentages 
                ? $"Life: {life.CurrentValue}/{life.MaxValue} ({life.Percentage:P0})"
                : $"Life: {life.CurrentValue}/{life.MaxValue}";
            lifeText.color = life.Percentage <= healthLowThreshold ? healthLowColor : healthNormalColor;
        }
        
        if (lifeSlider != null)
            lifeSlider.value = life.Percentage;
    }
    
    private void UpdateCreativityDisplay(Resource creativity)
    {
        if (creativityText != null)
            creativityText.text = $"Creativity: {creativity.CurrentValue}/{creativity.MaxValue}";
        
        if (creativitySlider != null)
            creativitySlider.value = creativity.Percentage;
            
        UpdateDiscardButton();
    }
    
    private void UpdateDeckDisplay(int deckSize)
    {
        if (deckText != null)
        {
            int discardSize = CombatManager.HasInstance ? CombatManager.Instance.DiscardSize : 0;
            deckText.text = $"Deck: {deckSize} | Discard: {discardSize}";
        }
        
        UpdateDrawButton();
        UpdateDiscardButton();
    }
    
    private void UpdateTurnDisplay(int turn)
    {
        if (turnText != null)
            turnText.text = $"Turn {turn}";
    }
    
    // NEW: Display current turn phase
    private void UpdateTurnPhaseDisplay(TurnPhase phase)
    {
        if (turnPhaseText != null)
        {
            string phaseText = phase switch
            {
                TurnPhase.PlayerTurn => "Your Turn",
                TurnPhase.EnemyTurn => "Enemy Turn",
                TurnPhase.TurnTransition => "Processing...",
                TurnPhase.CombatEnd => "Combat Ended",
                _ => phase.ToString()
            };
            
            turnPhaseText.text = phaseText;
            
            // Color coding for better UX
            turnPhaseText.color = phase switch
            {
                TurnPhase.PlayerTurn => Color.green,
                TurnPhase.EnemyTurn => Color.red,
                TurnPhase.TurnTransition => Color.yellow,
                TurnPhase.CombatEnd => Color.gray,
                _ => Color.white
            };
        }
        
        // Update turn button whenever phase changes
        UpdateEndTurnButton();
    }
    
    private void UpdateCardPlayUI(System.Collections.Generic.List<Card> selectedCards)
    {
        UpdateStatusDisplay(selectedCards);
        UpdateCardPlayButtons();
        UpdateDrawButton();
        UpdateDiscardButton();
    }
    
    private void UpdateCardPlayButtons()
    {
        bool hasCards = CardManager.HasInstance && CardManager.Instance.SelectedCards?.Count > 0;
        bool isPlayerTurn = CombatManager.HasInstance && CombatManager.Instance.IsPlayerTurn;
        
        if (playButton != null) playButton.interactable = hasCards && isPlayerTurn;
        if (clearButton != null) clearButton.interactable = hasCards && isPlayerTurn;
    }
    
    private void UpdateStatusDisplay(System.Collections.Generic.List<Card> selectedCards)
    {
        if (statusText == null) return;
        
        if (selectedCards?.Count > 0)
        {
            string letters = CardManager.GetLetterSequenceFromCards(selectedCards);
            statusText.text = $"Letters: {letters}";
            statusText.color = Color.white;
        }
        else
        {
            statusText.text = "Select cards to play\nTip: Double-click, hold, or Ctrl+click to play instantly";
            statusText.color = Color.gray;
        }
    }
    
    private void UpdateComboDisplay(string currentCombo)
    {
        if (comboText == null) return;
        
        if (string.IsNullOrEmpty(currentCombo))
        {
            comboText.text = "Combo: -";
            comboText.color = Color.gray;
        }
        else
        {
            comboText.text = $"Combo: {currentCombo}";
            comboText.color = Color.yellow;
        }
    }
    
    private void UpdateDrawButton()
    {
        if (drawButton != null)
        {
            bool canDraw = CardManager.HasInstance && !CardManager.Instance.IsHandFull &&
                          DeckManager.HasInstance && !DeckManager.Instance.IsDeckEmpty;
            bool isPlayerTurn = CombatManager.HasInstance && CombatManager.Instance.IsPlayerTurn;
            
            drawButton.interactable = canDraw && isPlayerTurn;
        }
    }
    
    private void UpdateDiscardButton()
    {
        if (discardButton != null)
        {
            bool hasSelectedCard = CardManager.HasInstance && CardManager.Instance.SelectedCards?.Count == 1;
            bool hasCreativity = CombatManager.HasInstance && CombatManager.Instance.CanSpendCreativity(discardCost);
            bool canDrawNew = DeckManager.HasInstance && DeckManager.Instance.GetTotalAvailableCards() > 0;
            bool isPlayerTurn = CombatManager.HasInstance && CombatManager.Instance.IsPlayerTurn;
            
            discardButton.interactable = hasSelectedCard && hasCreativity && canDrawNew && isPlayerTurn;
        }
    }
    
    // IMPROVED: More precise turn button logic
    private void UpdateEndTurnButton()
    {
        if (endTurnButton != null && CombatManager.HasInstance)
        {
            bool canEndTurn = CombatManager.Instance.CanEndTurn;
            endTurnButton.interactable = canEndTurn;
            
            // Update button text based on state
            var buttonText = endTurnButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                if (CombatManager.Instance.IsProcessingTurn)
                {
                    buttonText.text = "Processing...";
                }
                else if (CombatManager.Instance.IsPlayerTurn)
                {
                    buttonText.text = "End Turn";
                }
                else if (CombatManager.Instance.IsEnemyTurn)
                {
                    buttonText.text = "Enemy Turn";
                }
                else
                {
                    buttonText.text = "Wait...";
                }
            }
            
            Debug.Log($"[GameUIHandler] Turn Button Updated - CanEndTurn: {canEndTurn}, Phase: {CombatManager.Instance.CurrentPhase}, Processing: {CombatManager.Instance.IsProcessingTurn}");
        }
    }
    
    // EVENT HANDLERS - NEW: Granular turn event handling
    private void OnCombatStarted()
    {
        Debug.Log("[GameUIHandler] Combat started - updating all UI");
        UpdateAllButtons();
    }
    
    private void OnCombatEnded()
    {
        Debug.Log("[GameUIHandler] Combat ended - disabling combat UI");
        UpdateAllButtons();
    }
    
    private void OnPlayerTurnStarted(int turn)
    {
        Debug.Log($"[GameUIHandler] Player turn {turn} started - enabling player actions");
        UpdateAllButtons();
        
        // Optional: Flash the turn button or play sound
        if (endTurnButton != null)
        {
            endTurnButton.GetComponent<Image>().color = Color.green;
        }
    }
    
    private void OnPlayerTurnEnded(int turn)
    {
        Debug.Log($"[GameUIHandler] Player turn {turn} ended - disabling player actions");
        UpdateAllButtons();
    }
    
    private void OnEnemyTurnStarted(int turn)
    {
        Debug.Log($"[GameUIHandler] Enemy turn {turn} started - disabling player actions");
        UpdateAllButtons();
        
        // Optional: Change button color to indicate enemy turn
        if (endTurnButton != null)
        {
            endTurnButton.GetComponent<Image>().color = Color.red;
        }
    }
    
    private void OnEnemyTurnEnded(int turn)
    {
        Debug.Log($"[GameUIHandler] Enemy turn {turn} ended");
        UpdateAllButtons();
    }
    
    private void OnTurnTransitionStarted()
    {
        Debug.Log("[GameUIHandler] Turn transition started - disabling all actions");
        UpdateAllButtons();
        
        // Optional: Show processing indicator
        if (endTurnButton != null)
        {
            endTurnButton.GetComponent<Image>().color = Color.yellow;
        }
    }
    
    private void OnTurnTransitionCompleted()
    {
        Debug.Log("[GameUIHandler] Turn transition completed - updating UI");
        UpdateAllButtons();
        
        // Reset button color
        if (endTurnButton != null)
        {
            endTurnButton.GetComponent<Image>().color = Color.white;
        }
    }
    
    private void DiscardSelectedCard()
    {
        if (!CardManager.HasInstance || !CombatManager.HasInstance || !DeckManager.HasInstance)
            return;
            
        var selectedCards = CardManager.Instance.SelectedCards;
        if (selectedCards?.Count != 1)
            return;
            
        if (!CombatManager.Instance.CanSpendCreativity(discardCost))
            return;
            
        var cardToDiscard = selectedCards[0];
        
        // Spend creativity
        CombatManager.Instance.SpendCreativity(discardCost);
        
        // Add to discard pile
        DeckManager.Instance.DiscardCard(cardToDiscard.CardData);
        
        // FIXED: Use DiscardCard instead of RemoveCardFromHand
        CardManager.Instance.DiscardCard(cardToDiscard);
        
        // Draw new card
        var newCardData = DeckManager.Instance.DrawCard();
        if (newCardData != null)
        {
            CardManager.Instance.SpawnCard(newCardData, null, true);
        }
    }
    
    private void OnSpellFound(SpellAsset spell, string usedLetters)
    {
        if (statusText != null)
        {
            statusText.text = $"Spell: {spell.SpellName}!";
            statusText.color = Color.green;
            CancelInvoke(nameof(ResetStatusText));
            Invoke(nameof(ResetStatusText), 2f);
        }
    }
    
    private void OnSpellNotFound(string attemptedLetters)
    {
        if (statusText != null)
        {
            statusText.text = "No spell found";
            statusText.color = Color.red;
            CancelInvoke(nameof(ResetStatusText));
            Invoke(nameof(ResetStatusText), 1.5f);
        }
    }
    
    private void ResetStatusText()
    {
        if (statusText != null)
        {
            statusText.color = Color.white;
            if (CardManager.HasInstance)
                UpdateCardPlayUI(CardManager.Instance.SelectedCards);
        }
    }
}