using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class CardPlayHandler : MonoBehaviour
{
    [Header("Button References")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private Button drawButton;
    
    [Header("Settings")]
    [SerializeField] private int maxHistoryEntries = 10;
    [SerializeField] private int cardsPerDraw = 1;
    [SerializeField] private bool autoUpdateLayout = true;
    [SerializeField] private int initialHandSize = 5;
    [SerializeField] private float managerTimeout = 5f;
    
    private List<Card> _selectedCards = new List<Card>();
    
    // History tracking
    private Queue<string> _playHistory = new Queue<string>();
    private Queue<string> _spellHistory = new Queue<string>();
    
    // Manager state tracking
    private bool _managersInitialized = false;
    private Coroutine _initializationCoroutine;
    
    // Events
    public static event System.Action<List<Card>> OnCardsPlayed;
    public static event System.Action<int> OnCardsDrawn;
    public static event System.Action<Queue<string>, Queue<string>> OnHistoryUpdated;
    public static event System.Action OnManagersReady;
    
    // Properties for safe manager access
    private CardManager CardManagerInstance => CardManager.HasInstance ? CardManager.Instance : null;
    private DeckManager DeckManagerInstance => DeckManager.HasInstance ? DeckManager.Instance : null;
    private HandLayoutManager HandLayoutManagerInstance => HandLayoutManager.HasInstance ? HandLayoutManager.Instance : null;
    private SpellcastManager SpellcastManagerInstance => SpellcastManager.HasInstance ? SpellcastManager.Instance : null;
    
    private void Awake()
    {
        SetupButtons();
        _initializationCoroutine = StartCoroutine(WaitForManagerInitialization());
    }
    
    private void SetupButtons()
    {
        playButton?.onClick.AddListener(PlaySelectedCards);
        clearButton?.onClick.AddListener(ClearSelection);
        drawButton?.onClick.AddListener(DrawCardsFromDeck);
        
        // Initially disable buttons until managers are ready
        SetButtonsInteractable(false);
    }
    
    // Ersetze WaitForManagerInitialization() mit:
    private IEnumerator WaitForManagerInitialization()
    {
        yield return new WaitForSeconds(0.1f); // Kurze Pause für Awake-Calls
    
        // Prüfe Manager-Status
        _managersInitialized = CheckManagersReady();
    
        if (_managersInitialized)
        {
            OnManagersInitialized();
        }
        else
        {
            Debug.LogError("[CardPlayHandler] Managers not ready! Check GameManager setup.");
            // Trotzdem aktivieren für Debug
            SetButtonsInteractable(true);
        }
    }

// Ersetze CheckManagersReady() mit:
    private bool CheckManagersReady()
    {
        bool cardManagerReady = CardManager.HasInstance && CardManager.Instance.IsInitialized;
        bool deckManagerReady = DeckManager.HasInstance && DeckManager.Instance.IsInitialized;
    
        if (!cardManagerReady)
            Debug.LogWarning("[CardPlayHandler] CardManager not ready");
        if (!deckManagerReady)
            Debug.LogWarning("[CardPlayHandler] DeckManager not ready");
        
        return cardManagerReady && deckManagerReady;
    }
    
    private void OnManagersInitialized()
    {
        Debug.Log("[CardPlayHandler] All managers ready, enabling functionality");
        
        // Enable buttons
        SetButtonsInteractable(true);
        
        // Subscribe to events now that managers are ready
        SubscribeToEvents();
        
        // Draw initial hand if in combat or requested
        if (CombatManager.HasInstance && CombatManager.Instance.IsInCombat)
        {
            DrawInitialHand();
        }
        
        OnManagersReady?.Invoke();
    }
    
    private void SetButtonsInteractable(bool interactable)
    {
        if (playButton) playButton.interactable = interactable;
        if (clearButton) clearButton.interactable = interactable;
        if (drawButton) drawButton.interactable = interactable;
    }
    
    private void SubscribeToEvents()
    {
        // Only subscribe when managers are ready
        if (CardManagerInstance != null)
        {
            CardManager.OnSelectionChanged += OnSelectionChanged;
            CardManager.OnHandUpdated += OnHandUpdated;
        }
        
        if (SpellcastManagerInstance != null)
        {
            SpellcastManager.OnSpellFound += OnSpellFound;
        }
        
        Card.OnCardPlayTriggered += OnCardPlayTriggered;
        
        if (CombatManager.HasInstance)
        {
            CombatManager.OnCombatStarted += OnCombatStarted;
        }
    }
    
    private void OnEnable()
    {
        // Only subscribe if managers are already ready
        if (_managersInitialized)
        {
            SubscribeToEvents();
        }
    }
    
    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }
    
    private void UnsubscribeFromEvents()
    {
        CardManager.OnSelectionChanged -= OnSelectionChanged;
        CardManager.OnHandUpdated -= OnHandUpdated;
        SpellcastManager.OnSpellFound -= OnSpellFound;
        Card.OnCardPlayTriggered -= OnCardPlayTriggered;
        CombatManager.OnCombatStarted -= OnCombatStarted;
    }
    
    private void OnDestroy()
    {
        if (_initializationCoroutine != null)
        {
            StopCoroutine(_initializationCoroutine);
        }
        
        playButton?.onClick.RemoveAllListeners();
        clearButton?.onClick.RemoveAllListeners();
        drawButton?.onClick.RemoveAllListeners();
        
        UnsubscribeFromEvents();
    }
    
    // Event Handlers
    private void OnCombatStarted() => ClearSelection();
    
    private void OnCardPlayTriggered(Card card)
    {
        if (card != null && _managersInitialized) 
            PlayCards(new List<Card> { card });
    }
    
    private void OnSelectionChanged(List<Card> selectedCards)
    {
        _selectedCards.Clear();
        if (selectedCards != null) _selectedCards.AddRange(selectedCards);
    }
    
    private void OnHandUpdated(List<Card> handCards) 
    {
        if (autoUpdateLayout && _managersInitialized) 
            HandLayoutManagerInstance?.UpdateLayout();
    }
    
    private void DrawInitialHand()
    {
        if (!CanPerformAction()) return;
        
        StartCoroutine(DrawInitialHandCoroutine());
    }
    
    private IEnumerator DrawInitialHandCoroutine()
    {
        int cardsDrawn = 0;
        for (int i = 0; i < initialHandSize && CanDraw(); i++)
        {
            var drawnCard = DeckManagerInstance.DrawCard();
            if (drawnCard != null)
            {
                CardManagerInstance.SpawnCard(drawnCard, null, true);
                cardsDrawn++;
                yield return null; // Spread over multiple frames
            }
            else break;
        }
        
        if (cardsDrawn > 0)
        {
            OnCardsDrawn?.Invoke(cardsDrawn);
            if (autoUpdateLayout) HandLayoutManagerInstance?.UpdateLayout();
        }
    }
    
    // Core Actions
    public void PlaySelectedCards()
    {
        if (!CanPerformAction() || _selectedCards.Count == 0) return;
        PlayCards(_selectedCards);
    }
    
    private void PlayCards(List<Card> cardsToPlay)
    {
        if (!CanPerformAction() || cardsToPlay.Count == 0) return;
    
        string letterSequence = CardManager.GetLetterSequenceFromCards(cardsToPlay);
        if (string.IsNullOrEmpty(letterSequence)) return;
    
        OnCardsPlayed?.Invoke(cardsToPlay);
        SpellcastManagerInstance?.ProcessCardPlay(cardsToPlay, letterSequence);
        AddToPlayHistory(letterSequence);
    
        // Remove cards efficiently
        var cardsToRemove = new List<Card>(cardsToPlay);
        foreach (var card in cardsToRemove)
        {
            if (card?.CardData != null)
            {
                DeckManagerInstance?.DiscardCard(card.CardData);
                CardManagerInstance?.RemoveCardFromHand(card);
                HandLayoutManagerInstance?.CleanupCardReference(card);
                
                if (card.gameObject != null) Destroy(card.gameObject);
            }
        }
    
        if (cardsToPlay == _selectedCards) _selectedCards.Clear();
        if (autoUpdateLayout) HandLayoutManagerInstance?.UpdateLayout();
    }
    
    public void DrawCardsFromDeck()
    {
        if (!CanPerformAction() || !CanDraw()) 
        {
            Debug.LogWarning("[CardPlayHandler] Cannot draw cards - conditions not met");
            return;
        }
        
        StartCoroutine(DrawCardsCoroutine());
    }
    
    private IEnumerator DrawCardsCoroutine()
    {
        int cardsDrawn = 0;
        for (int i = 0; i < cardsPerDraw && CanDraw(); i++)
        {
            var drawnCard = DeckManagerInstance.DrawCard();
            if (drawnCard != null)
            {
                CardManagerInstance.SpawnCard(drawnCard, null, true);
                cardsDrawn++;
                yield return null; // Spread drawing over frames for better performance
            }
            else 
            {
                Debug.LogWarning($"[CardPlayHandler] Failed to draw card {i+1}/{cardsPerDraw}");
                break;
            }
        }
        
        if (cardsDrawn > 0)
        {
            OnCardsDrawn?.Invoke(cardsDrawn);
            if (autoUpdateLayout) HandLayoutManagerInstance?.UpdateLayout();
            Debug.Log($"[CardPlayHandler] Drew {cardsDrawn} cards");
        }
    }
    
    public void ClearSelection()
    {
        if (!CanPerformAction()) return;
        
        CardManagerInstance?.ClearSelection();
        _selectedCards.Clear();
    }
    
    // Spell Event Handlers
    private void OnSpellFound(SpellAsset spell, string usedLetters)
    {
        AddToSpellHistory(spell.SpellName, usedLetters);
    }
    
    // History Management
    private void AddToPlayHistory(string letters)
    {
        var entry = $"[{System.DateTime.Now:HH:mm:ss}] Played: {letters}";
        _playHistory.Enqueue(entry);
        if (_playHistory.Count > maxHistoryEntries) _playHistory.Dequeue();
        OnHistoryUpdated?.Invoke(_playHistory, _spellHistory);
    }
    
    private void AddToSpellHistory(string spellName, string letters)
    {
        var entry = $"[{System.DateTime.Now:HH:mm:ss}] Cast: {spellName} ({letters})";
        _spellHistory.Enqueue(entry);
        if (_spellHistory.Count > maxHistoryEntries) _spellHistory.Dequeue();
        OnHistoryUpdated?.Invoke(_playHistory, _spellHistory);
    }
    
    private bool CanPerformAction()
    {
        return _managersInitialized && CardManagerInstance != null && DeckManagerInstance != null;
    }
    
    private bool CanDraw()
    {
        return CanPerformAction() && 
               !CardManagerInstance.IsHandFull && 
               !DeckManagerInstance.IsDeckEmpty;
    }
    
    // Properties
    public bool HasSelectedCards => _selectedCards.Count > 0;
    public int SelectedCardCount => _selectedCards.Count;
    public IReadOnlyCollection<string> PlayHistory => _playHistory;
    public IReadOnlyCollection<string> SpellHistory => _spellHistory;
    public bool ManagersInitialized => _managersInitialized;
    
    // Public Methods
    public void ClearHistory()
    {
        _playHistory.Clear();
        _spellHistory.Clear();
        OnHistoryUpdated?.Invoke(_playHistory, _spellHistory);
    }
    
    public void ForceDrawCards(int count)
    {
        if (!CanPerformAction()) return;
        
        int originalCardsPerDraw = cardsPerDraw;
        cardsPerDraw = count;
        DrawCardsFromDeck();
        cardsPerDraw = originalCardsPerDraw;
    }
    
    // Debug Methods
    [ContextMenu("Force Manager Check")]
    public void DebugCheckManagers()
    {
        Debug.Log($"[CardPlayHandler] Manager Status:");
        Debug.Log($"  CardManager: {(CardManager.HasInstance ? "Available" : "Missing")} " +
                  $"{(CardManager.HasInstance && CardManager.Instance.IsInitialized ? "(Initialized)" : "(Not Initialized)")}");
        Debug.Log($"  DeckManager: {(DeckManager.HasInstance ? "Available" : "Missing")} " +
                  $"{(DeckManager.HasInstance && DeckManager.Instance.IsInitialized ? "(Initialized)" : "(Not Initialized)")}");
        Debug.Log($"  Managers Ready: {_managersInitialized}");
        Debug.Log($"  Can Draw: {CanDraw()}");
    }
}