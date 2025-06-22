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
    
    // PERFORMANCE FIX: Simple dirty flag instead of complex batching
    private bool _layoutDirty = false;
    
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
    
    private void InitializeHandLayout()
    {
        if (handContainer != null)
        {
            _handLayoutManager = handContainer.GetComponent<HandLayoutManager>();
            if (_handLayoutManager == null)
                _handLayoutManager = handContainer.gameObject.AddComponent<HandLayoutManager>();
        }
    }
    
    // SIMPLIFIED: SpawnCard without complex batch processing
    public Card SpawnCard(CardData cardData, Transform parent = null, bool addToHand = false)
    {
        if (cardData == null || cardPrefab == null) return null;
        
        GameObject cardObject = GetCardObject();
        if (cardObject == null) return null;
        
        Card cardComponent = SetupCard(cardObject, cardData, parent, addToHand);
        if (cardComponent == null)
        {
            ReturnToPool(cardObject);
            return null;
        }
        
        RegisterCard(cardComponent);
        
        if (addToHand && _handCards.Count < maxHandSize)
        {
            _handCards.Add(cardComponent);
            RequestLayoutUpdate();
            OnHandUpdated?.Invoke(new List<Card>(_handCards));
        }
        
        OnCardSpawned?.Invoke(cardComponent);
        return cardComponent;
    }
    
    // SIMPLIFIED: Card setup without LayoutElement manipulation overhead
    private Card SetupCard(GameObject cardObject, CardData cardData, Transform parent, bool addToHand)
    {
        Card cardComponent = cardObject.GetComponent<Card>();
        if (cardComponent == null) return null;
        
        cardComponent.SetCardData(cardData);
        
        if (addToHand && handContainer != null)
        {
            cardObject.transform.SetParent(handContainer, false);
            ConfigureCardForHand(cardObject);
        }
        else
        {
            Transform targetParent = parent ?? defaultSpawnParent ?? transform;
            cardObject.transform.SetParent(targetParent, false);
            ResetCardTransform(cardObject);
        }
        
        cardObject.SetActive(true);
        return cardComponent;
    }
    
    private void ConfigureCardForHand(GameObject cardObject)
    {
        var rectTransform = cardObject.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.localPosition = Vector3.zero;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
            
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            
            // REMOVED: LayoutElement manipulation - HandLayoutManager handles this
        }
    }
    
    private void RegisterCard(Card card)
    {
        int cardId = _nextCardId++;
        _allCards[cardId] = card;
        _cardToId[card] = cardId;
    }
    
    // SIMPLIFIED: Merge discard and destroy logic
    public bool DiscardCard(Card card)
    {
        if (card == null) return false;
        
        RemoveCardFromCollections(card);
        OnCardDiscarded?.Invoke(card);
        DestroyCardInternal(card);
        UpdateHandAndSelection();
        
        return true;
    }
    
    public void DestroyCard(Card card)
    {
        if (card == null) return;
        
        RemoveCardFromCollections(card);
        DestroyCardInternal(card);
        UpdateHandAndSelection();
        OnCardDestroyed?.Invoke(card);
    }
    
    // SIMPLIFIED: Unified card removal logic
    private void RemoveCardFromCollections(Card card)
    {
        _handCards.Remove(card);
        _selectedCards.Remove(card);
        _handLayoutManager?.CleanupCardReference(card);
        
        if (_cardToId.TryGetValue(card, out int cardId))
        {
            _allCards.Remove(cardId);
            _cardToId.Remove(card);
        }
    }
    
    private void UpdateHandAndSelection()
    {
        RequestLayoutUpdate();
        OnHandUpdated?.Invoke(new List<Card>(_handCards));
        OnSelectionChanged?.Invoke(new List<Card>(_selectedCards));
    }
    
    private void DestroyCardInternal(Card card)
    {
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
            
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
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
    
    // PERFORMANCE FIX: Simple layout update request
    private void RequestLayoutUpdate()
    {
        if (!_layoutDirty)
        {
            _layoutDirty = true;
            StartCoroutine(ProcessLayoutUpdate());
        }
    }
    
    private IEnumerator ProcessLayoutUpdate()
    {
        yield return null; // Wait one frame
        _handLayoutManager?.UpdateLayout();
        _layoutDirty = false;
    }
    
    public bool AddCardToHand(Card card)
    {
        if (card == null || _handCards.Contains(card) || _handCards.Count >= maxHandSize)
            return false;
        
        card.transform.SetParent(handContainer, false);
        ResetCardTransform(card.gameObject);
        
        _handCards.Add(card);
        RequestLayoutUpdate();
        OnHandUpdated?.Invoke(new List<Card>(_handCards));
        return true;
    }
    
    // CENTRALIZED: Letter sequence extraction (removes duplication from SpellcastManager)
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
        
        _selectedCards.Remove(card);
        _handLayoutManager?.CleanupCardReference(card);
        
        RequestLayoutUpdate();
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