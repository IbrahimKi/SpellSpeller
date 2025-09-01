using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public partial class GameUIHandler : MonoBehaviour
{
    [Header("Core UI")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI resourceText;
    [SerializeField] private TextMeshProUGUI handCountText;
    [SerializeField] private TextMeshProUGUI deckCountText;
    
    [Header("Resource Display")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider creativitySlider;
    
    // State tracking
    private int _lastHandCount = -1;
    private int _lastDeckCount = -1;
    private float _lastHealth = -1f;
    private float _lastCreativity = -1f;
    
    // Performance optimization
    private const float UPDATE_INTERVAL = 0.1f;
    private float _lastUpdate = 0f;
    
    // === INITIALIZATION ===
    
    private void Start()
    {
        InitializeUI();
        SubscribeToEvents();
        StartCoroutine(DelayedInitialUpdate());
    }
    
    private void InitializeUI()
    {
        SetupStatusText();
        ResetDisplays();
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
        UpdateHealth(100, 100);
        UpdateCreativity(3, 3);
    }
    
    private IEnumerator DelayedInitialUpdate()
    {
        yield return new WaitForSeconds(0.5f);
        ForceFullUpdate();
        SetStatus("Ready to play!", Color.green);
    }
    
    // === EVENT SUBSCRIPTION ===
    
    private void SubscribeToEvents()
    {
        // Card Manager Events
        CardManager.OnHandUpdated += OnHandUpdated;
        CardManager.OnSelectionChanged += OnSelectionChanged;
        CardManager.OnCardSpawned += OnCardSpawned;
        CardManager.OnCardDestroyed += OnCardDestroyed;
        
        // Deck Manager Events  
        DeckManager.OnDeckSizeChanged += OnDeckSizeChanged;
        DeckManager.OnCardDrawn += OnCardDrawn;
        DeckManager.OnCardDiscarded += OnCardDiscarded;
        
        // Drag Handler Events
        Handler.CardDragHandler.OnCardDragStart.AddListener(OnCardDragStart);
        Handler.CardDragHandler.OnCardDragEnd.AddListener(OnCardDragEnd);
        
        // Combat Manager Events
        CombatManager.OnLifeChanged += OnLifeChanged;
        CombatManager.OnCreativityChanged += OnCreativityChanged;
        CombatManager.OnTurnPhaseChanged += OnTurnPhaseChanged;
        CombatManager.OnCombatStarted += OnCombatStarted;
    }
    
    private void UnsubscribeFromEvents()
    {
        // Card Manager Events
        CardManager.OnHandUpdated -= OnHandUpdated;
        CardManager.OnSelectionChanged -= OnSelectionChanged;
        CardManager.OnCardSpawned -= OnCardSpawned;
        CardManager.OnCardDestroyed -= OnCardDestroyed;
        
        // Deck Manager Events
        DeckManager.OnDeckSizeChanged -= OnDeckSizeChanged;
        DeckManager.OnCardDrawn -= OnCardDrawn;
        DeckManager.OnCardDiscarded -= OnCardDiscarded;
        
        // Drag Handler Events
        Handler.CardDragHandler.OnCardDragStart.RemoveListener(OnCardDragStart);
        Handler.CardDragHandler.OnCardDragEnd.RemoveListener(OnCardDragEnd);
        
        // Combat Manager Events
        CombatManager.OnLifeChanged -= OnLifeChanged;
        CombatManager.OnCreativityChanged -= OnCreativityChanged;
        CombatManager.OnTurnPhaseChanged -= OnTurnPhaseChanged;
        CombatManager.OnCombatStarted -= OnCombatStarted;
    }
    
    // === EVENT HANDLERS ===
    
    private void OnHandUpdated(List<Card> handCards)
    {
        if (handCards != null)
        {
            UpdateHandCount(handCards.Count);
        }
    }
    
    private void OnSelectionChanged(List<Card> selectedCards)
    {
        if (selectedCards == null) return;
        
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
    
    private void OnDeckSizeChanged(int deckSize)
    {
        UpdateDeckCount(deckSize);
    }
    
    private void OnCardDrawn(CardData cardData)
    {
        if (cardData != null)
        {
            SetStatus($"Drew: {cardData.cardName}", Color.green);
        }
    }
    
    private void OnCardDiscarded(CardData cardData)
    {
        if (cardData != null)
        {
            SetStatus($"Discarded: {cardData.cardName}", new Color(1f, 0.5f, 0f));
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
        }
    }
    
    private void OnTurnPhaseChanged(TurnPhase phase)
    {
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
    }
    
    // === UI UPDATE SYSTEM ===
    
    private void Update()
    {
        if (Time.time - _lastUpdate < UPDATE_INTERVAL) return;
        _lastUpdate = Time.time;
        
        UpdateFromManagers();
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
        });
        
        UpdateFromManagers();
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
#endif
}