using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using GameCore.Enums; // CRITICAL: SharedEnums import
using GameCore.Data; // CRITICAL: SharedData import

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
    
    private bool _layoutDirty = false;
    private HandLayoutManager _handLayoutManager;
    
    // Events
    public static event System.Action<Card> OnCardSpawned;
    public static event System.Action<Card> OnCardDestroyed;
    public static event System.Action<Card> OnCardDiscarded;
    public static event System.Action<List<Card>> OnHandUpdated;
    public static event System.Action<List<Card>> OnSelectionChanged;
    public static event System.Action OnCardManagerInitialized;
    
    // Properties - Simplified using CardExtensions
    public bool IsInitialized { get; private set; }
    public List<Card> SelectedCards => _selectedCards.GetValidCards().ToList();
    public List<Card> GetHandCards() => _handCards.GetValidCards().ToList();
    public bool IsHandFull => _handCards.GetValidCardCount() >= maxHandSize;
    public int HandSize => _handCards.GetValidCardCount();
    public bool HasValidSelection => _selectedCards.HasValidCards();
    public bool HasPlayableCards => _handCards.HasPlayableCards();
    
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
        
        if (addToHand && _handCards.GetValidCardCount() < maxHandSize)
        {
            _handCards.Add(cardComponent);
            RequestLayoutUpdate();
            OnHandUpdated?.Invoke(GetHandCards());
        }
        
        OnCardSpawned?.Invoke(cardComponent);
        return cardComponent;
    }
    
    private Card SetupCard(GameObject cardObject, CardData cardData, Transform parent, bool addToHand)
    {
        Card cardComponent = cardObject.GetComponent<Card>();
        if (cardComponent == null) return null;
        
        cardComponent.SetCardData(cardData);
        
        Transform targetParent = addToHand && handContainer != null ? handContainer : (parent ?? defaultSpawnParent ?? transform);
        cardObject.transform.SetParent(targetParent, false);
        ResetCardTransform(cardObject);
        
        cardObject.SetActive(true);
        return cardComponent;
    }
    
    private void RegisterCard(Card card)
    {
        int cardId = _nextCardId++;
        _allCards[cardId] = card;
        _cardToId[card] = cardId;
    }
    
    public bool DiscardCard(Card card)
    {
        if (!card.IsValid()) return false;
        
        RemoveCardFromCollections(card);
        OnCardDiscarded?.Invoke(card);
        DestroyCardInternal(card);
        UpdateHandAndSelection();
        
        return true;
    }
    
    public void DestroyCard(Card card)
    {
        if (!card.IsValid()) return;
        
        RemoveCardFromCollections(card);
        DestroyCardInternal(card);
        UpdateHandAndSelection();
        OnCardDestroyed?.Invoke(card);
    }
    
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
        OnHandUpdated?.Invoke(GetHandCards());
        OnSelectionChanged?.Invoke(SelectedCards);
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
        if (!card.IsValid() || _selectedCards.Contains(card)) return;
        
        if (!allowMultiSelect)
        {
            foreach (var selectedCard in _selectedCards.GetValidCards().ToList())
                selectedCard.TryDeselect();
            _selectedCards.Clear();
        }
        else if (_selectedCards.GetValidCardCount() >= maxSelectedCards)
        {
            var oldestCard = _selectedCards.GetValidCards().FirstOrDefault();
            oldestCard?.TryDeselect();
            _selectedCards.Remove(oldestCard);
        }
        
        _selectedCards.Add(card);
        OnSelectionChanged?.Invoke(SelectedCards);
    }
    
    private void HandleCardDeselected(Card card)
    {
        if (_selectedCards.Remove(card))
            OnSelectionChanged?.Invoke(SelectedCards);
    }
    
    public void ClearSelection()
    {
        foreach (var card in _selectedCards.GetValidCards().ToList())
            card.TryDeselect();
        _selectedCards.Clear();
        OnSelectionChanged?.Invoke(new List<Card>());
    }
    
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
        yield return null;
        _handLayoutManager?.UpdateLayout();
        _layoutDirty = false;
    }
    
    public bool AddCardToHand(Card card)
    {
        if (!card.IsValid() || _handCards.Contains(card) || IsHandFull)
            return false;
        
        card.transform.SetParent(handContainer, false);
        ResetCardTransform(card.gameObject);
        
        _handCards.Add(card);
        RequestLayoutUpdate();
        OnHandUpdated?.Invoke(GetHandCards());
        return true;
    }
    
    public bool RemoveCardFromHand(Card card)
    {
        if (!card.IsValid() || !_handCards.Remove(card))
            return false;
        
        _selectedCards.Remove(card);
        _handLayoutManager?.CleanupCardReference(card);
        
        RequestLayoutUpdate();
        OnHandUpdated?.Invoke(GetHandCards());
        OnSelectionChanged?.Invoke(SelectedCards);
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
    
    // INTEGRATION: Extension method integrations
    public static string GetLetterSequenceFromCards(List<Card> cards)
        => cards.GetLetterSequence();
    
    public SpellBuildingPotential GetHandSpellPotential()
        => _handCards.GetSpellBuildingPotential();
    
    public List<Card> FindCardsForSpell(string spellCode)
        => _handCards.FindCardsForSpell(spellCode).ToList();
    
    public bool CanBuildSpell(string spellCode)
        => _handCards.CanBuildSpell(spellCode);
    
    public CollectionLetterAnalysis GetHandAnalysis()
        => _handCards.GetCollectionLetterAnalysis();
    
    public bool SelectCardsByCriteria(CardSortCriteria criteria, int maxCards = 1)
    {
        if (IsHandFull || maxCards <= 0) return false;
        
        var candidates = _handCards
            .GetValidCards()
            .Where(c => !c.IsSelected)
            .SortBy(criteria)
            .Take(maxCards);
        
        bool anySelected = false;
        foreach (var card in candidates)
        {
            if (card.TrySelect())
                anySelected = true;
        }
        
        return anySelected;
    }
    
    public List<Card> GetCardsByType(CardType cardType)
        => _handCards.FilterByType(cardType).ToList();
    
    public List<Card> GetCardsByTier(int tier)
        => _handCards.FilterByTier(tier).ToList();
    
    public void AutoSelectForSpellBuilding()
    {
        var potential = GetHandSpellPotential();
        if (potential.OverallScore < 0.3f) return;
        
        SelectCardsByCriteria(CardSortCriteria.LetterCount, 2);
    }
    
    public bool TrySelectCards(IEnumerable<Card> cards)
    {
        if (cards == null) return false;
        
        try
        {
            ClearSelection();
            foreach (var card in cards.Where(c => c.IsValid()))
                card.TrySelect();
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CardManager] Card selection failed: {ex.Message}");
            return false;
        }
    }
    
    // FIX: Fehlende Methode für GameUIHandler Integration
    public bool CanDrawCard()
    {
        if (!IsReady) return false;
        
        return !IsHandFull && 
               CoreExtensions.TryWithManager<DeckManager, bool>(this, dm => !dm.IsDeckEmpty) &&
               CoreExtensions.TryWithManager<CombatManager, bool>(this, cm => cm.IsPlayerTurn);
    }

#if UNITY_EDITOR
    [ContextMenu("Analyze Hand")]
    public void DebugAnalyzeHand()
    {
        var analysis = GetHandAnalysis();
        Debug.Log($"[CardManager] Hand Analysis:");
        Debug.Log($"  Total Cards: {analysis.TotalCards}");
        Debug.Log($"  Total Letters: {analysis.TotalLetters}");
        Debug.Log($"  Unique Letters: {analysis.UniqueLetters}");
        Debug.Log($"  Vowels: {analysis.Vowels}, Consonants: {analysis.Consonants}");
        
        var potential = GetHandSpellPotential();
        Debug.Log($"  Spell Potential: {potential.OverallScore:P0}");
    }
#endif
}