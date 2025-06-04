using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;

[System.Serializable]
public class Resource
{
    [SerializeField] private int _startValue;
    [SerializeField] private int _currentValue;
    [SerializeField] private int _maxValue;
    
    public int StartValue => _startValue;
    public int CurrentValue => _currentValue;
    public int MaxValue => _maxValue;
    public float Percentage => _maxValue > 0 ? (float)_currentValue / _maxValue : 0f;
    
    public Resource(int startValue, int maxValue = -1)
    {
        _startValue = startValue;
        _currentValue = startValue;
        _maxValue = maxValue > 0 ? maxValue : startValue;
    }
    
    public void SetCurrent(int value) => _currentValue = Mathf.Clamp(value, 0, _maxValue);
    public void ModifyBy(int delta) => SetCurrent(_currentValue + delta);
    public void Reset() => _currentValue = _startValue;
    
    public void SetMax(int newMax)
    {
        _maxValue = newMax;
        _currentValue = Mathf.Min(_currentValue, _maxValue);
    }
}

public enum EntityType { Enemy, SummonedUnit }

[System.Serializable]
public class CombatEntity
{
    public int id;
    public EntityType type;
    public string name;
    public GameObject gameObject;
    public Resource health;
    public bool isActive;
    
    public CombatEntity(int id, EntityType type, string name, GameObject gameObject, int health)
    {
        this.id = id;
        this.type = type;
        this.name = name;
        this.gameObject = gameObject;
        this.health = new Resource(health);
        this.isActive = true;
    }
}

public class CombatManager : SingletonBehaviour<CombatManager>
{
    [Header("Player Resources")]
    [SerializeField] private int startLife = 100;
    [SerializeField] private int startCreativity = 3;
    [SerializeField] private int maxCreativity = 10;
    
    [Header("Combat Start Settings")]
    [SerializeField] private int startingHandSize = 5;
    [SerializeField] private bool autoDrawOnCombatStart = true;
    [SerializeField] private bool validateDeckOnCombatStart = true;
    [SerializeField] private float managerWaitTimeout = 3f;
    
    // Resources
    private Resource _life;
    private Resource _creativity;
    
    // Entities
    private Dictionary<int, CombatEntity> _entities = new Dictionary<int, CombatEntity>();
    private List<CombatEntity> _enemies = new List<CombatEntity>();
    private List<CombatEntity> _summonedUnits = new List<CombatEntity>();
    private int _nextEntityId = 0;
    
    // Manager state tracking
    private bool _managersReady = false;
    private bool _cardManagerReady = false;
    private bool _deckManagerReady = false;
    
    // Events - Resources
    public static event Action<Resource> OnLifeChanged;
    public static event Action<int> OnDeckSizeChanged;
    public static event Action<Resource> OnCreativityChanged;
    
    // Events - Entities
    public static event Action<CombatEntity> OnEntityAdded;
    public static event Action<CombatEntity> OnEntityRemoved;
    public static event Action<CombatEntity> OnEntityHealthChanged;
    public static event Action<CombatEntity> OnEntityDestroyed;
    
    // Events - Combat
    public static event Action OnCombatStarted;
    public static event Action OnCombatEnded;
    public static event Action OnPlayerDeath;
    public static event Action OnCombatValidated;
    public static event Action OnHandDrawn;
    public static event Action OnDeckEmpty;
    
    // Properties
    public Resource Life => _life;
    public int DeckSize => DeckManager.HasInstance ? DeckManager.Instance.DeckSize : 0;
    public int DiscardSize => DeckManager.HasInstance ? DeckManager.Instance.DiscardSize : 0;
    public Resource Creativity => _creativity;
    public IReadOnlyList<CombatEntity> Enemies => _enemies.AsReadOnly();
    public IReadOnlyList<CombatEntity> SummonedUnits => _summonedUnits.AsReadOnly();
    public IReadOnlyCollection<CombatEntity> AllEntities => _entities.Values;
    public int EntityCount => _entities.Count;
    public bool IsInCombat { get; private set; }
    public bool ManagersReady => _managersReady;
    
    protected override void OnAwakeInitialize()
    {
        InitializeResources();
        StartCoroutine(WaitForManagers());
    }
    
