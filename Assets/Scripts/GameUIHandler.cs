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
    [SerializeField] private Button discardButton;
    
    [Header("Combat UI")]
    [SerializeField] private Button endTurnButton;
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private TextMeshProUGUI turnPhaseText;
    
    [Header("Combo Control")]
    [SerializeField] private Button comboClearButton; // Optional: Button für Combo Reset
    
    [Header("Status Display Settings")]
    [SerializeField] private bool showPercentages = true;
    [SerializeField] private Color healthLowColor = Color.red;
    [SerializeField] private Color healthNormalColor = Color.white;
    [SerializeField] private float healthLowThreshold = 0.25f;
    [SerializeField] private int discardCost = 1;
    [SerializeField] private float statusDisplayDuration = 3f; // Wie lange Status-Nachrichten angezeigt werden
    [SerializeField] private float statusFadeDelay = 1f; // Verzögerung vor dem Fade
    
    [Header("Combo Display Settings")]
    [SerializeField] private Color comboEmptyColor = Color.gray;
    [SerializeField] private Color comboBuildingColor = Color.yellow;
    [SerializeField] private Color comboPotentialColor = Color.green;
    [SerializeField] private Color comboInvalidColor = Color.red;
    [SerializeField] private Color comboCompletedColor = Color.cyan;
    [SerializeField] private bool showLastPlayedInCombo = true;
    
    // Status Display System
    private enum StatusType
    {
        Selection,
        SpellSuccess,
        SpellFailed,
        Combat,
        Error
    }
    
    [System.Serializable]
    private class StatusMessage
    {
        public string message;
        public Color color;
        public StatusType type;
        public float timestamp;
        public float duration;
        
        public StatusMessage(string msg, Color col, StatusType typ, float dur = 3f)
        {
            message = msg;
            color = col;
            type = typ;
            timestamp = Time.time;
            duration = dur;
        }
        
        public bool IsExpired => Time.time - timestamp > duration;
    }
    
    private StatusMessage _currentStatus;
    private StatusMessage _pendingStatus;
    private Coroutine _statusUpdateCoroutine;
    private bool _isStatusLocked = false;
    
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
        // Combat Manager Events
        if (CombatManager.HasInstance)
        {
            CombatManager.OnLifeChanged += UpdateLifeDisplay;
            CombatManager.OnCreativityChanged += UpdateCreativityDisplay;
            CombatManager.OnDeckSizeChanged += UpdateDeckDisplay;
            CombatManager.OnTurnChanged += UpdateTurnDisplay;
            CombatManager.OnTurnPhaseChanged += UpdateTurnPhaseDisplay;
            CombatManager.OnCombatStarted += OnCombatStarted;
            CombatManager.OnCombatEnded += OnCombatEnded;
            CombatManager.OnPlayerTurnStarted += OnPlayerTurnStarted;
            CombatManager.OnPlayerTurnEnded += OnPlayerTurnEnded;
            CombatManager.OnEnemyTurnStarted += OnEnemyTurnStarted;
            CombatManager.OnEnemyTurnEnded += OnEnemyTurnEnded;
            CombatManager.OnTurnTransitionStarted += OnTurnTransitionStarted;
            CombatManager.OnTurnTransitionCompleted += OnTurnTransitionCompleted;
        }
        
        // Card Manager Events - IMPROVED: Nicht sofort überschreiben
        if (CardManager.HasInstance)
        {
            CardManager.OnSelectionChanged += OnCardSelectionChanged;
        }
        
        // Spellcast Events - ERWEITERT für bessere Combo-Anzeige
        if (SpellcastManager.HasInstance)
        {
            SpellcastManager.OnSpellFound += OnSpellFound;
            SpellcastManager.OnSpellNotFound += OnSpellNotFound;
            SpellcastManager.OnComboUpdated += UpdateComboDisplay;
            SpellcastManager.OnComboStateChanged += UpdateComboDisplayWithState; // NEUER Event
            SpellcastManager.OnCardsPlayed += OnCardsPlayed;
        }
        
        SetupButtonListeners();
    }
    
    private void SetupButtonListeners()
    {
        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveAllListeners();
            endTurnButton.onClick.AddListener(() => {
                Debug.Log("[GameUIHandler] End Turn button clicked!");
            
                if (!CombatManager.HasInstance)
                {
                    Debug.LogError("[GameUIHandler] CombatManager not available!");
                    return;
                }
            
                var combat = CombatManager.Instance;
                Debug.Log($"[GameUIHandler] Combat Status - InCombat: {combat.IsInCombat}, Phase: {combat.CurrentPhase}, CanEndTurn: {combat.CanEndTurn}");
            
                if (!combat.IsInCombat)
                {
                    Debug.LogWarning("[GameUIHandler] Combat not active - starting combat first!");
                    combat.StartCombat();
                    return;
                }
            
                if (combat.CanEndTurn)
                {
                    Debug.Log("[GameUIHandler] Executing EndPlayerTurn...");
                    combat.EndPlayerTurn();
                }
                else
                {
                    Debug.LogWarning($"[GameUIHandler] Cannot end turn - Phase: {combat.CurrentPhase}, Processing: {combat.IsProcessingTurn}");
                }
            });
        
            if (Application.isEditor)
            {
                endTurnButton.interactable = true;
                Debug.Log("[GameUIHandler] EDITOR: Force-enabled turn button for testing");
            }
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
        
        // NEUER Combo Clear Button
        if (comboClearButton != null && SpellcastManager.HasInstance)
        {
            comboClearButton.onClick.RemoveAllListeners();
            comboClearButton.onClick.AddListener(() => {
                if (SpellcastManager.HasInstance)
                {
                    SpellcastManager.Instance.ResetComboDisplay();
                    SetStatus("Combo cleared", Color.red, StatusType.Combat, 1f);
                }
            });
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
            CardManager.OnSelectionChanged -= OnCardSelectionChanged;
        }
        
        if (SpellcastManager.HasInstance)
        {
            SpellcastManager.OnSpellFound -= OnSpellFound;
            SpellcastManager.OnSpellNotFound -= OnSpellNotFound;
            SpellcastManager.OnComboUpdated -= UpdateComboDisplay;
            SpellcastManager.OnComboStateChanged -= UpdateComboDisplayWithState;
            SpellcastManager.OnCardsPlayed -= OnCardsPlayed;
        }
        
        // Clean up button listeners
        endTurnButton?.onClick.RemoveAllListeners();
        playButton?.onClick.RemoveAllListeners();
        clearButton?.onClick.RemoveAllListeners();
        drawButton?.onClick.RemoveAllListeners();
        discardButton?.onClick.RemoveAllListeners();
        comboClearButton?.onClick.RemoveAllListeners();
        
        // Stop coroutines
        if (_statusUpdateCoroutine != null)
        {
            StopCoroutine(_statusUpdateCoroutine);
        }
    }
    
    // =========================
    // IMPROVED STATUS SYSTEM
    // =========================
    
    // NEW: Event handler für gespielte Karten
    private void OnCardsPlayed(List<Card> playedCards, string letterSequence)
    {
        if (playedCards?.Count > 0)
        {
            // Zeige gespielte Buchstaben als Status (wird eventuell durch Spell überschrieben)
            SetStatus($"Played: {letterSequence}", Color.white, StatusType.Selection, 2f);
        }
    }
    
    private void OnCardSelectionChanged(List<Card> selectedCards)
    {
        // NUR aktualisieren wenn kein wichtiger Status aktiv ist
        if (!_isStatusLocked && (_currentStatus == null || _currentStatus.type == StatusType.Selection))
        {
            UpdateCardPlayUI(selectedCards);
        }
        
        UpdateCardPlayButtons();
        UpdateDrawButton();
        UpdateDiscardButton();
    }
    
    private void SetStatus(string message, Color color, StatusType type, float duration = 3f)
    {
        var newStatus = new StatusMessage(message, color, type, duration);
        
        // Prioritäten: Spell > Combat > Error > Selection
        bool shouldOverride = _currentStatus == null || 
                             type == StatusType.SpellSuccess || type == StatusType.SpellFailed ||
                             (type == StatusType.Combat && _currentStatus.type == StatusType.Selection) ||
                             (type == StatusType.Error);
        
        if (shouldOverride)
        {
            _currentStatus = newStatus;
            UpdateStatusDisplay();
            
            // Lock status für wichtige Nachrichten
            if (type == StatusType.SpellSuccess || type == StatusType.SpellFailed)
            {
                _isStatusLocked = true;
                
                // Start countdown für unlock
                if (_statusUpdateCoroutine != null)
                    StopCoroutine(_statusUpdateCoroutine);
                _statusUpdateCoroutine = StartCoroutine(StatusUnlockCountdown(duration));
            }
        }
        else
        {
            // Store as pending if current status is more important
            _pendingStatus = newStatus;
        }
    }
    
    private IEnumerator StatusUnlockCountdown(float duration)
    {
        yield return new WaitForSeconds(duration - statusFadeDelay);
        
        // Begin fade
        yield return new WaitForSeconds(statusFadeDelay);
        
        // Unlock and check for pending
        _isStatusLocked = false;
        _currentStatus = null;
        
        // Show pending status or return to selection display
        if (_pendingStatus != null && !_pendingStatus.IsExpired)
        {
            _currentStatus = _pendingStatus;
            _pendingStatus = null;
            UpdateStatusDisplay();
        }
        else
        {
            // Return to selection display
            if (CardManager.HasInstance)
            {
                UpdateCardPlayUI(CardManager.Instance.SelectedCards);
            }
        }
    }
    
    private void UpdateStatusDisplay()
    {
        if (statusText == null || _currentStatus == null) return;
        
        statusText.text = _currentStatus.message;
        statusText.color = _currentStatus.color;
        
        Debug.Log($"[GameUIHandler] Status updated: {_currentStatus.message} ({_currentStatus.type})");
    }
    
    // =========================
    // COMBO DISPLAY SYSTEM
    // =========================
    
    // NEUE Methode für zustandsbasierte Combo-Anzeige
    private void UpdateComboDisplayWithState(string currentCombo, ComboState state)
    {
        if (comboText == null) return;
        
        // Bestimme Anzeige-Text und Farbe basierend auf Zustand
        string displayText;
        Color displayColor;
        
        switch (state)
        {
            case ComboState.Empty:
                displayText = "Combo: -";
                displayColor = comboEmptyColor;
                break;
                
            case ComboState.Building:
                displayText = $"Combo: {currentCombo}";
                displayColor = comboBuildingColor;
                break;
                
            case ComboState.Potential:
                displayText = $"Combo: {currentCombo} ✓";
                displayColor = comboPotentialColor;
                break;
                
            case ComboState.Invalid:
                // Zeige die invalid combo in rot, aber persistent
                displayText = showLastPlayedInCombo && SpellcastManager.HasInstance 
                    ? $"Played: {SpellcastManager.Instance.LastPlayedSequence} ✗"
                    : $"Combo: {currentCombo} ✗";
                displayColor = comboInvalidColor;
                break;
                
            case ComboState.Completed:
                displayText = $"Combo: {currentCombo} ★";
                displayColor = comboCompletedColor;
                break;
                
            default:
                displayText = $"Combo: {currentCombo}";
                displayColor = comboEmptyColor;
                break;
        }
        
        // Aktualisiere UI
        comboText.text = displayText;
        comboText.color = displayColor;
        
        Debug.Log($"[GameUIHandler] Combo display updated: {displayText} ({state})");
    }
    
    // ÜBERARBEITETE UpdateComboDisplay Methode (Fallback für Kompatibilität)
    private void UpdateComboDisplay(string currentCombo)
    {
        if (comboText == null) return;
        
        // Fallback falls der neue Event nicht verfügbar ist
        if (string.IsNullOrEmpty(currentCombo))
        {
            comboText.text = "Combo: -";
            comboText.color = comboEmptyColor;
        }
        else
        {
            comboText.text = $"Combo: {currentCombo}";
            comboText.color = comboBuildingColor; // Default gelb
        }
    }
    
    // =========================
    // RESOURCE DISPLAYS
    // =========================
    
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
        
        if (CardManager.HasInstance && !_isStatusLocked)
            UpdateCardPlayUI(CardManager.Instance.SelectedCards);
        
        UpdateAllButtons();
        ForceUpdateTurnButton();
    }
    
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
            
            turnPhaseText.color = phase switch
            {
                TurnPhase.PlayerTurn => Color.green,
                TurnPhase.EnemyTurn => Color.red,
                TurnPhase.TurnTransition => Color.yellow,
                TurnPhase.CombatEnd => Color.gray,
                _ => Color.white
            };
        }
        
        UpdateEndTurnButton();
    }
    
    private void UpdateCardPlayUI(System.Collections.Generic.List<Card> selectedCards)
    {
        // NUR aktualisieren wenn Status nicht gesperrt ist
        if (_isStatusLocked) return;
        
        UpdateStatusDisplayForSelection(selectedCards);
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
        
        // NEUER Combo Clear Button State
        if (comboClearButton != null && SpellcastManager.HasInstance) 
        {
            bool hasCombo = !string.IsNullOrEmpty(SpellcastManager.Instance.CurrentCombo) || 
                           !string.IsNullOrEmpty(SpellcastManager.Instance.LastPlayedSequence);
            comboClearButton.interactable = hasCombo && isPlayerTurn;
        }
    }
    
    private void UpdateStatusDisplayForSelection(System.Collections.Generic.List<Card> selectedCards)
    {
        if (statusText == null) return;
        
        if (selectedCards?.Count > 0)
        {
            string letters = CardManager.GetLetterSequenceFromCards(selectedCards);
            SetStatus($"Letters: {letters}", Color.white, StatusType.Selection, 1f);
        }
        else
        {
            SetStatus("Select cards to play\nTip: Double-click, hold, or Ctrl+click to play instantly", Color.gray, StatusType.Selection, 1f);
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
    
    private void UpdateEndTurnButton()
    {
        if (endTurnButton != null && CombatManager.HasInstance)
        {
            bool canEndTurn = CombatManager.Instance.CanEndTurn;
            endTurnButton.interactable = canEndTurn;
            
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
    
    // =========================
    // EVENT HANDLERS
    // =========================
    
    private void OnCombatStarted()
    {
        Debug.Log("[GameUIHandler] Combat started - updating all UI");
        SetStatus("Combat Started!", Color.green, StatusType.Combat, 2f);
        UpdateAllButtons();
    }
    
    private void OnCombatEnded()
    {
        Debug.Log("[GameUIHandler] Combat ended - disabling combat UI");
        SetStatus("Combat Ended", Color.gray, StatusType.Combat, 2f);
        UpdateAllButtons();
    }
    
    private void OnPlayerTurnStarted(int turn)
    {
        Debug.Log($"[GameUIHandler] Player turn {turn} started - enabling player actions");
        UpdateAllButtons();
        
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
        
        if (endTurnButton != null)
        {
            endTurnButton.GetComponent<Image>().color = Color.yellow;
        }
    }
    
    private void OnTurnTransitionCompleted()
    {
        Debug.Log("[GameUIHandler] Turn transition completed - updating UI");
        UpdateAllButtons();
        
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
        
        CombatManager.Instance.SpendCreativity(discardCost);
        DeckManager.Instance.DiscardCard(cardToDiscard.CardData);
        CardManager.Instance.DiscardCard(cardToDiscard);
        
        var newCardData = DeckManager.Instance.DrawCard();
        if (newCardData != null)
        {
            CardManager.Instance.SpawnCard(newCardData, null, true);
        }
        
        SetStatus("Card discarded", Color.red, StatusType.Combat, 1.5f);
    }
    
    public void ForceUpdateTurnButton()
    {
        if (endTurnButton == null) return;
    
        bool shouldBeInteractable = true;
    
        if (CombatManager.HasInstance)
        {
            var combat = CombatManager.Instance;
            shouldBeInteractable = combat.IsInCombat && combat.CanEndTurn;
        
            Debug.Log($"[GameUIHandler] Turn button update - InCombat: {combat.IsInCombat}, CanEndTurn: {combat.CanEndTurn}, Result: {shouldBeInteractable}");
        }
    
        endTurnButton.interactable = shouldBeInteractable;
    
        var buttonText = endTurnButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null && CombatManager.HasInstance)
        {
            var combat = CombatManager.Instance;
            if (!combat.IsInCombat)
            {
                buttonText.text = "Start Combat";
            }
            else if (combat.IsProcessingTurn)
            {
                buttonText.text = "Processing...";
            }
            else if (combat.IsPlayerTurn)
            {
                buttonText.text = "End Turn";
            }
            else
            {
                buttonText.text = "Enemy Turn";
            }
        }
    }
    
    // IMPROVED: Spell Events mit besserer Persistenz
    private void OnSpellFound(SpellAsset spell, string usedLetters)
    {
        if (spell != null)
        {
            SetStatus($"Spell: {spell.SpellName}!", Color.green, StatusType.SpellSuccess, statusDisplayDuration);
        }
    }
    
    private void OnSpellNotFound(string attemptedLetters)
    {
        SetStatus("No spell found", Color.red, StatusType.SpellFailed, 2f);
    }
}