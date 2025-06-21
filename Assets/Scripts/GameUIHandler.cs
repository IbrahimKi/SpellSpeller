using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

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
    [SerializeField] private Button castComboButton; // NEUER Button für Combo-Cast
    
    [Header("Combat UI")]
    [SerializeField] private Button endTurnButton;
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private TextMeshProUGUI turnPhaseText;
    
    [Header("Visual Settings")]
    [SerializeField] private Color healthLowColor = Color.red;
    [SerializeField] private Color healthNormalColor = Color.white;
    [SerializeField] private float healthLowThreshold = 0.25f;
    
    [Header("Combo Display Colors")]
    [SerializeField] private Color comboEmptyColor = Color.gray;
    [SerializeField] private Color comboBuildingColor = Color.yellow;
    [SerializeField] private Color comboReadyColor = Color.green;
    [SerializeField] private Color comboInvalidColor = Color.red;
    
    private void Start()
    {
        StartCoroutine(WaitForManagersAndSetup());
    }
    
    private IEnumerator WaitForManagersAndSetup()
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
        // Combat Events
        if (CombatManager.HasInstance)
        {
            CombatManager.OnLifeChanged += UpdateLifeDisplay;
            CombatManager.OnCreativityChanged += UpdateCreativityDisplay;
            CombatManager.OnDeckSizeChanged += UpdateDeckDisplay;
            CombatManager.OnTurnChanged += UpdateTurnDisplay;
            CombatManager.OnTurnPhaseChanged += UpdateTurnPhaseDisplay;
        }
        
        // Card Events
        if (CardManager.HasInstance)
        {
            CardManager.OnSelectionChanged += OnCardSelectionChanged;
        }
        
        // Spellcast Events
        if (SpellcastManager.HasInstance)
        {
            SpellcastManager.OnComboStateChanged += UpdateComboDisplay;
            SpellcastManager.OnSpellFound += OnSpellFound;
            SpellcastManager.OnSpellNotFound += OnSpellNotFound;
            SpellcastManager.OnComboCleared += OnComboCleared;
        }
        
        SetupButtonListeners();
    }
    
    private void SetupButtonListeners()
    {
        // Combat buttons
        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveAllListeners();
            endTurnButton.onClick.AddListener(() => {
                if (CombatManager.HasInstance && CombatManager.Instance.CanEndTurn)
                    CombatManager.Instance.EndPlayerTurn();
            });
        }
        
        // Card buttons
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
        
        // NEUER Cast Combo Button
        if (castComboButton != null && SpellcastManager.HasInstance)
        {
            castComboButton.onClick.RemoveAllListeners();
            castComboButton.onClick.AddListener(() => SpellcastManager.Instance.TryCastCurrentCombo());
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe events
        if (CombatManager.HasInstance)
        {
            CombatManager.OnLifeChanged -= UpdateLifeDisplay;
            CombatManager.OnCreativityChanged -= UpdateCreativityDisplay;
            CombatManager.OnDeckSizeChanged -= UpdateDeckDisplay;
            CombatManager.OnTurnChanged -= UpdateTurnDisplay;
            CombatManager.OnTurnPhaseChanged -= UpdateTurnPhaseDisplay;
        }
        
        if (CardManager.HasInstance)
        {
            CardManager.OnSelectionChanged -= OnCardSelectionChanged;
        }
        
        if (SpellcastManager.HasInstance)
        {
            SpellcastManager.OnComboStateChanged -= UpdateComboDisplay;
            SpellcastManager.OnSpellFound -= OnSpellFound;
            SpellcastManager.OnSpellNotFound -= OnSpellNotFound;
            SpellcastManager.OnComboCleared -= OnComboCleared;
        }
    }
    
    // Resource Displays
    private void UpdateLifeDisplay(Resource life)
    {
        if (lifeText != null)
        {
            lifeText.text = $"Life: {life.CurrentValue}/{life.MaxValue}";
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
    
    private void UpdateTurnDisplay(int turn)
    {
        if (turnText != null)
            turnText.text = $"Turn {turn}";
    }
    
    private void UpdateTurnPhaseDisplay(TurnPhase phase)
    {
        if (turnPhaseText != null)
        {
            turnPhaseText.text = phase switch
            {
                TurnPhase.PlayerTurn => "Your Turn",
                TurnPhase.EnemyTurn => "Enemy Turn",
                TurnPhase.TurnTransition => "Processing...",
                TurnPhase.CombatEnd => "Combat Ended",
                _ => phase.ToString()
            };
            
            turnPhaseText.color = phase switch
            {
                TurnPhase.PlayerTurn => Color.green,
                TurnPhase.EnemyTurn => Color.red,
                TurnPhase.TurnTransition => Color.yellow,
                TurnPhase.CombatEnd => Color.gray,
                _ => Color.white
            };
        }
        
        UpdateAllButtons();
    }
    
    // Combo Display
    private void UpdateComboDisplay(string combo, ComboState state)
    {
        if (comboText == null) return;
        
        string displayText;
        Color displayColor;
        
        switch (state)
        {
            case ComboState.Empty:
                displayText = "Combo: -";
                displayColor = comboEmptyColor;
                break;
                
            case ComboState.Building:
                displayText = $"Combo: {combo}";
                displayColor = comboBuildingColor;
                break;
                
            case ComboState.Ready:
                displayText = $"Combo: {combo} [SPACE TO CAST]";
                displayColor = comboReadyColor;
                break;
                
            case ComboState.Invalid:
                displayText = $"Combo: {combo} ✗";
                displayColor = comboInvalidColor;
                break;
                
            default:
                displayText = $"Combo: {combo}";
                displayColor = comboEmptyColor;
                break;
        }
        
        comboText.text = displayText;
        comboText.color = displayColor;
        
        // Update cast button
        UpdateCastComboButton(state == ComboState.Ready);
    }
    
    // Card Selection
    private void OnCardSelectionChanged(List<Card> selectedCards)
    {
        UpdateCardPlayUI(selectedCards);
        UpdateAllButtons();
    }
    
    private void UpdateCardPlayUI(List<Card> selectedCards)
    {
        if (statusText == null) return;
        
        if (selectedCards?.Count > 0)
        {
            string letters = CardManager.GetLetterSequenceFromCards(selectedCards);
            statusText.text = $"Selected: {letters}";
        }
        else
        {
            statusText.text = "Select cards to play";
        }
    }
    
    // Button Updates
    private void UpdateAllButtons()
    {
        bool isPlayerTurn = CombatManager.HasInstance && CombatManager.Instance.IsPlayerTurn;
        bool hasSelectedCards = CardManager.HasInstance && CardManager.Instance.SelectedCards?.Count > 0;
        
        if (playButton != null) 
            playButton.interactable = hasSelectedCards && isPlayerTurn;
            
        if (clearButton != null) 
            clearButton.interactable = hasSelectedCards;
            
        UpdateDrawButton();
        UpdateEndTurnButton();
    }
    
    private void UpdateCastComboButton(bool canCast)
    {
        if (castComboButton == null) return;
        
        bool isPlayerTurn = CombatManager.HasInstance && CombatManager.Instance.IsPlayerTurn;
        castComboButton.interactable = canCast && isPlayerTurn;
        
        var buttonText = castComboButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.text = canCast ? "CAST COMBO!" : "Cast Combo";
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
    
    private void UpdateEndTurnButton()
    {
        if (endTurnButton != null && CombatManager.HasInstance)
        {
            endTurnButton.interactable = CombatManager.Instance.CanEndTurn;
            
            var buttonText = endTurnButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = CombatManager.Instance.IsProcessingTurn ? "Processing..." : "End Turn";
            }
        }
    }
    
    // Event Handlers
    private void OnSpellFound(SpellAsset spell, string usedLetters)
    {
        if (statusText != null)
        {
            statusText.text = $"Cast: {spell.SpellName}!";
            statusText.color = Color.green;
        }
    }
    
    private void OnSpellNotFound(string attemptedLetters)
    {
        if (statusText != null)
        {
            statusText.text = "No spell found";
            statusText.color = Color.red;
        }
    }
    
    private void OnComboCleared()
    {
        if (comboText != null)
        {
            comboText.text = "Combo: -";
            comboText.color = comboEmptyColor;
        }
        
        UpdateCastComboButton(false);
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
}