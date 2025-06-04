// ========== CardManager.cs ==========
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class CardManager : SingletonBehaviour<CardManager>
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
    
    // Data structures
    private Dictionary<int, Card> _allCards = new Dictionary<int, Card>();
    private Dictionary<Card, int> _cardToId = new Dictionary<Card, int>();
    private List<Card> _handCards = new List<Card>();
    private List<Card> _selectedCards = new List<Card>();
    private Queue<GameObject> _cardPool = new Queue<GameObject>();
    private int _nextCardId = 0;
    
    // Reference to HandLayoutManager
    private HandLayoutManager _handLayoutManager;
    
    // Events
    public static event System.Action<Card> OnCardSpawned;
    public static event System.Action<Card> OnCardDestroyed;
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
    
    public Card SpawnCard(CardData cardData, Transform parent = null, bool addToHand = false)
    {
        if (cardData == null || cardPrefab == null) return null;
        
        GameObject cardObject = GetCardObject();
        if (cardObject == null) return null;
        
        // Setup transform
        Transform targetParent = parent ?? defaultSpawnParent ?? transform;
        cardObject.transform.SetParent(targetParent, false);
        ResetCardTransform(cardObject);
        
        // Setup card component
        Card cardComponent = cardObject.GetComponent<Card>();
        if (cardComponent == null)
        {
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
            AddCardToHandInternal(cardComponent);
        
        OnCardSpawned?.Invoke(cardComponent);
        return cardComponent;
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
            rectTransform.localScale = Vector3.one;
        }
    }
    
    private void AddCardToHandInternal(Card card)
    {
        _handCards.Add(card);
        card.transform.SetParent(handContainer, false);
        
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
        _handLayoutManager?.UpdateLayout();
    }
    
    public bool AddCardToHand(Card card)
    {
        if (card == null || _handCards.Contains(card) || _handCards.Count >= maxHandSize)
            return false;
        
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
        
        UpdateHandLayout();
        OnHandUpdated?.Invoke(new List<Card>(_handCards));
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