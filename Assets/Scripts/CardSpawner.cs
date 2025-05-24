using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System;

public class CardSpawner : MonoBehaviour
{
    public static CardSpawner Instance { get; private set; }

    [Header("Card Prefab")]
    [SerializeField] private GameObject cardPrefab; // Prefab with Card, DragObject components
    
    [Header("Spawn Settings")]
    [SerializeField] private Transform defaultSpawnParent; // Default parent for spawned cards
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;
    [SerializeField] private bool useRandomPosition = false;
    [SerializeField] private Vector2 randomPositionRange = new Vector2(100f, 50f);
    
    [Header("Card Pool Management")]
    [SerializeField] private bool useObjectPooling = true;
    [SerializeField] private int poolSize = 20;
    [SerializeField] private Transform poolParent; // Parent for pooled objects
    
    [Header("Animation Settings")]
    [SerializeField] private bool animateSpawn = true;
    [SerializeField] private float spawnAnimationDuration = 0.5f;
    [SerializeField] private AnimationCurve spawnCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 1.70158f),
        new Keyframe(1f, 1f, -1.70158f, 0f)
    );
    [SerializeField] private Vector3 spawnScale = new Vector3(0.1f, 0.1f, 1f);
    
    [Header("Hand Integration")]
    [SerializeField] private Transform handContainer; // Container for hand cards
    [SerializeField] private bool autoAttachToHand = true;
    [SerializeField] private float handCardSpacing = 120f;
    [SerializeField] private int maxHandSize = 7;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip spawnSound;
    [SerializeField] private AudioClip attachToHandSound;
    
    // Object Pool
    private Queue<GameObject> _cardPool = new Queue<GameObject>();
    private List<GameObject> _activeCards = new List<GameObject>();
    private List<Card> _handCards = new List<Card>();
    
    // Events
    public static event Action<Card> OnCardSpawned;
    public static event Action<Card> OnCardAddedToHand;
    public static event Action<Card> OnCardRemovedFromHand;
    public static event Action<List<Card>> OnHandUpdated;
    
    // Spawning queue for batch operations
    private Queue<SpawnRequest> _spawnQueue = new Queue<SpawnRequest>();
    private bool _isProcessingQueue = false;
    
    [System.Serializable]
    private class SpawnRequest
    {
        public CardData cardData;
        public Transform parent;
        public Vector3 position;
        public bool addToHand;
        public Action<Card> onComplete;
    }
    
    private void Awake()
    {
        // Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Validate references
        ValidateReferences();
    }
    
    private void Start()
    {
        // Initialize object pool
        if (useObjectPooling)
        {
            InitializePool();
        }
        
        // Setup audio
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        
        Debug.Log($"[CardSpawner] Initialized with pool size: {poolSize}");
    }
    
    private void ValidateReferences()
    {
        if (cardPrefab == null)
        {
            Debug.LogError("[CardSpawner] Card prefab is not assigned!");
            return;
        }
        
        // Check if prefab has required components
        if (cardPrefab.GetComponent<Card>() == null)
        {
            Debug.LogError("[CardSpawner] Card prefab missing Card component!");
        }
        
        if (cardPrefab.GetComponent<DragObject>() == null)
        {
            Debug.LogError("[CardSpawner] Card prefab missing DragObject component!");
        }
        
        // Set default spawn parent if not assigned
        if (defaultSpawnParent == null)
        {
            defaultSpawnParent = transform;
        }
        
        // Set pool parent if not assigned
        if (poolParent == null)
        {
            GameObject poolContainer = new GameObject("Card Pool");
            poolContainer.transform.SetParent(transform);
            poolParent = poolContainer.transform;
        }
        
        // Set hand container if not assigned
        if (handContainer == null)
        {
            GameObject handObject = GameObject.Find("Hand") ?? GameObject.Find("HandContainer");
            if (handObject != null)
            {
                handContainer = handObject.transform;
            }
        }
    }
    
    #region Object Pool Management
    
    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject pooledCard = CreatePooledCard();
            _cardPool.Enqueue(pooledCard);
        }
        
        Debug.Log($"[CardSpawner] Pool initialized with {_cardPool.Count} cards");
    }
    
    private GameObject CreatePooledCard()
    {
        GameObject pooledCard = Instantiate(cardPrefab, poolParent);
        pooledCard.SetActive(false);
        
        // Ensure components are properly configured
        Card cardComponent = pooledCard.GetComponent<Card>();
        DragObject dragComponent = pooledCard.GetComponent<DragObject>();
        
        if (cardComponent == null || dragComponent == null)
        {
            Debug.LogError("[CardSpawner] Pooled card missing required components!");
            return null;
        }
        
        return pooledCard;
    }
    
    private GameObject GetPooledCard()
    {
        if (_cardPool.Count > 0)
        {
            return _cardPool.Dequeue();
        }
        else
        {
            // Pool exhausted, create new card
            Debug.LogWarning("[CardSpawner] Pool exhausted, creating new card");
            return CreatePooledCard();
        }
    }
    
    private void ReturnToPool(GameObject card)
    {
        if (card == null) return;
        
        // Reset card state
        Card cardComponent = card.GetComponent<Card>();
        if (cardComponent != null)
        {
            cardComponent.DeselectCard();
            cardComponent.SetInteractable(true);
        }
        
        // Reset transform
        card.transform.SetParent(poolParent);
        card.transform.localScale = Vector3.one;
        card.transform.rotation = Quaternion.identity;
        
        // Deactivate and return to pool
        card.SetActive(false);
        _cardPool.Enqueue(card);
        
        // Remove from active cards
        _activeCards.Remove(card);
    }
    
    #endregion
    
    #region Card Spawning
    
    /// <summary>
    /// Spawn a card with specified data
    /// </summary>
    public Card SpawnCard(CardData cardData, Transform parent = null, bool addToHand = false)
    {
        if (cardData == null)
        {
            Debug.LogError("[CardSpawner] Cannot spawn card with null CardData");
            return null;
        }
        
        Vector3 spawnPosition = CalculateSpawnPosition(parent);
        return SpawnCardAtPosition(cardData, spawnPosition, parent, addToHand);
    }
    
    /// <summary>
    /// Spawn a card at specific position
    /// </summary>
    public Card SpawnCardAtPosition(CardData cardData, Vector3 position, Transform parent = null, bool addToHand = false)
    {
        GameObject cardObject = GetCardObject();
        if (cardObject == null) return null;
        
        // Setup transform
        Transform targetParent = parent ?? defaultSpawnParent;
        cardObject.transform.SetParent(targetParent);
        cardObject.transform.position = position + spawnOffset;
        cardObject.transform.rotation = Quaternion.identity;
        
        // Configure card component
        Card cardComponent = cardObject.GetComponent<Card>();
        cardComponent.SetCardData(cardData);
        
        // Configure drag component
        DragObject dragComponent = cardObject.GetComponent<DragObject>();
        dragComponent.DetectAttachedCards();
        
        // Activate the card
        cardObject.SetActive(true);
        
        // Add to active cards
        _activeCards.Add(cardObject);
        
        // Register with CardManager
        if (CardManager.Instance != null)
        {
            CardManager.Instance.RegisterCard(cardComponent);
        }
        
        // Add to hand if requested
        if (addToHand)
        {
            AddCardToHand(cardComponent);
        }
        
        // Play spawn animation
        if (animateSpawn)
        {
            PlaySpawnAnimation(cardObject);
        }
        
        // Play sound
        PlaySound(spawnSound);
        
        // Trigger events
        OnCardSpawned?.Invoke(cardComponent);
        
        Debug.Log($"[CardSpawner] Spawned card: {cardData.cardName}");
        return cardComponent;
    }
    
    /// <summary>
    /// Spawn multiple cards from data list
    /// </summary>
    public List<Card> SpawnCards(List<CardData> cardDataList, Transform parent = null, bool addToHand = false, float spawnDelay = 0.1f)
    {
        List<Card> spawnedCards = new List<Card>();
        
        for (int i = 0; i < cardDataList.Count; i++)
        {
            if (spawnDelay > 0 && i > 0)
            {
                // Queue delayed spawning
                SpawnRequest request = new SpawnRequest
                {
                    cardData = cardDataList[i],
                    parent = parent,
                    position = CalculateSpawnPosition(parent),
                    addToHand = addToHand,
                    onComplete = (card) => spawnedCards.Add(card)
                };
                
                _spawnQueue.Enqueue(request);
            }
            else
            {
                Card spawnedCard = SpawnCard(cardDataList[i], parent, addToHand);
                if (spawnedCard != null)
                {
                    spawnedCards.Add(spawnedCard);
                }
            }
        }
        
        // Start processing queue if not already processing
        if (_spawnQueue.Count > 0 && !_isProcessingQueue)
        {
            StartCoroutine(ProcessSpawnQueue(spawnDelay));
        }
        
        return spawnedCards;
    }
    
    /// <summary>
    /// Spawn random card from CardManager database
    /// </summary>
    public Card SpawnRandomCard(Transform parent = null, bool addToHand = false, CardType? filterType = null)
    {
        if (CardManager.Instance == null)
        {
            Debug.LogError("[CardSpawner] CardManager not found!");
            return null;
        }
        
        // Get available card data
        var availableCards = new List<CardData>();
        
        if (filterType.HasValue)
        {
            availableCards = CardManager.Instance.GetCardDataByType(filterType.Value);
        }
        else
        {
            // Would need access to all card data from CardManager
            Debug.LogWarning("[CardSpawner] Random spawning requires CardManager to expose all card data");
            return null;
        }
        
        if (availableCards.Count == 0)
        {
            Debug.LogWarning("[CardSpawner] No available cards to spawn");
            return null;
        }
        
        CardData randomCardData = availableCards[UnityEngine.Random.Range(0, availableCards.Count)];
        return SpawnCard(randomCardData, parent, addToHand);
    }
    
    private GameObject GetCardObject()
    {
        if (useObjectPooling)
        {
            return GetPooledCard();
        }
        else
        {
            return Instantiate(cardPrefab);
        }
    }
    
    private Vector3 CalculateSpawnPosition(Transform parent)
    {
        Vector3 basePosition = (parent ?? defaultSpawnParent).position;
        
        if (useRandomPosition)
        {
            float randomX = UnityEngine.Random.Range(-randomPositionRange.x, randomPositionRange.x);
            float randomY = UnityEngine.Random.Range(-randomPositionRange.y, randomPositionRange.y);
            basePosition += new Vector3(randomX, randomY, 0);
        }
        
        return basePosition;
    }
    
    private System.Collections.IEnumerator ProcessSpawnQueue(float delay)
    {
        _isProcessingQueue = true;
        
        while (_spawnQueue.Count > 0)
        {
            yield return new WaitForSeconds(delay);
            
            SpawnRequest request = _spawnQueue.Dequeue();
            Card spawnedCard = SpawnCardAtPosition(request.cardData, request.position, request.parent, request.addToHand);
            request.onComplete?.Invoke(spawnedCard);
        }
        
        _isProcessingQueue = false;
    }
    
    #endregion
    
    #region Hand Management
    
    /// <summary>
    /// Add card to hand with automatic positioning
    /// </summary>
    public bool AddCardToHand(Card card)
    {
        if (card == null || handContainer == null)
        {
            Debug.LogWarning("[CardSpawner] Cannot add card to hand - missing card or hand container");
            return false;
        }
        
        if (_handCards.Count >= maxHandSize)
        {
            Debug.LogWarning("[CardSpawner] Hand is full! Cannot add more cards.");
            return false;
        }
        
        if (_handCards.Contains(card))
        {
            Debug.LogWarning("[CardSpawner] Card is already in hand");
            return false;
        }
        
        // Add to hand list
        _handCards.Add(card);
        
        // Set parent to hand container
        card.transform.SetParent(handContainer);
        
        // Update hand layout
        UpdateHandLayout();
        
        // Play sound
        PlaySound(attachToHandSound);
        
        // Trigger events
        OnCardAddedToHand?.Invoke(card);
        OnHandUpdated?.Invoke(_handCards);
        
        Debug.Log($"[CardSpawner] Added {card.Data.cardName} to hand. Hand size: {_handCards.Count}");
        return true;
    }
    
    /// <summary>
    /// Remove card from hand
    /// </summary>
    public bool RemoveCardFromHand(Card card)
    {
        if (card == null || !_handCards.Contains(card))
        {
            return false;
        }
        
        _handCards.Remove(card);
        UpdateHandLayout();
        
        // Trigger events
        OnCardRemovedFromHand?.Invoke(card);
        OnHandUpdated?.Invoke(_handCards);
        
        Debug.Log($"[CardSpawner] Removed {card.Data.cardName} from hand. Hand size: {_handCards.Count}");
        return true;
    }
    
    /// <summary>
    /// Clear all cards from hand
    /// </summary>
    public void ClearHand()
    {
        foreach (var card in _handCards.ToList())
        {
            RemoveCardFromHand(card);
        }
    }
    
    /// <summary>
    /// Get copy of current hand
    /// </summary>
    public List<Card> GetHandCards()
    {
        return new List<Card>(_handCards);
    }
    
    private void UpdateHandLayout()
    {
        if (handContainer == null || _handCards.Count == 0) return;
        
        float totalWidth = (handCardSpacing * (_handCards.Count - 1));
        float startX = -totalWidth / 2f;
        
        for (int i = 0; i < _handCards.Count; i++)
        {
            if (_handCards[i] != null)
            {
                Vector3 targetPosition = new Vector3(startX + (i * handCardSpacing), 0, 0);
                _handCards[i].transform.localPosition = targetPosition;
                
                // Optional: Add slight rotation for visual appeal
                float rotation = (i - (_handCards.Count - 1) / 2f) * 2f; // Slight fan effect
                _handCards[i].transform.localRotation = Quaternion.Euler(0, 0, rotation);
            }
        }
    }
    
    #endregion
    
    #region Animation & Effects
    
    private void PlaySpawnAnimation(GameObject cardObject)
    {
        if (cardObject == null) return;
        
        // Set initial scale
        cardObject.transform.localScale = spawnScale;
        
        // Animate to normal scale
        StartCoroutine(AnimateSpawn(cardObject));
    }
    
    private System.Collections.IEnumerator AnimateSpawn(GameObject cardObject)
    {
        float elapsedTime = 0f;
        Vector3 targetScale = Vector3.one;
        
        while (elapsedTime < spawnAnimationDuration)
        {
            if (cardObject == null) yield break;
            
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / spawnAnimationDuration;
            float curveValue = spawnCurve.Evaluate(progress);
            
            cardObject.transform.localScale = Vector3.Lerp(spawnScale, targetScale, curveValue);
            
            yield return null;
        }
        
        if (cardObject != null)
        {
            cardObject.transform.localScale = targetScale;
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    
    #endregion
    
    #region Card Destruction
    
    /// <summary>
    /// Destroy or return card to pool
    /// </summary>
    public void DestroyCard(Card card)
    {
        if (card == null) return;
        
        // Remove from hand if present
        RemoveCardFromHand(card);
        
        // Unregister from CardManager
        if (CardManager.Instance != null)
        {
            CardManager.Instance.UnregisterCard(card);
        }
        
        GameObject cardObject = card.gameObject;
        
        if (useObjectPooling)
        {
            ReturnToPool(cardObject);
        }
        else
        {
            _activeCards.Remove(cardObject);
            Destroy(cardObject);
        }
        
        Debug.Log($"[CardSpawner] Destroyed card: {card.Data?.cardName ?? "Unknown"}");
    }
    
    /// <summary>
    /// Destroy all active cards
    /// </summary>
    public void DestroyAllCards()
    {
        foreach (var cardObject in _activeCards.ToList())
        {
            if (cardObject != null)
            {
                Card cardComponent = cardObject.GetComponent<Card>();
                if (cardComponent != null)
                {
                    DestroyCard(cardComponent);
                }
            }
        }
        
        _handCards.Clear();
        OnHandUpdated?.Invoke(_handCards);
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Get total number of active cards
    /// </summary>
    public int GetActiveCardCount()
    {
        return _activeCards.Count(cardObj => cardObj != null);
    }
    
    /// <summary>
    /// Get current hand size
    /// </summary>
    public int GetHandSize()
    {
        return _handCards.Count;
    }
    
    /// <summary>
    /// Check if hand is full
    /// </summary>
    public bool IsHandFull()
    {
        return _handCards.Count >= maxHandSize;
    }
    
    /// <summary>
    /// Fill hand with random cards
    /// </summary>
    public void FillHandWithRandomCards(int targetHandSize = -1)
    {
        if (targetHandSize == -1)
            targetHandSize = maxHandSize;
        
        int cardsToSpawn = Mathf.Min(targetHandSize - _handCards.Count, maxHandSize - _handCards.Count);
        
        for (int i = 0; i < cardsToSpawn; i++)
        {
            SpawnRandomCard(handContainer, true);
        }
    }
    
    [ContextMenu("Log Spawner State")]
    private void LogSpawnerState()
    {
        Debug.Log($"=== CARD SPAWNER STATE ===");
        Debug.Log($"Active Cards: {GetActiveCardCount()}");
        Debug.Log($"Hand Size: {GetHandSize()}/{maxHandSize}");
        Debug.Log($"Pool Size: {_cardPool.Count}");
        Debug.Log($"Spawn Queue: {_spawnQueue.Count}");
    }
    
    [ContextMenu("Fill Hand")]
    private void EditorFillHand()
    {
        FillHandWithRandomCards();
    }
    
    [ContextMenu("Clear Hand")]
    private void EditorClearHand()
    {
        ClearHand();
    }
    
    [ContextMenu("Destroy All Cards")]
    private void EditorDestroyAllCards()
    {
        DestroyAllCards();
    }
    
    #endregion
    
    private void OnDestroy()
    {
        // Clean up any remaining coroutines and events
        StopAllCoroutines();
    }
}