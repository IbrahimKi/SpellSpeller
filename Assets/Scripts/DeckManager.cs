using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DeckManager : SingletonBehaviour<DeckManager>
{
    [Header("Deck Configuration")]
    [SerializeField] private List<CardData> startingDeck = new List<CardData>();
    [SerializeField] private List<CardData> fallbackCards = new List<CardData>();
    [SerializeField] private bool shuffleOnStart = true;
    [SerializeField] private int defaultDeckSize = 30;
    
    [Header("Test Deck Settings")]
    [SerializeField] private int testDeckSize = 20;
    [SerializeField] private bool allowDuplicatesInTestDeck = true;
    [SerializeField] private int maxDuplicatesPerCard = 3;
    
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private float managerWaitTimeout = 10f;
    
    private Queue<CardData> _deck = new Queue<CardData>();
    private List<CardData> _discardPile = new List<CardData>();
    private List<CardData> _originalDeck = new List<CardData>();
    
    // Events
    public static event System.Action<int> OnDeckSizeChanged;
    public static event System.Action<CardData> OnCardDrawn;
    public static event System.Action<CardData> OnCardDiscarded;
    public static event System.Action OnDeckShuffled;
    public static event System.Action OnDeckEmpty;
    public static event System.Action OnDeckInitialized;
    public static event System.Action<int> OnTestDeckGenerated;
    
    // Properties
    public int DeckSize => _deck.Count;
    public int DiscardSize => _discardPile.Count;
    public bool IsDeckEmpty => _deck.Count == 0;
    public bool HasCardsToShuffle => _discardPile.Count > 0;
    public bool IsInitialized { get; private set; }
    
    protected override void OnAwakeInitialize()
    {
        StartCoroutine(InitializationSequence());
    }
    
    private void OnEnable()
    {
        if (CardManager.HasInstance)
            CardManager.OnCardManagerInitialized += OnCardManagerReady;
    }
    
    private void OnDisable()
    {
        if (CardManager.HasInstance)
            CardManager.OnCardManagerInitialized -= OnCardManagerReady;
    }
    
    private IEnumerator InitializationSequence()
    {
        LogDebug("[DeckManager] Starting initialization sequence...");
        
        // Längeres Timeout für CardManager
        float elapsed = 0f;
        
        while (elapsed < managerWaitTimeout)
        {
            if (CardManager.HasInstance && CardManager.Instance.IsInitialized)
            {
                LogDebug("[DeckManager] CardManager ready, initializing deck...");
                break;
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        if (elapsed >= managerWaitTimeout)
        {
            Debug.LogWarning("[DeckManager] CardManager timeout, proceeding with fallback...");
        }
        
        InitializeDeck();
    }
    
    private void OnCardManagerReady()
    {
        if (!IsInitialized)
        {
            LogDebug("[DeckManager] CardManager ready event received, initializing...");
            InitializeDeck();
        }
    }
    
    private void InitializeDeck()
    {
        if (IsInitialized) 
        {
            LogDebug("[DeckManager] Already initialized, skipping...");
            return;
        }
        
        LogDebug("[DeckManager] Starting deck initialization...");
        
        List<CardData> cardsToUse = null;
        
        // 1. Prüfe startingDeck zuerst
        if (ValidateDeck(startingDeck))
        {
            cardsToUse = new List<CardData>(startingDeck);
            LogDebug($"[DeckManager] Using predefined starting deck ({cardsToUse.Count} cards)");
        }
        // 2. Versuche CardManager Karten zu holen
        else if (CardManager.HasInstance && CardManager.Instance.IsInitialized)
        {
            var availableCards = GetAvailableCards();
            if (availableCards.Count > 0)
            {
                cardsToUse = GenerateDeckFromAvailable(availableCards);
                LogDebug($"[DeckManager] Generated deck from CardManager ({cardsToUse.Count} cards)");
            }
            else
            {
                Debug.LogWarning("[DeckManager] CardManager has no available cards!");
            }
        }
        // 3. Fallback Cards verwenden
        if (cardsToUse == null && ValidateDeck(fallbackCards))
        {
            cardsToUse = new List<CardData>(fallbackCards);
            Debug.LogWarning($"[DeckManager] Using fallback deck ({cardsToUse.Count} cards)");
        }
        
        // 4. Letzte Option: Minimales Test-Deck
        if (cardsToUse == null || cardsToUse.Count == 0)
        {
            Debug.LogError("[DeckManager] No valid cards found anywhere! Creating emergency deck...");
            cardsToUse = CreateEmergencyDeck();
        }
        
        if (cardsToUse.Count == 0)
        {
            Debug.LogError("[DeckManager] CRITICAL: No cards available at all! Check your CardData assignments!");
            return;
        }
        
        // Deck aufsetzen
        _originalDeck = cardsToUse;
        ResetDeck();
        
        IsInitialized = true;
        OnDeckInitialized?.Invoke();
        
        Debug.Log($"[DeckManager] Initialization complete - {DeckSize} cards ready");
    }
    
    private List<CardData> GetAvailableCards()
    {
        if (!CardManager.HasInstance || !CardManager.Instance.IsInitialized) 
        {
            LogDebug("[DeckManager] CardManager not available for card retrieval");
            return new List<CardData>();
        }
        
        // Verwende die neue öffentliche Methode statt Reflection
        try
        {
            return CardManager.Instance.GetAllCardData();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[DeckManager] Failed to get cards from CardManager: {ex.Message}");
            return new List<CardData>();
        }
    }
    
    private List<CardData> GenerateDeckFromAvailable(List<CardData> availableCards)
    {
        var generatedDeck = new List<CardData>();
        var cardCounts = new Dictionary<CardData, int>();
        int attempts = 0;
        int maxAttempts = defaultDeckSize * 3; // Erhöhte Versuche
        
        LogDebug($"[DeckManager] Generating deck from {availableCards.Count} available cards");
        
        while (generatedDeck.Count < defaultDeckSize && attempts < maxAttempts)
        {
            var randomCard = availableCards[Random.Range(0, availableCards.Count)];
            if (randomCard != null)
            {
                int currentCount = cardCounts.GetValueOrDefault(randomCard, 0);
                if (currentCount < maxDuplicatesPerCard)
                {
                    generatedDeck.Add(randomCard);
                    cardCounts[randomCard] = currentCount + 1;
                }
            }
            attempts++;
        }
        
        LogDebug($"[DeckManager] Generated {generatedDeck.Count} cards from {cardCounts.Count} unique cards");
        return generatedDeck;
    }
    
    private List<CardData> CreateEmergencyDeck()
    {
        Debug.LogWarning("[DeckManager] Creating emergency test deck...");
        
        var emergencyDeck = new List<CardData>();
        
        // Wenn fallbackCards vorhanden sind, verwende sie
        if (fallbackCards != null && fallbackCards.Count > 0)
        {
            for (int i = 0; i < defaultDeckSize; i++)
            {
                var card = fallbackCards[i % fallbackCards.Count];
                if (card != null) emergencyDeck.Add(card);
            }
        }
        
        LogDebug($"[DeckManager] Emergency deck created with {emergencyDeck.Count} cards");
        return emergencyDeck;
    }
    
    public void GenerateTestDeck()
    {
        Debug.Log("[DeckManager] Generating test deck...");
        
        List<CardData> availableCards = GetAvailableCards();
        
        // Fallback zu existierenden Karten wenn CardManager nicht verfügbar
        if (availableCards.Count == 0)
        {
            if (fallbackCards.Count > 0)
                availableCards = fallbackCards;
            else if (_originalDeck.Count > 0)
                availableCards = _originalDeck;
            else
            {
                Debug.LogError("[DeckManager] No cards available for test deck generation");
                return;
            }
        }
        
        var testDeck = new List<CardData>();
        var cardCounts = new Dictionary<CardData, int>();
        
        for (int i = 0; i < testDeckSize; i++)
        {
            var selectedCard = availableCards[Random.Range(0, availableCards.Count)];
            
            if (selectedCard != null)
            {
                int currentCount = cardCounts.GetValueOrDefault(selectedCard, 0);
                if (allowDuplicatesInTestDeck || currentCount < maxDuplicatesPerCard)
                {
                    testDeck.Add(selectedCard);
                    cardCounts[selectedCard] = currentCount + 1;
                }
            }
        }
        
        _originalDeck = testDeck;
        ResetDeck();
        
        OnTestDeckGenerated?.Invoke(testDeckSize);
        Debug.Log($"[DeckManager] Test deck generated: {testDeckSize} cards ({cardCounts.Count} unique)");
    }
    
    private bool ValidateDeck(List<CardData> deck)
    {
        if (deck == null || deck.Count == 0) 
        {
            LogDebug("[DeckManager] Deck validation failed: null or empty");
            return false;
        }
        
        int validCards = deck.Count(card => card != null);
        if (validCards == 0)
        {
            Debug.LogWarning("[DeckManager] Deck validation failed: no valid cards");
            return false;
        }
        
        if (validCards < deck.Count)
        {
            Debug.LogWarning($"[DeckManager] Deck has {deck.Count - validCards} null cards out of {deck.Count}");
        }
        
        LogDebug($"[DeckManager] Deck validation passed: {validCards} valid cards");
        return true;
    }
    
    public void ResetDeck()
    {
        _deck.Clear();
        _discardPile.Clear();
        
        foreach (var cardData in _originalDeck)
        {
            if (cardData != null)
                _deck.Enqueue(cardData);
        }
        
        if (shuffleOnStart && _deck.Count > 1)
            ShuffleDeck();
        
        OnDeckSizeChanged?.Invoke(DeckSize);
        LogDebug($"[DeckManager] Deck reset with {DeckSize} cards");
    }
    
    public CardData DrawCard()
    {
        if (_deck.Count == 0)
        {
            LogDebug("[DeckManager] Deck empty, trying to shuffle discard pile");
            if (!TryShuffleDiscardIntoDeck())
            {
                Debug.LogWarning("[DeckManager] No cards to draw!");
                OnDeckEmpty?.Invoke();
                return null;
            }
        }
        
        var drawnCard = _deck.Dequeue();
        OnCardDrawn?.Invoke(drawnCard);
        OnDeckSizeChanged?.Invoke(DeckSize);
        
        LogDebug($"[DeckManager] Drew card: {drawnCard?.cardName ?? "null"}, {DeckSize} remaining");
        return drawnCard;
    }
    
    public List<CardData> DrawCards(int count)
    {
        var drawnCards = new List<CardData>();
        int maxDrawable = _deck.Count + _discardPile.Count;
        int actualCount = Mathf.Min(count, maxDrawable);
        
        LogDebug($"[DeckManager] Drawing {actualCount} cards (requested {count})");
        
        for (int i = 0; i < actualCount; i++)
        {
            var card = DrawCard();
            if (card != null)
                drawnCards.Add(card);
            else
                break;
        }
        
        return drawnCards;
    }
    
    public void DiscardCard(CardData cardData)
    {
        if (cardData == null) return;
        
        _discardPile.Add(cardData);
        OnCardDiscarded?.Invoke(cardData);
        LogDebug($"[DeckManager] Discarded: {cardData.cardName}, {DiscardSize} in discard pile");
    }
    
    public void ShuffleDeck()
    {
        if (_deck.Count <= 1) return;
        
        var deckList = _deck.ToList();
        _deck.Clear();
        
        // Fisher-Yates shuffle
        for (int i = deckList.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            (deckList[i], deckList[randomIndex]) = (deckList[randomIndex], deckList[i]);
        }
        
        foreach (var card in deckList)
            _deck.Enqueue(card);
        
        OnDeckShuffled?.Invoke();
        LogDebug($"[DeckManager] Deck shuffled ({deckList.Count} cards)");
    }
    
    public bool TryShuffleDiscardIntoDeck()
    {
        if (_discardPile.Count == 0) 
        {
            LogDebug("[DeckManager] No discard pile to shuffle");
            return false;
        }
        
        LogDebug($"[DeckManager] Shuffling {_discardPile.Count} cards from discard into deck");
        
        foreach (var card in _discardPile)
            _deck.Enqueue(card);
        
        _discardPile.Clear();
        ShuffleDeck();
        OnDeckSizeChanged?.Invoke(DeckSize);
        
        return true;
    }
    
    public void ForceInitialization()
    {
        Debug.Log("[DeckManager] Force initialization requested");
        IsInitialized = false;
        InitializeDeck();
    }
    
    // Utility Methods
    private void LogDebug(string message)
    {
        if (enableDebugLogs)
            Debug.Log(message);
    }
    
    // Public utility methods
    public bool HasEnoughCards(int requiredCount)
    {
        return (DeckSize + DiscardSize) >= requiredCount;
    }
    
    public int GetTotalAvailableCards()
    {
        return DeckSize + DiscardSize;
    }
    
    public CardData PeekTopCard()
    {
        return _deck.Count > 0 ? _deck.Peek() : null;
    }
    
    public List<CardData> GetDiscardPileContents()
    {
        return new List<CardData>(_discardPile);
    }
    
#if UNITY_EDITOR
    [ContextMenu("Generate Test Deck")]
    public void DebugGenerateTestDeck() => GenerateTestDeck();
    
    [ContextMenu("Force Initialize")]
    public void DebugForceInitialize() => ForceInitialization();
    
    [ContextMenu("Debug Full Status")]
    public void DebugFullStatus()
    {
        Debug.Log("=== DECKMANAGER DEBUG ===");
        Debug.Log($"IsInitialized: {IsInitialized}");
        Debug.Log($"Deck Size: {DeckSize}");
        Debug.Log($"Discard Size: {DiscardSize}");
        Debug.Log($"Total Available: {GetTotalAvailableCards()}");
        Debug.Log($"Starting Deck Count: {startingDeck?.Count ?? 0}");
        Debug.Log($"Fallback Cards Count: {fallbackCards?.Count ?? 0}");
        Debug.Log($"Original Deck Count: {_originalDeck?.Count ?? 0}");
        Debug.Log($"CardManager HasInstance: {CardManager.HasInstance}");
        
        if (CardManager.HasInstance)
        {
            Debug.Log($"CardManager IsInitialized: {CardManager.Instance.IsInitialized}");
            try 
            {
                Debug.Log($"Available Cards from CM: {CardManager.Instance.GetAvailableCardCount()}");
            }
            catch (System.Exception ex)
            {
                Debug.Log($"Error getting CardManager cards: {ex.Message}");
            }
        }
        
        if (DeckSize > 0)
        {
            var topCard = PeekTopCard();
            Debug.Log($"Top Card: {topCard?.cardName ?? "null"}");
        }
        
        Debug.Log("=== END DEBUG ===");
    }
    
    [ContextMenu("Log Deck Contents")]
    public void LogDeckContents()
    {
        Debug.Log($"=== DECK CONTENTS ({DeckSize} cards) ===");
        var deckArray = _deck.ToArray();
        for (int i = 0; i < deckArray.Length && i < 10; i++) // Max 10 cards
        {
            Debug.Log($"  {i}: {deckArray[i]?.cardName ?? "null"}");
        }
        if (deckArray.Length > 10)
            Debug.Log($"  ... and {deckArray.Length - 10} more cards");
        Debug.Log("=== END CONTENTS ===");
    }
#endif
}