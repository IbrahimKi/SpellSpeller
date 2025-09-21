using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using CardSystem.Extensions;
using GameSystem.Extensions;

public partial class GameUIHandler : MonoBehaviour
{
    [Header("Core UI")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI resourceText;
    [SerializeField] private TextMeshProUGUI handCountText;
    [SerializeField] private TextMeshProUGUI deckCountText;
    [SerializeField] private TextMeshProUGUI discardCountText;
    [SerializeField] private TextMeshProUGUI turnCountText;
    
    [Header("Resource Display")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider creativitySlider;
    
    [Header("Combat UI")]
    [SerializeField] private TextMeshProUGUI phaseText;
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private TextMeshProUGUI selectionText;
    
    [Header("Action Buttons")]
    [SerializeField] private Button playCardsButton;
    [SerializeField] private Button clearSelectionButton;
    [SerializeField] private Button drawCardButton;
    [SerializeField] private Button endTurnButton;
    [SerializeField] private Button castSpellButton;
    [SerializeField] private Button discardCardButton;
    [SerializeField] private Button clearComboButton;
    
    [Header("Debug Buttons")]
    [SerializeField] private Button debugButton;
    [SerializeField] private Button forceUpdateButton;
    
    // State tracking
    private int _lastHandCount = -1;
    private int _lastDeckCount = -1;
    private int _lastDiscardCount = -1;
    private int _lastTurnCount = -1;
    private float _lastHealth = -1f;
    private float _lastCreativity = -1f;
    private TurnPhase _lastPhase = TurnPhase.PlayerTurn;
    private string _lastCombo = "";
    private int _lastSelectionCount = -1;
    
    // Performance optimization
    private const float UPDATE_INTERVAL = 0.1f;
    private float _lastUpdate = 0f;
    
    // === INITIALIZATION ===
    
    private void Start()
    {
        InitializeUI();
        SetupButtons();
        SubscribeToEvents();
        StartCoroutine(DelayedInitialUpdate());
    }
    
    private void InitializeUI()
    {
        SetupStatusText();
        ResetDisplays();
        UpdateButtonStates();
    }
    
    private void SetupStatusText()
    {
        if (statusText != null)
        {
            statusText.text = "Initializing...";
            statusText.color = Color.white;
        }
    }
    
    private void ResetDisplays()
    {
        UpdateHandCount(0);
        UpdateDeckCount(0);
        UpdateDiscardCount(0);
        UpdateTurnCount(1);
        UpdateHealth(100, 100);
        UpdateCreativity(3, 3);
        UpdatePhase(TurnPhase.PlayerTurn);
        UpdateCombo("", ComboState.Empty);
        UpdateSelection(0);
    }
    
    private IEnumerator DelayedInitialUpdate()
    {
        yield return new WaitForSeconds(0.5f);
        ForceFullUpdate();
        SetStatus("Ready to play!", Color.green);
    }
    
    // === BUTTON SETUP ===
    
    private void SetupButtons()
    {
        // Action Buttons
        if (playCardsButton != null)
            playCardsButton.onClick.AddListener(OnPlayCardsClicked);
        if (clearSelectionButton != null)
            clearSelectionButton.onClick.AddListener(OnClearSelectionClicked);
        if (drawCardButton != null)
            drawCardButton.onClick.AddListener(OnDrawCardClicked);
        if (endTurnButton != null)
            endTurnButton.onClick.AddListener(OnEndTurnClicked);
        if (castSpellButton != null)
            castSpellButton.onClick.AddListener(OnCastSpellClicked);
        if (discardCardButton != null)
            discardCardButton.onClick.AddListener(OnDiscardCardClicked);
        if (clearComboButton != null)
            clearComboButton.onClick.AddListener(OnClearComboClicked);
            
        // Debug Buttons
        if (debugButton != null)
            debugButton.onClick.AddListener(OnDebugClicked);
        if (forceUpdateButton != null)
            forceUpdateButton.onClick.AddListener(OnForceUpdateClicked);
    }
    
    // === BUTTON ACTIONS ===
    
    private void OnPlayCardsClicked()
    {
        this.TryWithManager<SpellcastManager>(sm => sm.PlaySelectedCards());
    }
    
    private void OnClearSelectionClicked()
    {
        this.TryWithManager<CardManager>(cm => cm.ClearSelection());
    }
    
    private void OnDrawCardClicked()
    {
        this.TryWithManager<DeckManager>(dm => dm.TryDrawCard());
    }
    
    private void OnEndTurnClicked()
    {
        this.TryWithManager<CombatManager>(cm => cm.EndPlayerTurn());
    }
    
    private void OnCastSpellClicked()
    {
        this.TryWithManager<SpellcastManager>(sm => sm.TryCastCurrentCombo());
    }
    
    private void OnDiscardCardClicked()
    {
        // Discard first selected card for creativity
        this.TryWithManager<CardManager>(cm => 
        {
            var selectedCards = cm.SelectedCards;
            if (selectedCards.Count > 0)
            {
                var cardToDiscard = selectedCards[0];
                this.TryWithManager<CombatManager>(combat => 
                {
                    if (combat.CanSpendResource(ResourceType.Creativity, 1))
                    {
                        combat.TryModifyResource(ResourceType.Creativity, -1);
                        
                        // Add to discard pile
                        this.TryWithManager<DeckManager>(dm => 
                        {
                            if (cardToDiscard.CardData != null)
                                dm.DiscardCard(cardToDiscard.CardData);
                        });
                        
                        // Remove from hand and destroy
                        cm.RemoveCardFromHand(cardToDiscard);
                        cm.DestroyCard(cardToDiscard);
                        
                        // Draw new card
                        this.TryWithManager<DeckManager>(dm => dm.TryDrawCard());
                        
                        SetStatus($"Discarded: {cardToDiscard.GetCardName()}", Color.yellow);
                    }
                    else
                    {
                        SetStatus("Not enough creativity to discard", Color.red);
                    }
                });
            }
            else
            {
                SetStatus("No card selected to discard", Color.yellow);
            }
        });
    }
    
    private void OnClearComboClicked()
    {
        this.TryWithManager<SpellcastManager>(sm => sm.ClearCombo());
    }
    
    private void OnDebugClicked()
    {
        GameExtensions.LogManagerPerformance();
        SetStatus("Debug Info Logged", Color.magenta);
    }
    
    private void OnForceUpdateClicked()
    {
        ForceFullUpdate();
        SetStatus("UI Force Updated", Color.magenta);
    }
    
    // === BUTTON STATE UPDATES ===
    
    private void UpdateButtonStates()
    {
        bool isPlayerTurn = false;
        bool hasSelection = false;
        bool canDrawCard = false;
        bool canEndTurn = false;
        bool canCastSpell = false;
        bool canDiscardCard = false;
        bool hasCombo = false;
        
        // Get current states from managers
        this.TryWithManager<CombatManager>(cm => 
        {
            isPlayerTurn = cm.IsPlayerTurn && !cm.IsProcessingTurn;
            canEndTurn = cm.CanEndTurn;
            canDiscardCard = cm.CanSpendResource(ResourceType.Creativity, 1);
        });
        
        this.TryWithManager<CardManager>(cm => 
        {
            hasSelection = cm.HasValidSelection;
            canDrawCard = cm.CanDrawCard();
        });
        
        this.TryWithManager<SpellcastManager>(sm => 
        {
            canCastSpell = sm.CurrentComboState == ComboState.Ready;
            hasCombo = !string.IsNullOrEmpty(sm.CurrentCombo);
        });
        
        // Update button interactability
        if (playCardsButton != null)
            playCardsButton.interactable = isPlayerTurn && hasSelection;
        if (clearSelectionButton != null)
            clearSelectionButton.interactable = hasSelection;
        if (drawCardButton != null)
            drawCardButton.interactable = isPlayerTurn && canDrawCard;
        if (endTurnButton != null)
            endTurnButton.interactable = canEndTurn;
        if (castSpellButton != null)
            castSpellButton.interactable = isPlayerTurn && canCastSpell;
        if (discardCardButton != null)
            discardCardButton.interactable = isPlayerTurn && hasSelection && canDiscardCard;
        if (clearComboButton != null)
            clearComboButton.interactable = hasCombo;
    }
    
    // === EVENT SUBSCRIPTION ===
    
    private void SubscribeToEvents()
    {
        // Card Manager Events
        CardManager.OnHandUpdated += OnHandUpdated;
        CardManager.OnSelectionChanged += OnSelectionChanged;
        CardManager.OnCardSpawned += OnCardSpawned;
        CardManager.OnCardDestroyed += OnCardDestroyed;
        CardManager.OnCardDiscarded += OnCardDiscarded;
        
        // Deck Manager Events  
        DeckManager.OnDeckSizeChanged += OnDeckSizeChanged;
        DeckManager.OnDiscardPileSizeChanged += OnDiscardSizeChanged;
        DeckManager.OnCardDrawn += OnCardDrawn;
        DeckManager.OnCardDiscarded += OnCardDiscardedFromDeck;
        
        // Drag Handler Events
        Handler.CardDragHandler.OnCardDragStart.AddListener(OnCardDragStart);
        Handler.CardDragHandler.OnCardDragEnd.AddListener(OnCardDragEnd);
        
        // Combat Manager Events
        CombatManager.OnLifeChanged += OnLifeChanged;
        CombatManager.OnCreativityChanged += OnCreativityChanged;
        CombatManager.OnTurnChanged += OnTurnChanged;
        CombatManager.OnTurnPhaseChanged += OnTurnPhaseChanged;
        CombatManager.OnCombatStarted += OnCombatStarted;
        
        // Spellcast Manager Events
        SpellcastManager.OnComboStateChanged += OnComboStateChanged;
        SpellcastManager.OnSpellCast += OnSpellCast;
        SpellcastManager.OnComboCleared += OnComboCleared;
    }
    
    private void UnsubscribeFromEvents()
    {
        // Card Manager Events
        CardManager.OnHandUpdated -= OnHandUpdated;
        CardManager.OnSelectionChanged -= OnSelectionChanged;
        CardManager.OnCardSpawned -= OnCardSpawned;
        CardManager.OnCardDestroyed -= OnCardDestroyed;
        CardManager.OnCardDiscarded -= OnCardDiscarded;
        
        // Deck Manager Events
        DeckManager.OnDeckSizeChanged -= OnDeckSizeChanged;
        DeckManager.OnDiscardPileSizeChanged -= OnDiscardSizeChanged;
        DeckManager.OnCardDrawn -= OnCardDrawn;
        DeckManager.OnCardDiscarded -= OnCardDiscardedFromDeck;
        
        // Drag Handler Events
        Handler.CardDragHandler.OnCardDragStart.RemoveListener(OnCardDragStart);
        Handler.CardDragHandler.OnCardDragEnd.RemoveListener(OnCardDragEnd);
        
        // Combat Manager Events
        CombatManager.OnLifeChanged -= OnLifeChanged;
        CombatManager.OnCreativityChanged -= OnCreativityChanged;
        CombatManager.OnTurnChanged -= OnTurnChanged;
        CombatManager.OnTurnPhaseChanged -= OnTurnPhaseChanged;
        CombatManager.OnCombatStarted -= OnCombatStarted;
        
        // Spellcast Manager Events
        SpellcastManager.OnComboStateChanged -= OnComboStateChanged;
        SpellcastManager.OnSpellCast -= OnSpellCast;
        SpellcastManager.OnComboCleared -= OnComboCleared;
    }
    
    // === EVENT HANDLERS ===
    
    private void OnHandUpdated(List<Card> handCards)
    {
        if (handCards != null)
        {
            UpdateHandCount(handCards.Count);
            UpdateButtonStates();
        }
    }
    
    private void OnSelectionChanged(List<Card> selectedCards)
    {
        if (selectedCards == null) return;
        
        UpdateSelection(selectedCards.Count);
        UpdateButtonStates();
        
        if (selectedCards.Count > 0)
        {
            SetStatus($"{selectedCards.Count} card(s) selected", Color.cyan);
        }
        else
        {
            SetStatus("Select cards to play", Color.white);
        }
    }
    
    private void OnCardSpawned(Card card)
    {
        if (card != null)
        {
            SetStatus($"Card drawn: {card.GetCardName()}", Color.green);
        }
    }
    
    private void OnCardDestroyed(Card card)
    {
        if (card != null)
        {
            SetStatus($"Card removed: {card.GetCardName()}", Color.gray);
        }
    }
    
    private void OnCardDiscarded(Card card)
    {
        if (card != null)
        {
            SetStatus($"Card discarded: {card.GetCardName()}", Color.yellow);
        }
    }
    
    private void OnDeckSizeChanged(int deckSize)
    {
        UpdateDeckCount(deckSize);
    }
    
    private void OnDiscardSizeChanged(int discardSize)
    {
        UpdateDiscardCount(discardSize);
    }
    
    private void OnCardDrawn(CardData cardData)
    {
        if (cardData != null)
        {
            SetStatus($"Drew: {cardData.cardName}", Color.green);
        }
    }
    
    private void OnCardDiscardedFromDeck(CardData cardData)
    {
        if (cardData != null)
        {
            SetStatus($"Added to discard: {cardData.cardName}", Color.yellow);
        }
    }
    
    private void OnCardDragStart(GameObject card)
    {
        SetStatus("Dragging card...", Color.yellow);
    }
    
    private void OnCardDragEnd(GameObject card)
    {
        SetStatus("Card dropped", Color.white);
    }
    
    private void OnLifeChanged(Resource life)
    {
        if (life != null)
        {
            UpdateHealthIfChanged(life.CurrentValue, life.MaxValue);
        }
    }
    
    private void OnCreativityChanged(Resource creativity)
    {
        if (creativity != null)
        {
            UpdateCreativityIfChanged(creativity.CurrentValue, creativity.MaxValue);
            UpdateButtonStates();
        }
    }
    
    private void OnTurnChanged(int turnNumber)
    {
        UpdateTurnCount(turnNumber);
        SetStatus($"Turn {turnNumber} Started", Color.cyan);
    }
    
    private void OnTurnPhaseChanged(TurnPhase phase)
    {
        UpdatePhase(phase);
        UpdateButtonStates();
        
        string phaseText = phase switch
        {
            TurnPhase.PlayerTurn => "Your Turn",
            TurnPhase.EnemyTurn => "Enemy Turn",
            TurnPhase.TurnTransition => "Processing...",
            TurnPhase.CombatEnd => "Combat End",
            _ => "Unknown Phase"
        };
        
        Color phaseColor = phase switch
        {
            TurnPhase.PlayerTurn => Color.green,
            TurnPhase.EnemyTurn => Color.red,
            TurnPhase.TurnTransition => Color.yellow,
            _ => Color.white
        };
        
        SetStatus(phaseText, phaseColor);
    }
    
    private void OnCombatStarted()
    {
        SetStatus("Combat Started!", Color.cyan);
        UpdateButtonStates();
    }
    
    private void OnComboStateChanged(string combo, ComboState state)
    {
        UpdateCombo(combo, state);
        UpdateButtonStates();
        
        string stateText = state switch
        {
            ComboState.Empty => "Enter combo...",
            ComboState.Building => $"Building: {combo}",
            ComboState.Ready => $"Ready: {combo}!",
            ComboState.Invalid => $"Invalid: {combo}",
            _ => combo
        };
        
        Color stateColor = state switch
        {
            ComboState.Ready => Color.green,
            ComboState.Building => Color.yellow,
            ComboState.Invalid => Color.red,
            _ => Color.white
        };
        
        if (state != ComboState.Empty)
        {
            SetStatus(stateText, stateColor);
        }
    }
    
    private void OnSpellCast(SpellAsset spell, List<CardData> cards)
    {
        if (spell != null)
        {
            SetStatus($"Cast: {spell.SpellName}!", Color.magenta);
        }
    }
    
    private void OnComboCleared()
    {
        UpdateCombo("", ComboState.Empty);
        UpdateButtonStates();
    }
    
    // === UI UPDATE SYSTEM ===
    
    private void Update()
    {
        if (Time.time - _lastUpdate < UPDATE_INTERVAL) return;
        _lastUpdate = Time.time;
        
        UpdateFromManagers();
        UpdateButtonStates();
    }
    
    private void UpdateFromManagers()
    {
        // Update from Combat Manager if available
        if (CoreExtensions.IsManagerReady<CombatManager>())
        {
            this.TryWithManager<CombatManager>(cm => 
            {
                UpdateHealthIfChanged(cm.Life.CurrentValue, cm.Life.MaxValue);
                UpdateCreativityIfChanged(cm.Creativity.CurrentValue, cm.Creativity.MaxValue);
            });
        }
    }
    
    private void ForceFullUpdate()
    {
        // Force update from all managers
        this.TryWithManager<CardManager>(cm => 
        {
            UpdateHandCount(cm.HandSize);
        });
        
        this.TryWithManager<DeckManager>(dm => 
        {
            UpdateDeckCount(dm.DeckSize);
            UpdateDiscardCount(dm.DiscardSize);
        });
        
        this.TryWithManager<CombatManager>(cm => 
        {
            UpdateTurnCount(cm.CurrentTurn);
            UpdatePhase(cm.CurrentPhase);
        });
        
        this.TryWithManager<SpellcastManager>(sm => 
        {
            UpdateCombo(sm.CurrentCombo, sm.CurrentComboState);
        });
        
        UpdateFromManagers();
        UpdateButtonStates();
    }
    
    // === UI UPDATE METHODS ===
    
    private void UpdateHandCount(int count)
    {
        if (_lastHandCount == count) return;
        _lastHandCount = count;
        
        if (handCountText != null)
        {
            handCountText.text = $"Hand: {count}";
        }
    }
    
    private void UpdateDeckCount(int count)
    {
        if (_lastDeckCount == count) return;
        _lastDeckCount = count;
        
        if (deckCountText != null)
        {
            deckCountText.text = $"Deck: {count}";
        }
    }
    
    private void UpdateDiscardCount(int count)
    {
        if (_lastDiscardCount == count) return;
        _lastDiscardCount = count;
        
        if (discardCountText != null)
        {
            discardCountText.text = $"Discard: {count}";
        }
    }
    
    private void UpdateTurnCount(int count)
    {
        if (_lastTurnCount == count) return;
        _lastTurnCount = count;
        
        if (turnCountText != null)
        {
            turnCountText.text = $"Turn: {count}";
        }
    }
    
    private void UpdateHealthIfChanged(int current, int max)
    {
        float percentage = max > 0 ? (float)current / max : 0f;
        if (Mathf.Abs(_lastHealth - percentage) < 0.01f) return;
        
        UpdateHealth(current, max);
    }
    
    private void UpdateHealth(int current, int max)
    {
        if (healthText != null)
        {
            healthText.text = $"Health: {current}/{max}";
        }
        
        if (healthSlider != null)
        {
            float percentage = max > 0 ? (float)current / max : 0f;
            healthSlider.value = percentage;
            _lastHealth = percentage;
        }
    }
    
    private void UpdateCreativityIfChanged(int current, int max)
    {
        float percentage = max > 0 ? (float)current / max : 0f;
        if (Mathf.Abs(_lastCreativity - percentage) < 0.01f) return;
        
        UpdateCreativity(current, max);
    }
    
    private void UpdateCreativity(int current, int max)
    {
        if (resourceText != null)
        {
            resourceText.text = $"Creativity: {current}/{max}";
        }
        
        if (creativitySlider != null)
        {
            float percentage = max > 0 ? (float)current / max : 0f;
            creativitySlider.value = percentage;
            _lastCreativity = percentage;
        }
    }
    
    private void UpdatePhase(TurnPhase phase)
    {
        if (_lastPhase == phase) return;
        _lastPhase = phase;
        
        if (phaseText != null)
        {
            string phaseStr = phase switch
            {
                TurnPhase.PlayerTurn => "Player Turn",
                TurnPhase.EnemyTurn => "Enemy Turn",
                TurnPhase.TurnTransition => "Processing",
                TurnPhase.CombatEnd => "Combat End",
                _ => "Unknown"
            };
            phaseText.text = phaseStr;
        }
    }
    
    private void UpdateCombo(string combo, ComboState state)
    {
        if (_lastCombo == combo) return;
        _lastCombo = combo;
        
        if (comboText != null)
        {
            if (string.IsNullOrEmpty(combo))
            {
                comboText.text = "Combo: -";
            }
            else
            {
                string stateSymbol = state switch
                {
                    ComboState.Building => "...",
                    ComboState.Ready => "!",
                    ComboState.Invalid => "✗",
                    _ => ""
                };
                comboText.text = $"Combo: {combo}{stateSymbol}";
            }
        }
    }
    
    private void UpdateSelection(int count)
    {
        if (_lastSelectionCount == count) return;
        _lastSelectionCount = count;
        
        if (selectionText != null)
        {
            selectionText.text = count > 0 ? $"Selected: {count}" : "Selected: -";
        }
    }
    
    private void SetStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
    }
    
    // === CLEANUP ===
    
    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

#if UNITY_EDITOR
    [ContextMenu("Test UI Handler")]
    private void TestUIHandler()
    {
        Debug.Log($"[GameUIHandler] Status: Active");
        ForceFullUpdate();
    }
    
    [ContextMenu("Force UI Update")]
    private void DebugForceUpdate()
    {
        ForceFullUpdate();
        SetStatus("UI Force Updated", Color.magenta);
    }
    
    [ContextMenu("Test All Buttons")]
    private void TestAllButtons()
    {
        Debug.Log("[GameUIHandler] Testing all button states...");
        UpdateButtonStates();
        
        Debug.Log($"Play Cards: {(playCardsButton?.interactable ?? false)}");
        Debug.Log($"Draw Card: {(drawCardButton?.interactable ?? false)}");
        Debug.Log($"End Turn: {(endTurnButton?.interactable ?? false)}");
        Debug.Log($"Cast Spell: {(castSpellButton?.interactable ?? false)}");
        Debug.Log($"Discard Card: {(discardCardButton?.interactable ?? false)}");
    }
#endif
}