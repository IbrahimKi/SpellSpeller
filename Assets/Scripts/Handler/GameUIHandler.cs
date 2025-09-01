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
    
    [Header("Card Slot System")]
    [SerializeField] private GameObject slotSystemPanel;
    [SerializeField] private Button playSlotSequenceButton;
    [SerializeField] private Button clearSlotsButton;
    [SerializeField] private TextMeshProUGUI slotStatusText;
    [SerializeField] private TextMeshProUGUI slotSequenceText;
    [SerializeField] private Toggle enableSlotsToggle;
    
    [Header("Visual Settings")]
    [SerializeField] private Color healthLowColor = Color.red;
    [SerializeField] private Color healthNormalColor = Color.white;
    [SerializeField] private float healthLowThreshold = 0.25f;
    
    [Header("Combo Display Colors")]
    [SerializeField] private Color comboEmptyColor = Color.gray;
    [SerializeField] private Color comboBuildingColor = Color.yellow;
    [SerializeField] private Color comboReadyColor = Color.green;
    [SerializeField] private Color comboInvalidColor = Color.red;
    
    [Header("Spell Cast Display")]
    [SerializeField] private GameObject spellCastPanel;
    [SerializeField] private TextMeshProUGUI lastSpellText;
    [SerializeField] private TextMeshProUGUI spellDamageText;
    [SerializeField] private float spellDisplayDuration = 3f;
    
    [Header("Enemy Info Display")]
    [SerializeField] private GameObject enemyInfoPanel;
    [SerializeField] private TextMeshProUGUI enemyNameText;
    [SerializeField] private TextMeshProUGUI enemyTypeText;
    [SerializeField] private TextMeshProUGUI enemyHealthText;
    [SerializeField] private Slider enemyHealthBar;
    
    [Header("Total Enemy Health")]
    [SerializeField] private GameObject totalEnemyHealthPanel;
    [SerializeField] private Slider totalEnemyHealthBar;
    [SerializeField] private TextMeshProUGUI totalEnemyHealthText;
    [SerializeField] private TextMeshProUGUI enemyCountText;
    
    [Header("Update Settings")]
    [SerializeField] private float enemyPanelUpdateInterval = 0.1f;
    
    // Performance optimization
    private float _lastButtonUpdate = 0f;
    private float _lastEnemyPanelUpdate = 0f;
    private bool _managersReady = false;
    
    // Spell tracking
    private Coroutine _spellDisplayCoroutine;
    private SpellAsset _currentSpell;
    private bool _waitingForDamage = false;
    
    // Enemy tracking
    private EntityBehaviour _currentDisplayedEnemy;
    private int _totalEnemyMaxHealth;
    private int _totalEnemyCurrentHealth;
    
    // Card Slot System State
    private bool _slotSystemEnabled = false;
    private List<Card> _currentSlotSequence = new List<Card>();
    
    private void Start()
    {
        StartCoroutine(WaitForManagersAndSetup());
    }
    
    private void Update()
    {
        if (!_managersReady) return;
        UpdateEnemyPanelLogic();
    }
    
    private void UpdateEnemyPanelLogic()
    {
        if (Time.time - _lastEnemyPanelUpdate < enemyPanelUpdateInterval) return;
        _lastEnemyPanelUpdate = Time.time;
        
        EntityBehaviour enemyToShow = GetEnemyToDisplay();
        
        if (enemyToShow != null)
        {
            if (_currentDisplayedEnemy != enemyToShow)
            {
                _currentDisplayedEnemy = enemyToShow;
                ShowEnemyInfo(enemyToShow);
            }
            else
            {
                UpdateEnemyHealthDisplay(enemyToShow);
            }
        }
        else if (_currentDisplayedEnemy != null)
        {
            _currentDisplayedEnemy = null;
            HideEnemyInfo();
        }
    }
    
    private EntityBehaviour GetEnemyToDisplay()
    {
        return CoreExtensions.TryWithManager<EnemyManager, EntityBehaviour>(this, em =>
        {
            var targetedEnemy = em.AllEnemies.FirstOrDefault(e => e != null && e.IsTargeted());
            if (targetedEnemy != null) return targetedEnemy;
            
            var boss = em.AllEnemies.FirstOrDefault(e => e != null && e.IsBoss() && e.IsValidEntity());
            if (boss != null) return boss;
            
            var elite = em.AllEnemies.FirstOrDefault(e => e != null && e.IsElite() && e.IsValidEntity());
            if (elite != null) return elite;
            
            return em.AllEnemies.FirstOrDefault(e => e != null && e.IsValidEntity());
        });
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
        SetupCardSlotSystem();
        RefreshAllDisplays();
    }
    
    // === CARD SLOT SYSTEM SETUP ===
    
    private void SetupCardSlotSystem()
    {
        Debug.Log("[GameUIHandler] Setting up Card Slot System...");
        
        if (!ValidateSlotSystemComponents())
        {
            Debug.LogWarning("[GameUIHandler] Slot system components missing - disabling");
            DisableSlotSystemUI();
            return;
        }
        
        if (!CardSlotManager.HasInstance)
        {
            Debug.LogWarning("[GameUIHandler] CardSlotManager not found - retrying...");
            StartCoroutine(DelayedSlotSystemSetup());
            return;
        }
        
        var csm = CardSlotManager.Instance;
        if (!csm.IsReady)
        {
            Debug.LogWarning("[GameUIHandler] CardSlotManager not ready yet - retrying...");
            StartCoroutine(DelayedSlotSystemSetup());
            return;
        }
        
        SetupSlotEventListeners();
        SetupSlotButtons();
        SetupSlotToggle(csm);
        
        _slotSystemEnabled = csm.IsEnabled;
        UpdateSlotSystemDisplay();
        
        Debug.Log("[GameUIHandler] Card Slot System setup complete");
    }

    private bool ValidateSlotSystemComponents()
    {
        bool hasAllComponents = true;
        
        if (slotSystemPanel == null)
        {
            Debug.LogError("[GameUIHandler] slotSystemPanel is null!");
            hasAllComponents = false;
        }
        
        if (playSlotSequenceButton == null)
        {
            Debug.LogError("[GameUIHandler] playSlotSequenceButton is null!");
            hasAllComponents = false;
        }
        
        if (clearSlotsButton == null)
        {
            Debug.LogError("[GameUIHandler] clearSlotsButton is null!");
            hasAllComponents = false;
        }
        
        return hasAllComponents;
    }

    private void DisableSlotSystemUI()
    {
        if (slotSystemPanel != null)
            slotSystemPanel.SetActive(false);
    }

    private void SetupSlotEventListeners()
    {
        CardSlotManager.OnSlotSequenceChanged -= OnSlotSequenceChanged;
        CardSlotManager.OnCardPlacedInSlot -= OnCardPlacedInSlot;
        CardSlotManager.OnCardRemovedFromSlot -= OnCardRemovedFromSlot;
        
        CardSlotManager.OnSlotSequenceChanged += OnSlotSequenceChanged;
        CardSlotManager.OnCardPlacedInSlot += OnCardPlacedInSlot;
        CardSlotManager.OnCardRemovedFromSlot += OnCardRemovedFromSlot;
        
        Debug.Log("[GameUIHandler] Slot event listeners setup");
    }

    private void SetupSlotButtons()
    {
        if (playSlotSequenceButton != null)
        {
            playSlotSequenceButton.onClick.RemoveAllListeners();
            playSlotSequenceButton.onClick.AddListener(PlaySlotSequence);
            Debug.Log("[GameUIHandler] Play slot button configured");
        }
        
        if (clearSlotsButton != null)
        {
            clearSlotsButton.onClick.RemoveAllListeners();
            clearSlotsButton.onClick.AddListener(ClearAllSlots);
            Debug.Log("[GameUIHandler] Clear slots button configured");
        }
    }

    private void SetupSlotToggle(CardSlotManager csm)
    {
        if (enableSlotsToggle != null)
        {
            enableSlotsToggle.onValueChanged.RemoveAllListeners();
            enableSlotsToggle.isOn = csm.IsEnabled;
            enableSlotsToggle.onValueChanged.AddListener(ToggleSlotSystem);
            Debug.Log("[GameUIHandler] Slot toggle configured");
        }
    }

    private IEnumerator DelayedSlotSystemSetup()
    {
        yield return new WaitForSeconds(0.5f);
        SetupCardSlotSystem();
    }
    
    // === CARD SLOT EVENT HANDLERS ===
    
    private void OnSlotSequenceChanged(List<Card> slotSequence)
    {
        Debug.Log($"[GameUIHandler] Slot sequence changed: {slotSequence?.Count ?? 0} cards");
        _currentSlotSequence = slotSequence ?? new List<Card>();
        UpdateSlotSystemDisplay();
    }
    
    private void OnCardPlacedInSlot(CardSlotBehaviour slot, Card card)
    {
        Debug.Log($"[GameUIHandler] Card {card.GetCardName()} placed in slot {slot.SlotIndex + 1}");
        UpdateSlotSystemDisplay();
        
        if (statusText != null)
        {
            statusText.text = $"Card placed in slot {slot.SlotIndex + 1}";
            statusText.color = Color.green;
            StartCoroutine(ResetStatusTextDelayed(2f));
        }
    }
    
    private void OnCardRemovedFromSlot(CardSlotBehaviour slot, Card card)
    {
        Debug.Log($"[GameUIHandler] Card {card.GetCardName()} removed from slot {slot.SlotIndex + 1}");
        UpdateSlotSystemDisplay();
        
        if (statusText != null)
        {
            statusText.text = $"Card removed from slot {slot.SlotIndex + 1}";
            statusText.color = Color.yellow;
            StartCoroutine(ResetStatusTextDelayed(2f));
        }
    }
    
    // === SLOT SYSTEM UI UPDATES ===
    
    private void UpdateSlotSystemDisplay()
    {
        if (!CardSlotManager.HasInstance)
        {
            DisableSlotSystemUI();
            return;
        }
        
        var csm = CardSlotManager.Instance;
        bool systemEnabled = csm.IsEnabled;
        
        if (slotSystemPanel != null)
            slotSystemPanel.SetActive(systemEnabled);
        
        if (!systemEnabled) return;
        
        UpdateSlotSequenceDisplay(csm);
        UpdateSlotStatusDisplay(csm);
        UpdateSlotButtons();
    }
    
    private void UpdateSlotSequenceDisplay(CardSlotManager csm)
    {
        if (slotSequenceText == null) return;
        
        string sequence = csm.GetSlotLetterSequence();
        if (string.IsNullOrEmpty(sequence))
        {
            slotSequenceText.text = "Sequence: Empty";
            slotSequenceText.color = Color.gray;
        }
        else
        {
            slotSequenceText.text = $"Sequence: {sequence}";
            
            if (csm.CanPlaySlotSequence())
            {
                slotSequenceText.color = Color.green;
            }
            else if (csm.FilledSlotCount > 0)
            {
                slotSequenceText.color = Color.yellow;
            }
            else
            {
                slotSequenceText.color = Color.white;
            }
        }
    }
    
    private void UpdateSlotStatusDisplay(CardSlotManager csm)
    {
        if (slotStatusText == null) return;
        
        int filled = csm.FilledSlotCount;
        int total = csm.SlotCount;
        slotStatusText.text = $"Slots: {filled}/{total}";
        
        if (filled == 0)
            slotStatusText.color = Color.gray;
        else if (csm.CanPlaySlotSequence())
            slotStatusText.color = Color.green;
        else
            slotStatusText.color = Color.yellow;
    }
    
    private void UpdateSlotButtons()
    {
        if (!CardSlotManager.HasInstance) return;
        
        var csm = CardSlotManager.Instance;
        bool isPlayerTurn = CoreExtensions.TryWithManager<CombatManager, bool>(this, cm => cm.IsPlayerTurn);
        
        UpdatePlaySlotButton(csm, isPlayerTurn);
        UpdateClearSlotButton(csm);
    }
    
    private void UpdatePlaySlotButton(CardSlotManager csm, bool isPlayerTurn)
    {
        if (playSlotSequenceButton == null) return;
        
        bool canPlay = csm.CanPlaySlotSequence() && isPlayerTurn;
        playSlotSequenceButton.interactable = canPlay;
        
        var buttonText = playSlotSequenceButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            int count = csm.FilledSlotCount;
            if (count > 0)
            {
                string sequence = csm.GetSlotLetterSequence();
                buttonText.text = $"Play Slots ({sequence})";
            }
            else
            {
                buttonText.text = "Play Slots";
            }
        }
    }
    
    private void UpdateClearSlotButton(CardSlotManager csm)
    {
        if (clearSlotsButton == null) return;
        
        clearSlotsButton.interactable = csm.FilledSlotCount > 0;
        
        var buttonText = clearSlotsButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            int count = csm.FilledSlotCount;
            buttonText.text = count > 0 ? $"Clear Slots ({count})" : "Clear Slots";
        }
    }
    
    // === SLOT SYSTEM BUTTON ACTIONS ===
    
    private void PlaySlotSequence()
    {
        Debug.Log("[GameUIHandler] PlaySlotSequence() called");
        
        if (!_managersReady || !CardSlotManager.HasInstance) 
        {
            Debug.LogWarning("[GameUIHandler] Managers not ready for slot play");
            return;
        }
        
        var csm = CardSlotManager.Instance;
        
        if (!csm.CanPlaySlotSequence())
        {
            Debug.LogWarning("[GameUIHandler] Cannot play slot sequence - conditions not met");
            ShowSlotMessage("Cannot play slot sequence", Color.red);
            return;
        }
        
        if (csm.FilledSlotCount == 0)
        {
            Debug.LogWarning("[GameUIHandler] No cards in slots to play");
            ShowSlotMessage("No cards in slots", Color.grey);
            return;
        }
        
        Debug.Log($"[GameUIHandler] Playing slot sequence with {csm.FilledSlotCount} cards");
        
        try
        {
            bool success = csm.PlayAllSlots();
            
            if (success)
            {
                string sequence = csm.GetSlotLetterSequence();
                ShowSlotMessage($"Played sequence: {sequence}", Color.cyan);
                Debug.Log($"[GameUIHandler] Successfully played slot sequence: {sequence}");
            }
            else
            {
                ShowSlotMessage("Failed to play sequence", Color.red);
                Debug.LogError("[GameUIHandler] PlayAllSlots() returned false");
            }
        }
        catch (System.Exception ex)
        {
            ShowSlotMessage("Error playing slots", Color.red);
            Debug.LogError($"[GameUIHandler] Exception in PlaySlotSequence: {ex.Message}");
        }
        
        UpdateSlotSystemDisplay();
    }
    
    private void ClearAllSlots()
    {
        Debug.Log("[GameUIHandler] ClearAllSlots() called");
        
        if (!CardSlotManager.HasInstance) 
        {
            Debug.LogWarning("[GameUIHandler] CardSlotManager not available");
            return;
        }
        
        var csm = CardSlotManager.Instance;
        int clearedCount = csm.FilledSlotCount;
        
        csm.ClearAllSlots();
        
        ShowSlotMessage($"Cleared {clearedCount} slots", Color.yellow);
        Debug.Log($"[GameUIHandler] Cleared {clearedCount} card slots");
        
        UpdateSlotSystemDisplay();
    }
    
    private void ToggleSlotSystem(bool enabled)
    {
        Debug.Log($"[GameUIHandler] ToggleSlotSystem({enabled}) called");
        
        if (!CardSlotManager.HasInstance) 
        {
            Debug.LogWarning("[GameUIHandler] CardSlotManager not available for toggle");
            return;
        }
        
        var csm = CardSlotManager.Instance;
        csm.SetEnabled(enabled);
        
        _slotSystemEnabled = enabled;
        UpdateSlotSystemDisplay();
        
        ShowSlotMessage(enabled ? "Slot system enabled" : "Slot system disabled", 
                       enabled ? Color.green : Color.gray);
        
        Debug.Log($"[GameUIHandler] Card slot system {(enabled ? "enabled" : "disabled")}");
    }
    
    private void ShowSlotMessage(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
            StartCoroutine(ResetStatusTextDelayed(3f));
        }
    }
    
    // === STANDARD UI METHODS (gekürzt) ===
    
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
    
    private void SetupEventListeners()
    {
        CoreExtensions.TryWithManager<CombatManager>(this, manager =>
        {
            CombatManager.OnLifeChanged += UpdateLifeDisplay;
            CombatManager.OnCreativityChanged += UpdateCreativityDisplay;
            CombatManager.OnDeckSizeChanged += UpdateDeckDisplay;
            CombatManager.OnTurnChanged += UpdateTurnDisplay;
            CombatManager.OnTurnPhaseChanged += UpdateTurnPhaseDisplay;
        });
        
        CoreExtensions.TryWithManager<CardManager>(this, manager =>
        {
            CardManager.OnSelectionChanged += OnCardSelectionChanged;
            CardManager.OnHandUpdated += OnHandUpdated;
        });
        
        CoreExtensions.TryWithManager<SpellcastManager>(this, manager =>
        {
            SpellcastManager.OnComboStateChanged += UpdateComboDisplay;
            SpellcastManager.OnSpellFound += OnSpellFound;
            SpellcastManager.OnSpellNotFound += OnSpellNotFound;
            SpellcastManager.OnComboCleared += OnComboCleared;
            SpellcastManager.OnSpellCast += OnSpellCast;
            SpellcastManager.OnSpellDamageDealt += OnSpellDamageDealt;
        });
        
        CoreExtensions.TryWithManager<EnemyManager>(this, manager =>
        {
            EnemyManager.OnEnemySpawned += OnEnemySpawned;
            EnemyManager.OnEnemyDespawned += OnEnemyDespawned;
            EntityBehaviour.OnEntityHealthChanged += OnEntityHealthChanged;
        });
    }
    
    private void OnDestroy()
    {
        // Unsubscribe all events
        CombatManager.OnLifeChanged -= UpdateLifeDisplay;
        CombatManager.OnCreativityChanged -= UpdateCreativityDisplay;
        CombatManager.OnDeckSizeChanged -= UpdateDeckDisplay;
        CombatManager.OnTurnChanged -= UpdateTurnDisplay;
        CombatManager.OnTurnPhaseChanged -= UpdateTurnPhaseDisplay;
        
        CardManager.OnSelectionChanged -= OnCardSelectionChanged;
        CardManager.OnHandUpdated -= OnHandUpdated;
        
        SpellcastManager.OnComboStateChanged -= UpdateComboDisplay;
        SpellcastManager.OnSpellFound -= OnSpellFound;
        SpellcastManager.OnSpellNotFound -= OnSpellNotFound;
        SpellcastManager.OnComboCleared -= OnComboCleared;
        SpellcastManager.OnSpellCast -= OnSpellCast;
        SpellcastManager.OnSpellDamageDealt -= OnSpellDamageDealt;
        
        EnemyManager.OnEnemySpawned -= OnEnemySpawned;
        EnemyManager.OnEnemyDespawned -= OnEnemyDespawned;
        EntityBehaviour.OnEntityHealthChanged -= OnEntityHealthChanged;
        
        // Card Slot System Events cleanup
        if (CardSlotManager.HasInstance)
        {
            CardSlotManager.OnSlotSequenceChanged -= OnSlotSequenceChanged;
            CardSlotManager.OnCardPlacedInSlot -= OnCardPlacedInSlot;
            CardSlotManager.OnCardRemovedFromSlot -= OnCardRemovedFromSlot;
        }
    }
    
    // === BUTTON ACTIONS ===
    private void PlaySelectedCards()
    {
        if (!_managersReady) return;
        CoreExtensions.TryWithManager<SpellcastManager>(this, sm => sm.PlaySelectedCards());
    }
    
    private void ClearSelection()
    {
        if (!_managersReady) return;
        CoreExtensions.TryWithManager<SpellcastManager>(this, sm => sm.ClearSelection());
    }
    
    private void DrawCard()
    {
        if (!_managersReady) return;
        
        if (!CanDraw())
        {
            Debug.LogWarning("[GameUIHandler] Cannot draw - conditions not met");
            return;
        }
        
        CoreExtensions.TryWithManager<SpellcastManager>(this, sm => sm.DrawCard());
    }
    
    private void CastCombo()
    {
        if (!_managersReady) return;
        CoreExtensions.TryWithManager<SpellcastManager>(this, sm => sm.TryCastCurrentCombo());
    }
    
    private void EndTurn()
    {
        if (!_managersReady) return;
        CoreExtensions.TryWithManager<CombatManager>(this, cm => 
        {
            if (cm.CanEndTurn)
                cm.EndPlayerTurn();
        });
    }
    
    private bool CanDraw()
    {
        if (!_managersReady) return false;
        return CoreExtensions.TryWithManager<CardManager, bool>(this, cm => cm.CanDrawCard());
    }
    
    // === RESOURCE DISPLAYS ===
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
            int discardSize = CoreExtensions.TryWithManager<CombatManager, int>(this, cm => cm.DiscardSize);
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
    
    // === COMBO DISPLAY ===
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
    
    // === CARD SELECTION ===
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
            statusText.color = Color.white;
        }
        else
        {
            statusText.text = "Select cards to play";
            statusText.color = Color.white;
        }
    }
    
    private void UpdateButtonsThrottled()
    {
        if (Time.unscaledTime - _lastButtonUpdate < 0.1f) return;
        UpdateAllButtons();
        _lastButtonUpdate = Time.unscaledTime;
    }
    
    private void UpdateAllButtons()
    {
        if (!_managersReady) return;
        
        bool isPlayerTurn = CoreExtensions.TryWithManager<CombatManager, bool>(this, cm => cm.IsPlayerTurn);
        bool hasSelectedCards = CoreExtensions.TryWithManager<CardManager, bool>(this, cm => cm.SelectedCards?.Count > 0);
        
        if (playButton != null) 
            playButton.interactable = hasSelectedCards && isPlayerTurn;
            
        if (clearButton != null) 
            clearButton.interactable = hasSelectedCards;
            
        UpdateDrawButton();
        UpdateEndTurnButton();
        UpdateSlotButtons();
    }
    
    private void UpdateCastComboButton(bool canCast)
    {
        if (castComboButton == null) return;
        
        bool isPlayerTurn = CoreExtensions.TryWithManager<CombatManager, bool>(this, cm => cm.IsPlayerTurn);
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
    }
    
    private void UpdateEndTurnButton()
    {
        if (endTurnButton != null)
        {
            bool canEndTurn = CoreExtensions.TryWithManager<CombatManager, bool>(this, cm => cm.CanEndTurn);
            endTurnButton.interactable = canEndTurn;
            
            var buttonText = endTurnButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                bool isProcessing = CoreExtensions.TryWithManager<CombatManager, bool>(this, cm => cm.IsProcessingTurn);
                buttonText.text = isProcessing ? "Processing..." : "End Turn";
            }
        }
    }
    
    // === SPELL EVENTS (gekürzt) ===
    private void OnSpellFound(SpellAsset spell, string usedLetters)
    {
        if (statusText != null)
        {
            statusText.text = $"Casting: {spell.SpellName}...";
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
        
        StartCoroutine(ResetStatusTextDelayed(2f));
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
    
    private void OnSpellCast(SpellAsset spell, List<CardData> cards)
    {
        Debug.Log($"[GameUIHandler] Spell cast: {spell.SpellName}");
        
        _currentSpell = spell;
        _waitingForDamage = true;
        
        ShowSpellCastDisplay(spell);
        
        StartCoroutine(SpellDamageTimeout(1f));
    }
    
    private void OnSpellDamageDealt(SpellAsset spell, int totalDamage)
    {
        Debug.Log($"[GameUIHandler] Spell damage received: {spell.SpellName} dealt {totalDamage} damage");
        
        _waitingForDamage = false;
        UpdateSpellDamageDisplay(spell, totalDamage);
        
        if (statusText != null)
        {
            statusText.text = $"{spell.SpellName}: {totalDamage} damage!";
            statusText.color = Color.red;
        }
        
        StartCoroutine(ResetStatusTextDelayed(3f));
    }
    
    private IEnumerator SpellDamageTimeout(float timeout)
    {
        yield return new WaitForSeconds(timeout);
        
        if (_waitingForDamage && _currentSpell != null)
        {
            Debug.LogWarning($"[GameUIHandler] Timeout waiting for damage from {_currentSpell.SpellName}");
            _waitingForDamage = false;
            
            UpdateSpellDamageDisplay(_currentSpell, 0);
        }
    }
    
    private IEnumerator ResetStatusTextDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (statusText != null)
        {
            statusText.text = "Select cards to play";
            statusText.color = Color.white;
        }
    }
    
    // === SPELL CAST DISPLAY ===
    private void ShowSpellCastDisplay(SpellAsset spell)
    {
        if (spellCastPanel == null || spell == null) return;
        
        spellCastPanel.SetActive(true);
        
        if (lastSpellText != null)
            lastSpellText.text = spell.SpellName;
            
        if (spellDamageText != null)
        {
            spellDamageText.text = "Casting...";
            spellDamageText.color = Color.yellow;
        }
        
        if (_spellDisplayCoroutine != null)
            StopCoroutine(_spellDisplayCoroutine);
        _spellDisplayCoroutine = StartCoroutine(HideSpellDisplayAfterDelay());
    }
    
    private void UpdateSpellDamageDisplay(SpellAsset spell, int totalDamage)
    {
        if (spellDamageText == null || !spellCastPanel.activeSelf) return;
        
        var primaryEffect = spell.Effects?.FirstOrDefault();
        if (primaryEffect != null)
        {
            switch (primaryEffect.effectType)
            {
                case SpellEffectType.Damage:
                    spellDamageText.text = $"Damage: {totalDamage}";
                    spellDamageText.color = totalDamage > 0 ? Color.red : Color.gray;
                    break;
                case SpellEffectType.Heal:
                    spellDamageText.text = $"Heal: +{Mathf.RoundToInt(primaryEffect.value)}";
                    spellDamageText.color = Color.green;
                    break;
                case SpellEffectType.Buff:
                    spellDamageText.text = "Buff Applied";
                    spellDamageText.color = Color.blue;
                    break;
                default:
                    spellDamageText.text = "Effect Applied";
                    spellDamageText.color = Color.cyan;
                    break;
            }
        }
        else
        {
            spellDamageText.text = "Unknown Effect";
            spellDamageText.color = Color.gray;
        }
    }
    
    private IEnumerator HideSpellDisplayAfterDelay()
    {
        yield return new WaitForSeconds(spellDisplayDuration);
        
        if (spellCastPanel != null)
            spellCastPanel.SetActive(false);
    }
    
    // === ENEMY INFO DISPLAY ===
    private void ShowEnemyInfo(EntityBehaviour enemy)
    {
        if (enemyInfoPanel == null || enemy == null) return;
        
        enemyInfoPanel.SetActive(true);
        
        if (enemyNameText != null)
            enemyNameText.text = enemy.EntityName;
            
        if (enemyTypeText != null)
        {
            string type = enemy.IsBoss() ? "BOSS" : enemy.IsElite() ? "Elite" : "Enemy";
            enemyTypeText.text = type;
            enemyTypeText.color = enemy.IsBoss() ? Color.red : enemy.IsElite() ? Color.yellow : Color.white;
        }
        
        UpdateEnemyHealthDisplay(enemy);
    }
    
    private void HideEnemyInfo()
    {
        if (enemyInfoPanel != null)
            enemyInfoPanel.SetActive(false);
    }
    
    private void UpdateEnemyHealthDisplay(EntityBehaviour enemy)
    {
        if (enemy == null) return;
        
        if (enemyHealthText != null)
            enemyHealthText.text = $"{enemy.CurrentHealth}/{enemy.MaxHealth} HP";
            
        if (enemyHealthBar != null)
            enemyHealthBar.value = enemy.HealthPercentage;
    }
    
    // === TOTAL ENEMY HEALTH ===
    private void OnEnemySpawned(EntityBehaviour enemy)
    {
        UpdateTotalEnemyHealth();
    }
    
    private void OnEnemyDespawned(EntityBehaviour enemy)
    {
        UpdateTotalEnemyHealth();
    }
    
    private void OnEntityHealthChanged(EntityBehaviour entity, int oldHealth, int newHealth)
    {
        if (entity.IsEnemy())
        {
            UpdateTotalEnemyHealth();
        }
    }
    
    private void UpdateTotalEnemyHealth()
    {
        _totalEnemyMaxHealth = 0;
        _totalEnemyCurrentHealth = 0;
        
        CoreExtensions.TryWithManager<EnemyManager>(this, em =>
        {
            foreach (var enemy in em.AllEnemies)
            {
                if (enemy != null && enemy.IsValidEntity())
                {
                    _totalEnemyMaxHealth += enemy.MaxHealth;
                    _totalEnemyCurrentHealth += Mathf.Max(0, enemy.CurrentHealth);
                }
            }
            
            UpdateTotalEnemyHealthDisplay(em.AliveEnemyCount);
        });
    }
    
    private void UpdateTotalEnemyHealthDisplay(int enemyCount)
    {
        if (totalEnemyHealthPanel != null)
        {
            totalEnemyHealthPanel.SetActive(_totalEnemyMaxHealth > 0);
        }
        
        if (totalEnemyHealthBar != null && _totalEnemyMaxHealth > 0)
        {
            totalEnemyHealthBar.value = (float)_totalEnemyCurrentHealth / _totalEnemyMaxHealth;
        }
        
        if (totalEnemyHealthText != null)
        {
            totalEnemyHealthText.text = $"{_totalEnemyCurrentHealth} / {_totalEnemyMaxHealth}";
        }
        
        if (enemyCountText != null)
        {
            enemyCountText.text = $"Enemies: {enemyCount}";
        }
    }
    
    private void RefreshAllDisplays()
    {
        if (!_managersReady) return;
        
        CoreExtensions.TryWithManager<CombatManager>(this, combat =>
        {
            UpdateLifeDisplay(combat.Life);
            UpdateCreativityDisplay(combat.Creativity);
            UpdateDeckDisplay(combat.DeckSize);
            UpdateTurnDisplay(combat.CurrentTurn);
            UpdateTurnPhaseDisplay(combat.CurrentPhase);
        });
        
        CoreExtensions.TryWithManager<CardManager>(this, cm =>
            UpdateCardPlayUI(cm.SelectedCards)
        );
        
        UpdateTotalEnemyHealth();
        UpdateAllButtons();
        UpdateSlotSystemDisplay();
    }
    
    // === CONVENIENCE METHODS FOR SLOT SYSTEM ===
    
    public bool IsSlotSystemActive()
    {
        return _slotSystemEnabled && CardSlotManager.HasInstance && CardSlotManager.Instance.IsEnabled;
    }
    
    public int GetCurrentSlotCount()
    {
        return CardSlotManager.HasInstance ? CardSlotManager.Instance.FilledSlotCount : 0;
    }
    
    public string GetCurrentSlotLetterSequence()
    {
        return CardSlotManager.HasInstance ? CardSlotManager.Instance.GetSlotLetterSequence() : "";
    }
    
    public bool CanPlayCurrentSlots()
    {
        return IsSlotSystemActive() && 
               CardSlotManager.Instance.FilledSlotCount > 0 && 
               CardSlotManager.Instance.CanPlaySlotSequence();
    }

#if UNITY_EDITOR
    [ContextMenu("Test Slot System")]
    private void TestSlotSystem()
    {
        Debug.Log("[GameUIHandler] Testing slot system...");
        Debug.Log($"Slot system enabled: {_slotSystemEnabled}");
        Debug.Log($"Slot system active: {IsSlotSystemActive()}");
        Debug.Log($"Current slot count: {GetCurrentSlotCount()}");
        Debug.Log($"Current sequence: '{GetCurrentSlotLetterSequence()}'");
        Debug.Log($"Can play slots: {CanPlayCurrentSlots()}");
    }
    
    [ContextMenu("Toggle Slot System")]
    private void DebugToggleSlotSystem()
    {
        ToggleSlotSystem(!_slotSystemEnabled);
    }
    
    [ContextMenu("Clear Test Slots")]
    private void DebugClearSlots()
    {
        ClearAllSlots();
    }
    
    [ContextMenu("Refresh Slot Display")]
    private void DebugRefreshSlotDisplay()
    {
        UpdateSlotSystemDisplay();
    }
#endif
}