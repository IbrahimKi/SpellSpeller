// ========== CardManager.cs ==========
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class CardManager : SingletonBehaviour<CardManager>, IGameManager
{
    [Header("Card Database")]
    [SerializeField] private List<CardData> allCardData = new List<CardData>();
    
    [Header("Spawning")]
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Transform defaultSpawnParent;
    [SerializeField] private bool useObjectPooling = true;
    [SerializeField] private int poolSize = 20;
    
    [Header("Hand Management")]  
    [SerializeField] private Transform handContainer;
    [SerializeField] private int maxHandSize = 7;
    
    [Header("Selection")]
    [SerializeField] private int maxSelectedCards = 1;
    [SerializeField] private bool allowMultiSelect = false;
    
    public bool IsReady => IsInitialized;
    
    // Data structures
    private Dictionary<int, Card> _allCards = new Dictionary<int, Card>();
    private Dictionary<Card, int> _cardToId = new Dictionary<Card, int>();
    private List<Card> _handCards = new List<Card>();
    private List<Card> _selectedCards = new List<Card>();
    private Queue<GameObject> _cardPool = new Queue<GameObject>();
    private int _nextCardId = 0;
    
    // Batch processing for simultaneous spawns
    private bool _isBatchingUpdates = false;
    private List<Card> _pendingHandCards = new List<Card>();
    
    // Reference to HandLayoutManager
    private HandLayoutManager _handLayoutManager;
    
    // Events
    public static event System.Action<Card> OnCardSpawned;
    public static event System.Action<Card> OnCardDestroyed;
    public static event System.Action<Card> OnCardDiscarded;
    public static event System.Action<List<Card>> OnHandUpdated;
    public static event System.Action<List<Card>> OnSelectionChanged;
    public static event System.Action OnCardManagerInitialized;
    
    // Properties
    public bool IsInitialized { get; private set; }
    public List<Card> SelectedCards => new List<Card>(_selectedCards);
    public List<Card> GetHandCards() => new List<Card>(_handCards);
    public bool IsHandFull => _handCards.Count >= maxHandSize;
    public int HandSize => _handCards.Count;
    
    protected override void OnAwakeInitialize()
    {
        InitializePool();
        InitializeHandLayout();
        IsInitialized = true;
        OnCardManagerInitialized?.Invoke();
    }
    
    private void OnEnable()
    {
        Card.OnCardSelected += HandleCardSelected;
        Card.OnCardDeselected += HandleCardDeselected;
        Card.OnCardPlayTriggered += HandleCardPlayTriggered;
    }
    
    private void OnDisable()
    {
        Card.OnCardSelected -= HandleCardSelected;
        Card.OnCardDeselected -= HandleCardDeselected;
        Card.OnCardPlayTriggered -= HandleCardPlayTriggered;
    }
    
    private void InitializePool()
    {
        if (!useObjectPooling || cardPrefab == null) return;
        
        Transform poolParent = new GameObject("Card Pool").transform;
        poolParent.SetParent(transform);
        
        for (int i = 0; i < poolSize; i++)
        {
            GameObject pooledCard = Instantiate(cardPrefab, poolParent);
            pooledCard.SetActive(false);
            _cardPool.Enqueue(pooledCard);
        }
    }
    
    private void InitializeHandLayout()
    {
        if (handContainer != null)
        {
            _handLayoutManager = handContainer.GetComponent<HandLayoutManager>();
            if (_handLayoutManager == null)
                _handLayoutManager = handContainer.gameObject.AddComponent<HandLayoutManager>();
        }
    }
    
    // NEW: Batch spawn multiple cards without layout conflicts
    public List<Card> SpawnMultipleCards(List<CardData> cardDataList, Transform parent = null, bool addToHand = false)
    {
        if (cardDataList == null || cardDataList.Count == 0) return new List<Card>();
        
        var spawnedCards = new List<Card>();
        
        // Start batching to prevent multiple layout updates
        if (addToHand) StartBatchUpdate();
        
        try
        {
            foreach (var cardData in cardDataList)
            {
                var card = SpawnCardInternal(cardData, parent, addToHand);
                if (card != null) spawnedCards.Add(card);
            }
        }
        finally
        {
            // End batching and apply final layout
            if (addToHand) EndBatchUpdate();
        }
        
        return spawnedCards;
    }
    
    public Card SpawnCard(CardData cardData, Transform parent = null, bool addToHand = false)
    {
        return SpawnCardInternal(cardData, parent, addToHand);
    }
    
    private Card SpawnCardInternal(CardData cardData, Transform parent = null, bool addToHand = false)
    {
        if (cardData == null || cardPrefab == null) return null;
        
        GameObject cardObject = GetCardObject();
        if (cardObject == null) return null;
        
        // Setup card component FIRST
        Card cardComponent = cardObject.GetComponent<Card>();
        if (cardComponent == null)
        {
            ReturnToPool(cardObject);
            return null;
        }
        
        cardComponent.SetCardData(cardData);
        
        // Setup transform AFTER card setup
        if (addToHand && handContainer != null)
        {
            // For hand cards: parent to hand container
            cardObject.transform.SetParent(handContainer, false);
            
            // KRITISCH: Korrekte RectTransform Setup für Hand-Karten
            var rectTransform = cardObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // Reset auf Standard-UI Layout
                rectTransform.localPosition = Vector3.zero;
                rectTransform.localRotation = Quaternion.identity;
                rectTransform.localScale = Vector3.one; // WICHTIG: Keine Scale-Manipulation hier!
                
                // Anchors für UI Layout
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = Vector2.zero;
                
                // WICHTIG: Entferne existierende LayoutElement (HandLayoutManager setzt neue)
                var existingLayoutElement = cardObject.GetComponent<LayoutElement>();
                if (existingLayoutElement != null)
                {
                    DestroyImmediate(existingLayoutElement);
                }
            }
        }
        else
        {
            // For non-hand cards: use provided parent or default
            Transform targetParent = parent ?? defaultSpawnParent ?? transform;
            cardObject.transform.SetParent(targetParent, false);
            ResetCardTransform(cardObject);
        }
        
        cardObject.SetActive(true);
        
        // Register card
        int cardId = _nextCardId++;
        _allCards[cardId] = cardComponent;
        _cardToId[cardComponent] = cardId;
        
        if (addToHand && _handCards.Count < maxHandSize)
        {
            if (_isBatchingUpdates)
                _pendingHandCards.Add(cardComponent);
            else
                AddCardToHandInternal(cardComponent);
        }
        
        OnCardSpawned?.Invoke(cardComponent);
        return cardComponent;
    }
    
    // NEW: Batch update system
    private void StartBatchUpdate()
    {
        _isBatchingUpdates = true;
        _pendingHandCards.Clear();
    }
    
    private void EndBatchUpdate()
    {
        if (!_isBatchingUpdates) return;
        
        _isBatchingUpdates = false;
        
        // Add all pending cards at once
        foreach (var card in _pendingHandCards)
        {
            _handCards.Add(card);
        }
        _pendingHandCards.Clear();
        
        // Single layout update for all cards
        UpdateHandLayoutImmediate();
        OnHandUpdated?.Invoke(new List<Card>(_handCards));
    }
    
    private void HandleCardPlayTriggered(Card card)
    {
        if (card == null || !_selectedCards.Contains(card)) return;
        
        if (SpellcastManager.HasInstance)
            SpellcastManager.Instance.TryPlayCards(_selectedCards);
    }
    
    private GameObject GetCardObject()
    {
        if (useObjectPooling && _cardPool.Count > 0)
            return _cardPool.Dequeue();
        return cardPrefab != null ? Instantiate(cardPrefab) : null;
    }
    
    private void ResetCardTransform(GameObject cardObject)
    {
        var rectTransform = cardObject.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.localPosition = Vector3.zero;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one; // Immer 1,1,1 für UI
            
            // Standard UI Anchors
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            
            // Entferne LayoutElement falls vorhanden (wird vom HandLayoutManager neu gesetzt)
            var existingLayoutElement = cardObject.GetComponent<LayoutElement>();
            if (existingLayoutElement != null)
            {
                DestroyImmediate(existingLayoutElement);
            }
        }
    }
    
    private void AddCardToHandInternal(Card card)
    {
        _handCards.Add(card);
        // Parent already set in SpawnCard for hand cards
        
        UpdateHandLayout();
        OnHandUpdated?.Invoke(new List<Card>(_handCards));
    }
    
    // NEW: Separate discard method that actually removes the card visually
    public bool DiscardCard(Card card)
    {
        if (card == null) return false;
        
        // Remove from collections
        _handCards.Remove(card);
        _selectedCards.Remove(card);
        
        // Clean up hand layout manager reference
        _handLayoutManager?.CleanupCardReference(card);
        
        // Fire discard event BEFORE destroying
        OnCardDiscarded?.Invoke(card);
        
        // Actually destroy/hide the card
        DestroyCardInternal(card);
        
        // Update layouts and events
        UpdateHandLayout();
        OnHandUpdated?.Invoke(new List<Card>(_handCards));
        OnSelectionChanged?.Invoke(new List<Card>(_selectedCards));
        
        return true;
    }
    
    public void DestroyCard(Card card)
    {
        if (card == null) return;
        
        // Remove from tracking
        _handCards.Remove(card);
        _selectedCards.Remove(card);
        
        // Clean up hand layout manager reference
        _handLayoutManager?.CleanupCardReference(card);
        
        DestroyCardInternal(card);
        
        UpdateHandLayout();
        OnHandUpdated?.Invoke(new List<Card>(_handCards));
        OnSelectionChanged?.Invoke(new List<Card>(_selectedCards));
        OnCardDestroyed?.Invoke(card);
    }
    
    // INTERNAL method for actual card destruction/pooling
    private void DestroyCardInternal(Card card)
    {
        // Remove from tracking
        if (_cardToId.TryGetValue(card, out int cardId))
        {
            _allCards.Remove(cardId);
            _cardToId.Remove(card);
        }
        
        GameObject cardObject = card.gameObject;
        
        if (useObjectPooling)
        {
            CleanupCardForPool(card);
            ReturnToPool(cardObject);
        }
        else
        {
            Destroy(cardObject);
        }
    }
    
    private void ReturnToPool(GameObject cardObject)
    {
        cardObject.transform.SetParent(transform, false);
        cardObject.SetActive(false);
        _cardPool.Enqueue(cardObject);
    }
    
    private void CleanupCardForPool(Card card)
    {
        if (card == null) return;
        card.ResetCardState();
        ResetCardTransform(card.gameObject);
    }
    
    private void HandleCardSelected(Card card)
    {
        if (card == null || _selectedCards.Contains(card)) return;
        
        if (!allowMultiSelect)
        {
            var cardsToDeselect = _selectedCards.ToList();
            foreach (var selectedCard in cardsToDeselect)
                selectedCard.ForceDeselect();
            _selectedCards.Clear();
        }
        else if (_selectedCards.Count >= maxSelectedCards)
        {
            Card oldestCard = _selectedCards[0];
            oldestCard.ForceDeselect();
            _selectedCards.RemoveAt(0);
        }
        
        _selectedCards.Add(card);
        OnSelectionChanged?.Invoke(new List<Card>(_selectedCards));
    }
    
    private void HandleCardDeselected(Card card)
    {
        if (_selectedCards.Remove(card))
            OnSelectionChanged?.Invoke(new List<Card>(_selectedCards));
    }
    
    public void ClearSelection()
    {
        var cardsToDeselect = _selectedCards.ToList();
        foreach (var card in cardsToDeselect)
            card.ForceDeselect();
        _selectedCards.Clear();
        OnSelectionChanged?.Invoke(new List<Card>(_selectedCards));
    }
    
    private void UpdateHandLayout()
    {
        if (_isBatchingUpdates) return; // Skip during batch operations
        _handLayoutManager?.UpdateLayout();
    }
    
    private void UpdateHandLayoutImmediate()
    {
        _handLayoutManager?.ForceImmediateLayout();
    }
    
    public bool AddCardToHand(Card card)
    {
        if (card == null || _handCards.Contains(card) || _handCards.Count >= maxHandSize)
            return false;
        
        // Ensure proper parenting for existing cards
        card.transform.SetParent(handContainer, false);
        ResetCardTransform(card.gameObject);
        
        AddCardToHandInternal(card);
        return true;
    }
    
    // ZENTRALISIERTE Methode für Letter-Extraktion (eliminiert Dopplung)
    public static string GetLetterSequenceFromCards(List<Card> cards)
    {
        if (cards == null || cards.Count == 0) return "";
    
        var letterBuilder = new StringBuilder(cards.Count * 2);
        foreach (var card in cards)
        {
            if (card?.CardData?.letterValues != null)
                letterBuilder.Append(card.CardData.letterValues);
        }
        return letterBuilder.ToString();
    }
    
    public bool RemoveCardFromHand(Card card)
    {
        if (card == null || !_handCards.Remove(card))
            return false;
        
        // Remove from selection if selected
        _selectedCards.Remove(card);
        
        // Clean up hand layout manager reference
        _handLayoutManager?.CleanupCardReference(card);
        
        UpdateHandLayout();
        OnHandUpdated?.Invoke(new List<Card>(_handCards));
        OnSelectionChanged?.Invoke(new List<Card>(_selectedCards));
        return true;
    }
    
    public List<CardData> GetAllCardData()
    {
        return new List<CardData>(allCardData.Where(card => card != null));
    }

    public int GetAvailableCardCount()
    {
        return allCardData?.Count(card => card != null) ?? 0;
    }
}