using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CardSystem.Extensions;
using GameSystem.Extensions;

public class DeckManager : SingletonBehaviour<DeckManager>, IGameManager
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
    
    public bool IsReady => IsInitialized;
    // Events
    public static event System.Action<int> OnDeckSizeChanged;
    public static event System.Action<CardData> OnCardDrawn;
    public static event System.Action<CardData> OnCardDiscarded;
    public static event System.Action OnDeckShuffled;
    public static event System.Action OnDeckEmpty;
    public static event System.Action OnDeckInitialized;
    public static event System.Action<int> OnTestDeckGenerated;
    public static event System.Action<int> OnDiscardPileSizeChanged;
    
    // Properties
    public int DeckSize => _deck.Count;
    public int DiscardSize => _discardPile.Count;
    public bool IsDeckEmpty => _deck.Count == 0;
    public bool HasCardsToShuffle => _discardPile.Count > 0;
    public bool IsInitialized { get; private set; }
    
    protected override void OnAwakeInitialize()
    {
        InitializeDeck();
        if (_deck.Count == 0)
        {
            GenerateTestDeck();
        }
        IsInitialized = true;
        OnDeckInitialized?.Invoke();
        OnDeckSizeChanged?.Invoke(DeckSize);
    }
    
    private void InitializeDeck()
    {
        if (IsInitialized) return;
        
        List<CardData> cardsToUse = new List<CardData>();
        
        // 1. StartingDeck verwenden
        if (startingDeck != null && startingDeck.Count > 0)
        {
            cardsToUse.AddRange(startingDeck.Where(c => c != null));
            LogDebug($"[DeckManager] Using starting deck with {cardsToUse.Count} cards");
        }
        
        // 2. Fallback Cards
        if (cardsToUse.Count == 0 && fallbackCards != null)
        {
            cardsToUse.AddRange(fallbackCards.Where(c => c != null));
            LogDebug($"[DeckManager] Using fallback cards with {cardsToUse.Count} cards");
        }
        
        if (cardsToUse.Count > 0)
        {
            _originalDeck = cardsToUse;
            ResetDeck();
        }
    }
    
    public void GenerateTestDeck()
    {
        LogDebug("[DeckManager] Generating test deck...");
        
        List<CardData> availableCards = new List<CardData>();
        
        // Versuche Karten aus CardManager zu holen
        if (CardManager.HasInstance && CardManager.Instance.IsInitialized)
        {
            availableCards = CardManager.Instance.GetAllCardData();
            LogDebug($"[DeckManager] Got {availableCards.Count} cards from CardManager");
        }
        
        // Fallback zu existierenden Karten
        if (availableCards.Count == 0)
        {
            if (fallbackCards != null && fallbackCards.Count > 0)
            {
                availableCards = fallbackCards.Where(c => c != null).ToList();
                LogDebug($"[DeckManager] Using {availableCards.Count} fallback cards for test deck");
            }
            else
            {
                Debug.LogError("[DeckManager] No cards available for test deck generation");
                return;
            }
        }
        
        var testDeck = new List<CardData>();
        var cardCounts = new Dictionary<CardData, int>();
        
        for (int i = 0; i < testDeckSize && testDeck.Count < testDeckSize; i++)
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
                else
                {
                    i--; // Retry mit anderem Card
                }
            }
        }
        
        _originalDeck = testDeck;
        ResetDeck();
        
        OnTestDeckGenerated?.Invoke(testDeckSize);
        LogDebug($"[DeckManager] Test deck generated: {DeckSize} cards ({cardCounts.Count} unique)");
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
        OnDiscardPileSizeChanged?.Invoke(DiscardSize);
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
        OnDiscardPileSizeChanged?.Invoke(DiscardSize);
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
        OnDiscardPileSizeChanged?.Invoke(DiscardSize);
        
        return true;
    }
    
    public void ShuffleDiscardIntoDeck()
    {
        if (_discardPile.Count == 0)
        {
            LogDebug("[DeckManager] No discard pile to shuffle");
            return;
        }
    
        LogDebug($"[DeckManager] Shuffling {_discardPile.Count} cards from discard to bottom of deck");
    
        // Convert current deck to list
        var currentDeck = _deck.ToList();
        _deck.Clear();
    
        // Shuffle discard pile
        var shuffledDiscard = new List<CardData>(_discardPile);
        for (int i = shuffledDiscard.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            (shuffledDiscard[i], shuffledDiscard[randomIndex]) = (shuffledDiscard[randomIndex], shuffledDiscard[i]);
        }
    
        // Re-enqueue: current deck first, then shuffled discard pile (bottom)
        foreach (var card in currentDeck)
            _deck.Enqueue(card);
    
        foreach (var card in shuffledDiscard)
            _deck.Enqueue(card);
    
        // Clear discard pile
        _discardPile.Clear();
    
        // Fire events
        OnDeckSizeChanged?.Invoke(DeckSize);
        OnDiscardPileSizeChanged?.Invoke(DiscardSize);
        OnDeckShuffled?.Invoke();
    
        LogDebug($"[DeckManager] Shuffle complete. Deck size: {DeckSize}");
    }

    public bool TryDrawCard()
    {
        if (IsDeckEmpty) return false;

        var cardData = DrawCard();
        if (cardData != null)
        {
            // Spawn card in hand
            CoreExtensions.TryWithManagerStatic<CardManager>( cm => 
                cm.SpawnCard(cardData, null, true)
            );
            return true;
        }
        return false;
    }
    
    public void ForceInitialization()
    {
        Debug.Log("[DeckManager] Force initialization requested");
        IsInitialized = false;
        InitializeDeck();
        
        if (_deck.Count == 0)
            GenerateTestDeck();
            
        IsInitialized = true;
        OnDeckInitialized?.Invoke();
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
    public void AddCardToBottom(CardData cardData)
    {
        if (cardData == null) return;

        // Convert to list, add at end, rebuild queue
        var deckList = _deck.ToList();
        deckList.Add(cardData);

        _deck.Clear();
        foreach (var card in deckList)
            _deck.Enqueue(card);

        OnDeckSizeChanged?.Invoke(DeckSize);
        LogDebug($"[DeckManager] Added {cardData.cardName} to bottom of deck");
    }
}