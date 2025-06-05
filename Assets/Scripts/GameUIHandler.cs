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
    
    [Header("Settings")]
    [SerializeField] private bool showPercentages = true;
    [SerializeField] private Color healthLowColor = Color.red;
    [SerializeField] private Color healthNormalColor = Color.white;
    [SerializeField] private float healthLowThreshold = 0.25f;
    [SerializeField] private int discardCost = 1;
    
    private void Start()
    {
        // Warte auf Manager-Initialisierung
        StartCoroutine(WaitForManagersAndSetup());
    }
    
    private System.Collections.IEnumerator WaitForManagersAndSetup()
    {
        // Warte auf GameManager
        while (!GameManager.HasInstance || !GameManager.Instance.IsInitialized)
        {
            yield return new WaitForSeconds(0.1f);
        }
    
        SetupEventListeners();
        RefreshAllDisplays();
    }
    
    private void SetupEventListeners()
    {
        // Combat Manager Events
        CombatManager.OnLifeChanged += UpdateLifeDisplay;
        CombatManager.OnCreativityChanged += UpdateCreativityDisplay;
        CombatManager.OnDeckSizeChanged += UpdateDeckDisplay;
        
        // Card Manager Events
        CardManager.OnSelectionChanged += UpdateCardPlayUI;
        
        // Spellcast Events
        if (SpellcastManager.HasInstance)
        {
            SpellcastManager.OnSpellFound += OnSpellFound;
            SpellcastManager.OnSpellNotFound += OnSpellNotFound;
            SpellcastManager.OnComboUpdated += UpdateComboDisplay;
            
            if (playButton) playButton.onClick.AddListener(() => SpellcastManager.Instance?.PlaySelectedCards());
            if (clearButton) clearButton.onClick.AddListener(() => SpellcastManager.Instance?.ClearSelection());
            if (drawButton) drawButton.onClick.AddListener(() => SpellcastManager.Instance?.DrawCard());
        }
        
        // Discard Button
        if (discardButton) discardButton.onClick.AddListener(DiscardSelectedCard);
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (CombatManager.HasInstance)
        {
            CombatManager.OnLifeChanged -= UpdateLifeDisplay;
            CombatManager.OnCreativityChanged -= UpdateCreativityDisplay;
            CombatManager.OnDeckSizeChanged -= UpdateDeckDisplay;
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
    }
    
    private void RefreshAllDisplays()
    {
        if (CombatManager.HasInstance)
        {
            var combat = CombatManager.Instance;
            UpdateLifeDisplay(combat.Life);
            UpdateCreativityDisplay(combat.Creativity);
            UpdateDeckDisplay(combat.DeckSize);
        }
        
        if (CardManager.HasInstance)
            UpdateCardPlayUI(CardManager.Instance.SelectedCards);
        
        UpdateDrawButton();
        UpdateDiscardButton();
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
    
    private void UpdateCardPlayUI(System.Collections.Generic.List<Card> selectedCards)
    {
        bool hasCards = selectedCards?.Count > 0;
        
        if (playButton != null) playButton.interactable = hasCards;
        if (clearButton != null) clearButton.interactable = hasCards;
        
        UpdateStatusDisplay(selectedCards);
        UpdateDrawButton();
        UpdateDiscardButton();
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
            drawButton.interactable = canDraw;
        }
    }
    
    private void UpdateDiscardButton()
    {
        if (discardButton != null)
        {
            bool hasSelectedCard = CardManager.HasInstance && CardManager.Instance.SelectedCards?.Count == 1;
            bool hasCreativity = CombatManager.HasInstance && CombatManager.Instance.CanSpendCreativity(discardCost);
            bool canDrawNew = DeckManager.HasInstance && DeckManager.Instance.GetTotalAvailableCards() > 0;
            
            discardButton.interactable = hasSelectedCard && hasCreativity && canDrawNew;
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
        // This properly destroys/hides the card instead of just removing it from hand
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
    
    private void SetupEntityEventListeners()
    {
        if (CombatManager.HasInstance)
        {
            CombatManager.OnTargetsChanged += UpdateTargetingDisplay;
            CombatManager.OnTargetingModeChanged += UpdateTargetingModeDisplay;
        }
    }

    private void UpdateTargetingDisplay(List<EntityBehaviour> targets)
    {
        // Update UI to show current targets
        if (statusText != null)
        {
            if (targets.Count > 0)
            {
                string targetNames = string.Join(", ", targets.Select(t => t.EntityName));
                statusText.text = $"Targets: {targetNames}";
            }
            else
            {
                statusText.text = "No targets selected";
            }
        }
    }

    private void UpdateTargetingModeDisplay(TargetingMode mode)
    {
        // Update UI to show targeting mode
        if (statusText != null)
        {
            statusText.text = $"Targeting: {mode}";
        }
    }
}