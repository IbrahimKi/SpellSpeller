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
    
    private IEnumerator InitializationSequence()
    {
        LogDebug("[DeckManager] Starting initialization sequence...");
        
        // Sofort initialisieren wenn möglich
        if (startingDeck != null && startingDeck.Count > 0)
        {
            InitializeDeck();
            yield break;
        }
        
        // Warte max 2 Sekunden auf CardManager
        float elapsed = 0f;
        while (elapsed < 2f && !CardManager.HasInstance)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        InitializeDeck();
    }
    
    private void InitializeDeck()
    {
        if (IsInitialized) return;
        
        List<CardData> cardsToUse = new List<CardData>();
        
        // 1. StartingDeck verwenden
        if (startingDeck != null && startingDeck.Count > 0)
            cardsToUse.AddRange(startingDeck.Where(c => c != null));
        
        // 2. Fallback Cards
        if (cardsToUse.Count == 0 && fallbackCards != null)
            cardsToUse.AddRange(fallbackCards.Where(c => c != null));
        
        // 3. Emergency Test Deck
        if (cardsToUse.Count == 0)
        {
            Debug.LogError("[DeckManager] No cards available! Check your CardData assignments!");
            GenerateTestDeck();
            return;
        }
        
        _originalDeck = cardsToUse;
        ResetDeck();
        IsInitialized = true;
        OnDeckInitialized?.Invoke();
        
        Debug.Log($"[DeckManager] Initialization complete - {DeckSize} cards ready");
    }
    
    public void GenerateTestDeck()
    {
        Debug.Log("[DeckManager] Generating test deck...");
        
        List<CardData> availableCards = new List<CardData>();
        
        // Versuche Karten aus CardManager zu holen
        if (CardManager.HasInstance && CardManager.Instance.IsInitialized)
        {
            availableCards = CardManager.Instance.GetAllCardData();
        }
        
        // Fallback zu existierenden Karten
        if (availableCards.Count == 0)
        {
            if (fallbackCards.Count > 0)
                availableCards = fallbackCards;
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
        IsInitialized = true;
        
        OnTestDeckGenerated?.Invoke(testDeckSize);
        OnDeckInitialized?.Invoke();
        Debug.Log($"[DeckManager] Test deck generated: {testDeckSize} cards ({cardCounts.Count} unique)");
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
}