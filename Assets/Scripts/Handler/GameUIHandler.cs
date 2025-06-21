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
    [SerializeField] private Button castComboButton;
    
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
    
    private bool _managersReady = false;
    
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
        
        _managersReady = true;
        SetupEventListeners();
        SetupButtons();
        RefreshAllDisplays();
    }
    
    private void SetupButtons()
    {
        // Combat buttons
        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveAllListeners();
            endTurnButton.onClick.AddListener(EndTurn);
        }
        
        // Card action buttons - alle über SpellcastManager
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(PlaySelectedCards);
        }
        
        if (clearButton != null)
        {
            clearButton.onClick.RemoveAllListeners();
            clearButton.onClick.AddListener(ClearSelection);
        }
        
        if (drawButton != null)
        {
            drawButton.onClick.RemoveAllListeners();
            drawButton.onClick.AddListener(DrawCard);
        }
        
        if (castComboButton != null)
        {
            castComboButton.onClick.RemoveAllListeners();
            castComboButton.onClick.AddListener(CastCombo);
        }
        
        // Initial button state
        UpdateAllButtons();
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
            CardManager.OnHandUpdated += OnHandUpdated;
        }
        
        // Spellcast Events
        if (SpellcastManager.HasInstance)
        {
            SpellcastManager.OnComboStateChanged += UpdateComboDisplay;
            SpellcastManager.OnSpellFound += OnSpellFound;
            SpellcastManager.OnSpellNotFound += OnSpellNotFound;
            SpellcastManager.OnComboCleared += OnComboCleared;
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
            CardManager.OnHandUpdated -= OnHandUpdated;
        }
        
        if (SpellcastManager.HasInstance)
        {
            SpellcastManager.OnComboStateChanged -= UpdateComboDisplay;
            SpellcastManager.OnSpellFound -= OnSpellFound;
            SpellcastManager.OnSpellNotFound -= OnSpellNotFound;
            SpellcastManager.OnComboCleared -= OnComboCleared;
        }
    }
    
    // ===== BUTTON ACTIONS =====
    private void PlaySelectedCards()
    {
        if (!_managersReady || !SpellcastManager.HasInstance) return;
        SpellcastManager.Instance.PlaySelectedCards();
    }
    
    private void ClearSelection()
    {
        if (!_managersReady || !SpellcastManager.HasInstance) return;
        SpellcastManager.Instance.ClearSelection();
    }
    
    private void DrawCard()
    {
        if (!_managersReady || !SpellcastManager.HasInstance) return;
        
        Debug.Log("[GameUIHandler] Draw button clicked");
        
        // Zusätzliche Checks für Debug
        if (!CanDraw())
        {
            Debug.LogWarning("[GameUIHandler] Cannot draw - conditions not met");
            return;
        }
        
        SpellcastManager.Instance.DrawCard();
    }
    
    private void CastCombo()
    {
        if (!_managersReady || !SpellcastManager.HasInstance) return;
        SpellcastManager.Instance.TryCastCurrentCombo();
    }
    
    private void EndTurn()
    {
        if (!_managersReady || !CombatManager.HasInstance) return;
        if (CombatManager.Instance.CanEndTurn)
            CombatManager.Instance.EndPlayerTurn();
    }
    
    // ===== DRAW LOGIC =====
    private bool CanDraw()
    {
        if (!_managersReady) 
        {
            Debug.Log("[GameUIHandler] Managers not ready");
            return false;
        }
        
        if (!CombatManager.HasInstance || !CombatManager.Instance.IsPlayerTurn)
        {
            Debug.Log("[GameUIHandler] Not player turn");
            return false;
        }
        
        if (!CardManager.HasInstance)
        {
            Debug.Log("[GameUIHandler] CardManager missing");
            return false;
        }
        
        if (CardManager.Instance.IsHandFull)
        {
            Debug.Log("[GameUIHandler] Hand is full");
            return false;
        }
        
        if (!DeckManager.HasInstance)
        {
            Debug.Log("[GameUIHandler] DeckManager missing");
            return false;
        }
        
        if (DeckManager.Instance.IsDeckEmpty)
        {
            Debug.Log("[GameUIHandler] Deck is empty");
            return false;
        }
        
        return true;
    }
    
    // ===== RESOURCE DISPLAYS =====
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
    
    // ===== COMBO DISPLAY =====
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
        
        UpdateCastComboButton(state == ComboState.Ready);
    }
    
    // ===== CARD SELECTION =====
    private void OnCardSelectionChanged(List<Card> selectedCards)
    {
        UpdateCardPlayUI(selectedCards);
        UpdateAllButtons();
    }
    
    private void OnHandUpdated(List<Card> handCards)
    {
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
    
    // ===== BUTTON UPDATES =====
    private void UpdateAllButtons()
    {
        if (!_managersReady) return;
        
        bool isPlayerTurn = CombatManager.HasInstance && CombatManager.Instance.IsPlayerTurn;
        bool hasSelectedCards = CardManager.HasInstance && CardManager.Instance.SelectedCards?.Count > 0;
        
        // Play button
        if (playButton != null) 
            playButton.interactable = hasSelectedCards && isPlayerTurn;
            
        // Clear button
        if (clearButton != null) 
            clearButton.interactable = hasSelectedCards;
            
        // Draw and end turn buttons
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
        if (drawButton == null) return;
        
        bool canDraw = CanDraw();
        drawButton.interactable = canDraw;
        
        // Debug info im Editor
        #if UNITY_EDITOR
        var buttonText = drawButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null && !canDraw)
        {
            if (!_managersReady) buttonText.text = "Draw (Wait)";
            else if (CardManager.HasInstance && CardManager.Instance.IsHandFull) buttonText.text = "Draw (Full)";
            else if (DeckManager.HasInstance && DeckManager.Instance.IsDeckEmpty) buttonText.text = "Draw (Empty)";
            else if (!CombatManager.HasInstance || !CombatManager.Instance.IsPlayerTurn) buttonText.text = "Draw (Turn)";
            else buttonText.text = "Draw (?)";
        }
        else if (buttonText != null)
        {
            buttonText.text = "Draw Card";
        }
        #endif
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
    
    // ===== EVENT HANDLERS =====
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
        if (!_managersReady) return;
        
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