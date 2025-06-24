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
    
    // PERFORMANCE FIX: Button Update Throttling
    private float _lastButtonUpdate = 0f;
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
        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveAllListeners();
            endTurnButton.onClick.AddListener(EndTurn);
        }
        
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
        
        UpdateButtonsThrottled();
    }
    
    // INTEGRATION: Standardized Event Setup using ManagerExtensions
    private void SetupEventListeners()
    {
        ManagerExtensions.TryWithManager<CombatManager>(manager =>
        {
            CombatManager.OnLifeChanged += UpdateLifeDisplay;
            CombatManager.OnCreativityChanged += UpdateCreativityDisplay;
            CombatManager.OnDeckSizeChanged += UpdateDeckDisplay;
            CombatManager.OnTurnChanged += UpdateTurnDisplay;
            CombatManager.OnTurnPhaseChanged += UpdateTurnPhaseDisplay;
        });
        
        ManagerExtensions.TryWithManager<CardManager>(manager =>
        {
            CardManager.OnSelectionChanged += OnCardSelectionChanged;
            CardManager.OnHandUpdated += OnHandUpdated;
        });
        
        ManagerExtensions.TryWithManager<SpellcastManager>(manager =>
        {
            SpellcastManager.OnComboStateChanged += UpdateComboDisplay;
            SpellcastManager.OnSpellFound += OnSpellFound;
            SpellcastManager.OnSpellNotFound += OnSpellNotFound;
            SpellcastManager.OnComboCleared += OnComboCleared;
        });
    }
    
    private void OnDestroy()
    {
        // INTEGRATION: Standardized Event Cleanup using ManagerExtensions
        ManagerExtensions.TryWithManager<CombatManager>(manager =>
        {
            CombatManager.OnLifeChanged -= UpdateLifeDisplay;
            CombatManager.OnCreativityChanged -= UpdateCreativityDisplay;
            CombatManager.OnDeckSizeChanged -= UpdateDeckDisplay;
            CombatManager.OnTurnChanged -= UpdateTurnDisplay;
            CombatManager.OnTurnPhaseChanged -= UpdateTurnPhaseDisplay;
        });
        
        ManagerExtensions.TryWithManager<CardManager>(manager =>
        {
            CardManager.OnSelectionChanged -= OnCardSelectionChanged;
            CardManager.OnHandUpdated -= OnHandUpdated;
        });
        
        ManagerExtensions.TryWithManager<SpellcastManager>(manager =>
        {
            SpellcastManager.OnComboStateChanged -= UpdateComboDisplay;
            SpellcastManager.OnSpellFound -= OnSpellFound;
            SpellcastManager.OnSpellNotFound -= OnSpellNotFound;
            SpellcastManager.OnComboCleared -= OnComboCleared;
        });
    }
    
    // ===== BUTTON ACTIONS =====
    private void PlaySelectedCards()
    {
        if (!_managersReady) return;
        ManagerExtensions.TryWithManager<SpellcastManager>(sm => sm.PlaySelectedCards());
    }
    
    private void ClearSelection()
    {
        if (!_managersReady) return;
        ManagerExtensions.TryWithManager<SpellcastManager>(sm => sm.ClearSelection());
    }
    
    private void DrawCard()
    {
        if (!_managersReady) return;
        
        if (!CanDraw())
        {
            Debug.LogWarning("[GameUIHandler] Cannot draw - conditions not met");
            return;
        }
        
        ManagerExtensions.TryWithManager<SpellcastManager>(sm => sm.DrawCard());
    }
    
    private void CastCombo()
    {
        if (!_managersReady) return;
        ManagerExtensions.TryWithManager<SpellcastManager>(sm => sm.TryCastCurrentCombo());
    }
    
    private void EndTurn()
    {
        if (!_managersReady) return;
        ManagerExtensions.TryWithManager<CombatManager>(cm => 
        {
            if (cm.CanEndTurn)
                cm.EndPlayerTurn();
        });
    }
    
    private bool CanDraw()
    {
        if (!_managersReady) return false;
        
        return ManagerExtensions.TryWithManager<CardManager, bool>(cm => cm.CanDrawCard());
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
            int discardSize = ManagerExtensions.TryWithManager<CombatManager, int>(cm => cm.DiscardSize);
            deckText.text = $"Deck: {deckSize} | Discard: {discardSize}";
        }
        
        UpdateButtonsThrottled();
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
        
        UpdateButtonsThrottled();
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
        UpdateButtonsThrottled();
    }
    
    private void OnHandUpdated(List<Card> handCards)
    {
        UpdateButtonsThrottled();
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
    
    // PERFORMANCE FIX: Throttle Button Updates
    private void UpdateButtonsThrottled()
    {
        if (Time.unscaledTime - _lastButtonUpdate < 0.1f) return;
        UpdateAllButtons();
        _lastButtonUpdate = Time.unscaledTime;
    }
    
    private void UpdateAllButtons()
    {
        if (!_managersReady) return;
        
        bool isPlayerTurn = ManagerExtensions.TryWithManager<CombatManager, bool>(cm => cm.IsPlayerTurn);
        bool hasSelectedCards = ManagerExtensions.TryWithManager<CardManager, bool>(cm => cm.SelectedCards?.Count > 0);
        
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
        
        bool isPlayerTurn = ManagerExtensions.TryWithManager<CombatManager, bool>(cm => cm.IsPlayerTurn);
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
        
        #if UNITY_EDITOR
        var buttonText = drawButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null && !canDraw)
        {
            if (!_managersReady) buttonText.text = "Draw (Wait)";
            else if (ManagerExtensions.TryWithManager<CardManager, bool>(cm => cm.IsHandFull)) buttonText.text = "Draw (Full)";
            else if (ManagerExtensions.TryWithManager<DeckManager, bool>(dm => dm.IsDeckEmpty)) buttonText.text = "Draw (Empty)";
            else if (!ManagerExtensions.TryWithManager<CombatManager, bool>(cm => cm.IsPlayerTurn)) buttonText.text = "Draw (Turn)";
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
        if (endTurnButton != null)
        {
            bool canEndTurn = ManagerExtensions.TryWithManager<CombatManager, bool>(cm => cm.CanEndTurn);
            endTurnButton.interactable = canEndTurn;
            
            var buttonText = endTurnButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                bool isProcessing = ManagerExtensions.TryWithManager<CombatManager, bool>(cm => cm.IsProcessingTurn);
                buttonText.text = isProcessing ? "Processing..." : "End Turn";
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
        
        ManagerExtensions.TryWithManager<CombatManager>(combat =>
        {
            UpdateLifeDisplay(combat.Life);
            UpdateCreativityDisplay(combat.Creativity);
            UpdateDeckDisplay(combat.DeckSize);
            UpdateTurnDisplay(combat.CurrentTurn);
            UpdateTurnPhaseDisplay(combat.CurrentPhase);
        });
        
        ManagerExtensions.TryWithManager<CardManager>(cm =>
            UpdateCardPlayUI(cm.SelectedCards)
        );
            
        UpdateAllButtons();
    }
}