    private void OnEnable()
    {
        if (DeckManager.HasInstance)
        {
            DeckManager.OnDeckSizeChanged += OnDeckManagerSizeChanged;
            DeckManager.OnDeckInitialized += OnDeckManagerReady;
            DeckManager.OnDeckEmpty += HandleDeckEmpty;
        }
        
        if (CardManager.HasInstance)
        {
            CardManager.OnCardManagerInitialized += OnCardManagerReady;
        }
    }
    
    private void OnDisable()
    {
        if (DeckManager.HasInstance)
        {
            DeckManager.OnDeckSizeChanged -= OnDeckManagerSizeChanged;
            DeckManager.OnDeckInitialized -= OnDeckManagerReady;
            DeckManager.OnDeckEmpty -= HandleDeckEmpty;
        }
        
        if (CardManager.HasInstance)
        {
            CardManager.OnCardManagerInitialized -= OnCardManagerReady;
        }
    }
    
    private void InitializeResources()
    {
        _life = new Resource(startLife);
        _creativity = new Resource(startCreativity, maxCreativity);
    }
    
    private IEnumerator WaitForManagers()
    {
        float elapsed = 0f;
        
        while (elapsed < managerWaitTimeout)
        {
            CheckManagersReady();
            
            if (_managersReady)
            {
                Debug.Log("[CombatManager] All managers ready");
                yield break;
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Fallback: Force ready state after timeout
        Debug.LogWarning($"[CombatManager] Manager timeout after {managerWaitTimeout}s - forcing ready state");
        _managersReady = true;
        
        // Try to initialize deck if it failed
        if (DeckManager.HasInstance && !DeckManager.Instance.IsInitialized)
        {
            DeckManager.Instance.ForceInitialization();
        }
    }
    
    private void OnCardManagerReady()
    {
        _cardManagerReady = true;
        CheckManagersReady();
    }
    
    private void OnDeckManagerReady()
    {
        _deckManagerReady = true;
        CheckManagersReady();
    }
    
    private void CheckManagersReady()
    {
        if (_managersReady) return;
        
        // Check CardManager
        if (!_cardManagerReady && CardManager.HasInstance && CardManager.Instance.IsInitialized)
        {
            _cardManagerReady = true;
            CardManager.OnCardManagerInitialized += OnCardManagerReady;
        }
        
        // Check DeckManager
        if (!_deckManagerReady && DeckManager.HasInstance && DeckManager.Instance.IsInitialized)
        {
            _deckManagerReady = true;
            DeckManager.OnDeckInitialized += OnDeckManagerReady;
            DeckManager.OnDeckSizeChanged += OnDeckManagerSizeChanged;
            DeckManager.OnDeckEmpty += HandleDeckEmpty;
        }
        
        // Set ready if both managers are available
        if (_cardManagerReady && _deckManagerReady)
        {
            _managersReady = true;
            Debug.Log("[CombatManager] Managers ready");
        }
    }
    
    // Combat State Management
    public void StartCombat()
    {
        if (!IsInCombat) StartCoroutine(StartCombatSequence());
    }
    
    private IEnumerator StartCombatSequence()
    {
        // Wait for managers with timeout
        float timeout = managerWaitTimeout;
        while (!_managersReady && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        
        IsInCombat = true;
        
        if (validateDeckOnCombatStart)
            yield return ValidateCombatState();
        
        if (autoDrawOnCombatStart && startingHandSize > 0)
            yield return DrawStartingHand();
        
        OnCombatStarted?.Invoke();
    }
    
    private IEnumerator ValidateCombatState()
    {
        if (!DeckManager.HasInstance)
        {
            Debug.LogError("[CombatManager] DeckManager not available for validation");
            yield break;
        }
        
        if (DeckManager.Instance.DeckSize == 0)
        {
            Debug.Log("[CombatManager] Empty deck detected - generating test deck");
            DeckManager.Instance.GenerateTestDeck();
            
            float timeout = 2f;
            while (DeckManager.Instance.DeckSize == 0 && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
        }
        
        OnCombatValidated?.Invoke();
    }
    
    private IEnumerator DrawStartingHand()
    {
        if (!DeckManager.HasInstance || !CardManager.HasInstance) 
        {
            Debug.LogWarning("[CombatManager] Managers not available for drawing starting hand");
            yield break;
        }
        
        var drawnCards = DeckManager.Instance.DrawCards(startingHandSize);
        
        foreach (var cardData in drawnCards)
        {
            if (cardData != null)
            {
                CardManager.Instance.SpawnCard(cardData, null, true);
                yield return null;
            }
        }
        
        OnHandDrawn?.Invoke();
    }
    
    public void EndCombat()
    {
        if (IsInCombat)
        {
            IsInCombat = false;
            OnCombatEnded?.Invoke();
        }
    }
    
    public void ResetCombat()
    {
        var entitiesToRemove = new List<CombatEntity>(_entities.Values);
        foreach (var entity in entitiesToRemove)
            RemoveEntity(entity);
        
        _life.Reset();
        _creativity.Reset();
        IsInCombat = false;
        
        OnLifeChanged?.Invoke(_life);
        OnCreativityChanged?.Invoke(_creativity);
        OnDeckSizeChanged?.Invoke(DeckSize);
    }
    
    // Resource Management
    public void ModifyLife(int delta)
    {
        int oldValue = _life.CurrentValue;
        _life.ModifyBy(delta);
        
        if (_life.CurrentValue != oldValue)
        {
            OnLifeChanged?.Invoke(_life);
            if (_life.CurrentValue <= 0)
                OnPlayerDeath?.Invoke();
        }
    }
    
    public void ModifyCreativity(int delta)
    {
        int oldValue = _creativity.CurrentValue;
        _creativity.ModifyBy(delta);
        
        if (_creativity.CurrentValue != oldValue)
            OnCreativityChanged?.Invoke(_creativity);
    }
    
    public bool CanSpendCreativity(int amount) => _creativity.CurrentValue >= amount;
    
    public bool SpendCreativity(int amount)
    {
        if (!CanSpendCreativity(amount)) return false;
        ModifyCreativity(-amount);
        return true;
    }
    
    private void OnDeckManagerSizeChanged(int deckSize) => OnDeckSizeChanged?.Invoke(deckSize);
    private void HandleDeckEmpty() => OnDeckEmpty?.Invoke();
    
    // Entity Management
    public CombatEntity AddEntity(EntityType type, string name, GameObject gameObject, int health)
    {
        if (gameObject == null) return null;
        
        var entity = new CombatEntity(_nextEntityId++, type, name, gameObject, health);
        _entities[entity.id] = entity;
        
        (type == EntityType.Enemy ? _enemies : _summonedUnits).Add(entity);
        
        OnEntityAdded?.Invoke(entity);
        return entity;
    }
    
    public bool RemoveEntity(int entityId) =>
        _entities.TryGetValue(entityId, out CombatEntity entity) && RemoveEntity(entity);
    
    public bool RemoveEntity(CombatEntity entity)
    {
        if (entity == null || !_entities.Remove(entity.id)) return false;
        
        var targetList = entity.type == EntityType.Enemy ? _enemies : _summonedUnits;
        targetList.RemoveAll(e => e.id == entity.id);
        
        entity.isActive = false;
        OnEntityRemoved?.Invoke(entity);
        return true;
    }
    
    public void ModifyEntityHealth(int entityId, int delta)
    {
        if (_entities.TryGetValue(entityId, out CombatEntity entity))
            ModifyEntityHealth(entity, delta);
    }
    
    public void ModifyEntityHealth(CombatEntity entity, int delta)
    {
        if (entity == null || !entity.isActive) return;
        
        int oldHealth = entity.health.CurrentValue;
        entity.health.ModifyBy(delta);
        
        if (entity.health.CurrentValue != oldHealth)
        {
            OnEntityHealthChanged?.Invoke(entity);
            
            if (entity.health.CurrentValue <= 0)
            {
                OnEntityDestroyed?.Invoke(entity);
                RemoveEntity(entity);
            }
        }
    }
    
    public CombatEntity GetEntity(int entityId) =>
        _entities.TryGetValue(entityId, out CombatEntity entity) ? entity : null;
    
    // Utility
    public void ClearAllEnemies()
    {
        var enemiesToRemove = new List<CombatEntity>(_enemies);
        foreach (var enemy in enemiesToRemove)
            RemoveEntity(enemy);
    }
    
    public void ClearAllSummonedUnits()
    {
        var unitsToRemove = new List<CombatEntity>(_summonedUnits);
        foreach (var unit in unitsToRemove)
            RemoveEntity(unit);
    }
}