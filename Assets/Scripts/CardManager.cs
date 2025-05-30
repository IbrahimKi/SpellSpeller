using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }
    
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
    
    // OPTIMIZED: Simplified data structures
    private Dictionary<int, Card> _allCards = new Dictionary<int, Card>();
    private Dictionary<Card, int> _cardToId = new Dictionary<Card, int>();
    private List<Card> _handCards = new List<Card>();
    private List<Card> _selectedCards = new List<Card>();
    private Queue<GameObject> _cardPool = new Queue<GameObject>();
    private int _nextCardId = 0;
    
    // OPTIMIZED: Reference to HandLayoutManager (removes duplication)
    private HandLayoutManager _handLayoutManager;
    
    // Events
    public static event System.Action<Card> OnCardSpawned;
    public static event System.Action<Card> OnCardDestroyed;
    public static event System.Action<List<Card>> OnHandUpdated;
    public static event System.Action<List<Card>> OnSelectionChanged;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePool();
            InitializeHandLayout();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void OnEnable()
    {
        Card.OnCardSelected += HandleCardSelected;
        Card.OnCardDeselected += HandleCardDeselected;
    }
    
    private void OnDisable()
    {
        Card.OnCardSelected -= HandleCardSelected;
        Card.OnCardDeselected -= HandleCardDeselected;
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
    
    // OPTIMIZED: Initialize HandLayoutManager reference
    private void InitializeHandLayout()
    {
        if (handContainer != null)
        {
            _handLayoutManager = handContainer.GetComponent<HandLayoutManager>();
            if (_handLayoutManager == null)
            {
                _handLayoutManager = handContainer.gameObject.AddComponent<HandLayoutManager>();
            }
        }
    }
    
    public Card SpawnCard(CardData cardData, Transform parent = null, bool addToHand = false)
    {
        if (cardData == null || cardPrefab == null) return null;
        
        GameObject cardObject = GetCardObject();
        if (cardObject == null) return null;
        
        // FIXED: Proper transform setup without conflicts
        Transform targetParent = parent ?? defaultSpawnParent ?? transform;
        cardObject.transform.SetParent(targetParent, false); // worldPositionStays = false
        
        // FIXED: Reset transform properly
        var rectTransform = cardObject.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.localPosition = Vector3.zero;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one; // FIXED: Always start with proper scale
        }
        else
        {
            cardObject.transform.localPosition = Vector3.zero;
            cardObject.transform.localRotation = Quaternion.identity;
            cardObject.transform.localScale = Vector3.one;
        }
        
        // Setup card component
        Card cardComponent = cardObject.GetComponent<Card>();
        if (cardComponent == null)
        {
            Debug.LogError("[CardManager] Card prefab missing Card component!");
            ReturnToPool(cardObject);
            return null;
        }
        
        cardComponent.SetCardData(cardData);
        cardObject.SetActive(true);
        
        // Register card
        int cardId = _nextCardId++;
        _allCards[cardId] = cardComponent;
        _cardToId[cardComponent] = cardId;
        
        if (addToHand && _handCards.Count < maxHandSize)
        {
            AddCardToHandInternal(cardComponent);
        }
        
        OnCardSpawned?.Invoke(cardComponent);
        return cardComponent;
    }
    
    private GameObject GetCardObject()
    {
        if (useObjectPooling && _cardPool.Count > 0)
        {
            return _cardPool.Dequeue();
        }
        return cardPrefab != null ? Instantiate(cardPrefab) : null;
    }
    
    // OPTIMIZED: Separate internal method to avoid duplicate layout updates
    private void AddCardToHandInternal(Card card)
    {
        _handCards.Add(card);
        card.transform.SetParent(handContainer, false); // FIXED: worldPositionStays = false
        
        // FIXED: Don't manually set position here - let HandLayoutManager handle it
        UpdateHandLayout();
        OnHandUpdated?.Invoke(new List<Card>(_handCards));
    }
    
    public void DestroyCard(Card card)
    {
        if (card == null) return;
        
        // Remove from tracking
        if (_cardToId.TryGetValue(card, out int cardId))
        {
            _allCards.Remove(cardId);
            _cardToId.Remove(card);
        }
        
        _handCards.Remove(card);
        _selectedCards.Remove(card);
        
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
        
        UpdateHandLayout();
        OnHandUpdated?.Invoke(new List<Card>(_handCards));
        OnSelectionChanged?.Invoke(new List<Card>(_selectedCards));
        OnCardDestroyed?.Invoke(card);
    }
    
    // OPTIMIZED: Centralized pool return method
    private void ReturnToPool(GameObject cardObject)
    {
        cardObject.transform.SetParent(transform, false);
        cardObject.SetActive(false);
        _cardPool.Enqueue(cardObject);
    }
    
    // OPTIMIZED: Proper cleanup method
    private void CleanupCardForPool(Card card)
    {
        if (card == null) return;
        
        card.ClearEventSubscriptions();
        card.ResetCardState();
        
        // FIXED: Reset transform properly
        var rectTransform = card.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.localPosition = Vector3.zero;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
        }
        else
        {
            card.transform.localPosition = Vector3.zero;
            card.transform.localRotation = Quaternion.identity;
            card.transform.localScale = Vector3.one;
        }
    }
    
    private int GetCardId(Card card)
    {
        return _cardToId.TryGetValue(card, out int id) ? id : -1;
    }
    
    private void HandleCardSelected(Card card)
    {
        if (card == null || _selectedCards.Contains(card)) return;
        
        if (!allowMultiSelect)
        {
            var cardsToDeselect = _selectedCards.ToList();
            foreach (var selectedCard in cardsToDeselect)
            {
                selectedCard.ForceDeselect();
            }
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
        {
            card.ForceDeselect();
        }
        _selectedCards.Clear();
        OnSelectionChanged?.Invoke(new List<Card>(_selectedCards));
    }
    
    // OPTIMIZED: Delegate to HandLayoutManager instead of duplicate logic
    private void UpdateHandLayout()
    {
        if (_handLayoutManager != null)
        {
            _handLayoutManager.UpdateLayout();
        }
    }
    
    public void CleanupAllCards()
    {
        var allCardsCopy = _allCards.Values.ToList();
        foreach (var card in allCardsCopy)
        {
            if (card != null)
                DestroyCard(card);
        }
        
        _allCards.Clear();
        _cardToId.Clear();
        _handCards.Clear();
        _selectedCards.Clear();
    }
    
    public bool AddCardToHand(Card card)
    {
        if (card == null || _handCards.Contains(card) || _handCards.Count >= maxHandSize)
            return false;
        
        AddCardToHandInternal(card);
        return true;
    }
    
    public bool RemoveCardFromHand(Card card)
    {
        if (card == null || !_handCards.Remove(card))
            return false;
        
        UpdateHandLayout();
        OnHandUpdated?.Invoke(new List<Card>(_handCards));
        return true;
    }
    
    // Public accessors
    public List<Card> GetSelectedCards() => _selectedCards != null ? new List<Card>(_selectedCards) : new List<Card>();
    public List<Card> GetHandCards() => _handCards != null ? new List<Card>(_handCards) : new List<Card>();
    public List<Card> GetAllCards() => _allCards != null ? new List<Card>(_allCards.Values) : new List<Card>();
    public int ActiveCardCount => _allCards?.Count ?? 0;
    public int HandSize => _handCards?.Count ?? 0;
    public int SelectedCount => _selectedCards?.Count ?? 0;
    public bool IsHandFull => _handCards != null && _handCards.Count >= maxHandSize;
    
    public bool IsValidCard(Card card)
    {
        return card != null && _cardToId.ContainsKey(card);
    }
    
    [ContextMenu("Spawn Test Card")]
    public void SpawnTestCard()
    {
        if (allCardData.Count > 0)
            SpawnCard(allCardData[0], null, true);
    }

    [ContextMenu("Clear All Cards")]
    public void ClearAllCards()
    {
        CleanupAllCards();
    }
}