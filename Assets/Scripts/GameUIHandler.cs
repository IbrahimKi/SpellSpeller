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
    
    [Header("Settings")]
    [SerializeField] private bool showPercentages = true;
    [SerializeField] private Color healthLowColor = Color.red;
    [SerializeField] private Color healthNormalColor = Color.white;
    [SerializeField] private float healthLowThreshold = 0.25f;
    
    private void OnEnable()
    {
        // Combat Manager Events
        CombatManager.OnLifeChanged += UpdateLifeDisplay;
        CombatManager.OnCreativityChanged += UpdateCreativityDisplay;
        CombatManager.OnDeckSizeChanged += UpdateDeckDisplay;
        
        // Card Manager Events
        CardManager.OnSelectionChanged += UpdateCardPlayUI;
        
        // Spellcast Events
        SpellcastManager.OnSpellFound += OnSpellFound;
        SpellcastManager.OnSpellNotFound += OnSpellNotFound;
        SpellcastManager.OnComboUpdated += UpdateComboDisplay;
        
        RefreshAllDisplays();
    }
    
    private void OnDisable()
    {
        CombatManager.OnLifeChanged -= UpdateLifeDisplay;
        CombatManager.OnCreativityChanged -= UpdateCreativityDisplay;
        CombatManager.OnDeckSizeChanged -= UpdateDeckDisplay;
        CardManager.OnSelectionChanged -= UpdateCardPlayUI;
        SpellcastManager.OnSpellFound -= OnSpellFound;
        SpellcastManager.OnSpellNotFound -= OnSpellNotFound;
        SpellcastManager.OnComboUpdated -= UpdateComboDisplay;
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
    }
    
    private void UpdateDeckDisplay(int deckSize)
    {
        if (deckText != null)
        {
            int discardSize = CombatManager.HasInstance ? CombatManager.Instance.DiscardSize : 0;
            deckText.text = $"Deck: {deckSize} | Discard: {discardSize}";
        }
        
        UpdateDrawButton();
    }
    
    private void UpdateCardPlayUI(System.Collections.Generic.List<Card> selectedCards)
    {
        bool hasCards = selectedCards?.Count > 0;
        
        if (playButton != null) playButton.interactable = hasCards;
        if (clearButton != null) clearButton.interactable = hasCards;
        
        UpdateStatusDisplay(selectedCards);
        UpdateDrawButton();
    }
    
    private void UpdateStatusDisplay(System.Collections.Generic.List<Card> selectedCards)
    {
        if (statusText == null) return;
        
        if (selectedCards?.Count > 0)
        {
            // VERWENDET zentrale Methode statt Dopplung
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
                          CombatManager.HasInstance && CombatManager.Instance.DeckSize > 0;
            drawButton.interactable = canDraw;
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