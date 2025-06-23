using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class GameManager : SingletonBehaviour<GameManager>
{
    [Header("Initialization Order")]
    [SerializeField] private List<ManagerInitConfig> managerInitOrder = new List<ManagerInitConfig>()
    {
        new ManagerInitConfig { managerType = ManagerType.Card, priority = 0 },
        new ManagerInitConfig { managerType = ManagerType.Deck, priority = 1 },
        new ManagerInitConfig { managerType = ManagerType.HandLayout, priority = 2 },
        new ManagerInitConfig { managerType = ManagerType.Spellcast, priority = 3 },
        new ManagerInitConfig { managerType = ManagerType.Combat, priority = 4 },
        new ManagerInitConfig { managerType = ManagerType.Enemy, priority = 5 },
        new ManagerInitConfig { managerType = ManagerType.Unit, priority = 6 }
    };
    
    [Header("Settings")]
    [SerializeField] private float initStepDelay = 0.05f;
    [SerializeField] private float timeoutPerManager = 2f;
    [SerializeField] private bool autoStartCombat = true;
    
    // Manager Registry
    private Dictionary<ManagerType, IGameManager> _managers = new Dictionary<ManagerType, IGameManager>();
    private bool _isInitialized = false;
    
    // Events
    public static event Action<ManagerType> OnManagerInitialized;
    public static event Action OnAllManagersReady;
    public static event Action<string> OnInitializationError;
    
    // Properties
    public bool IsInitialized => _isInitialized;
    public bool IsReady => _isInitialized; // IGameManager implementation
    public IReadOnlyDictionary<ManagerType, IGameManager> Managers => _managers;
    
    protected override void OnAwakeInitialize()
    {
        // Sort by priority
        managerInitOrder.Sort((a, b) => a.priority.CompareTo(b.priority));
    }
    
    private void Start()
    {
        StartCoroutine(InitializationSequence());
    }
    
    private IEnumerator InitializationSequence()
    {
        Debug.Log("[GameManager] Starting initialization sequence...");
        
        // Step 1: Discover all managers using ManagerExtensions
        DiscoverManagers();
        
        // Step 2: Initialize in order
        foreach (var config in managerInitOrder)
        {
            if (!config.enabled) continue;
            
            yield return StartCoroutine(InitializeManager(config.managerType));
            yield return new WaitForSeconds(initStepDelay);
        }
        
        // Step 3: Verify all critical managers using ManagerExtensions
        if (!this.AreAllCriticalManagersReady())
        {
            OnInitializationError?.Invoke("Critical managers missing!");
            yield break;
        }
        
        // Step 4: Complete initialization
        _isInitialized = true;
        OnAllManagersReady?.Invoke();
        Debug.Log("[GameManager] All managers initialized successfully!");
        
        // Log status using ManagerExtensions
        ManagerExtensions.LogManagerPerformance();
        
        // Step 5: Auto-start combat if enabled
        if (autoStartCombat)
        {
            yield return new WaitForSeconds(0.1f);
            this.TryStartCombat();
        }
    }
    
    private void DiscoverManagers()
    {
        // Use ManagerExtensions for safer discovery
        RegisterManagerSafely(ManagerType.Card, ManagerExtensions.TryGetManager<CardManager>());
        RegisterManagerSafely(ManagerType.Deck, ManagerExtensions.TryGetManager<DeckManager>());
        RegisterManagerSafely(ManagerType.HandLayout, ManagerExtensions.TryGetManager<HandLayoutManager>());
        RegisterManagerSafely(ManagerType.Spellcast, ManagerExtensions.TryGetManager<SpellcastManager>());
        RegisterManagerSafely(ManagerType.Combat, ManagerExtensions.TryGetManager<CombatManager>());
        RegisterManagerSafely(ManagerType.Enemy, ManagerExtensions.TryGetManager<EnemyManager>());
        RegisterManagerSafely(ManagerType.Unit, ManagerExtensions.TryGetManager<UnitManager>());
            
        Debug.Log($"[GameManager] Discovered {_managers.Count} managers");
    }
    
    private void RegisterManagerSafely(ManagerType type, MonoBehaviour manager)
    {
        if (manager != null && manager is IGameManager gameManager)
        {
            _managers[type] = gameManager;
            Debug.Log($"[GameManager] Registered {type} manager");
        }
    }
    
    private void RegisterManager(ManagerType type, MonoBehaviour manager)
    {
        if (manager != null && manager is IGameManager gameManager)
        {
            _managers[type] = gameManager;
            Debug.Log($"[GameManager] Registered {type} manager");
        }
    }
    
    private IEnumerator InitializeManager(ManagerType type)
    {
        Debug.Log($"[GameManager] Starting initialization of {type} manager");
        
        // Try to find manager if not registered yet
        if (!_managers.ContainsKey(type))
        {
            yield return null; // Wait one frame
            DiscoverManagers(); // Try again
        }
        
        if (!_managers.TryGetValue(type, out var manager))
        {
            Debug.LogWarning($"[GameManager] {type} manager not found after discovery!");
            yield break;
        }
        
        Debug.Log($"[GameManager] Waiting for {type} manager... (IsReady: {manager.IsReady})");
        
        float elapsed = 0f;
        
        while (!manager.IsReady && elapsed < timeoutPerManager)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        if (manager.IsReady)
        {
            Debug.Log($"[GameManager] {type} manager ready after {elapsed:F2}s");
            OnManagerInitialized?.Invoke(type);
        }
        else
        {
            Debug.LogError($"[GameManager] {type} manager initialization timeout after {elapsed:F2}s!");
            OnInitializationError?.Invoke($"{type} manager failed to initialize");
        }
    }
    
    public void StartCombat()
    {
        // Use ManagerExtensions for safer combat start
        this.TryStartCombat();
    }
    
    public T GetManager<T>(ManagerType type) where T : class, IGameManager
    {
        return _managers.TryGetValue(type, out var manager) ? manager as T : null;
    }
    
    // Quick access properties using ManagerExtensions pattern
    public CardManager CardManager => GetManager<CardManager>(ManagerType.Card);
    public DeckManager DeckManager => GetManager<DeckManager>(ManagerType.Deck);
    public CombatManager CombatManager => GetManager<CombatManager>(ManagerType.Combat);
    public SpellcastManager SpellcastManager => GetManager<SpellcastManager>(ManagerType.Spellcast);
    public EnemyManager EnemyManager => GetManager<EnemyManager>(ManagerType.Enemy);
    public UnitManager UnitManager => GetManager<UnitManager>(ManagerType.Unit);
    public HandLayoutManager HandLayoutManager => GetManager<HandLayoutManager>(ManagerType.HandLayout);
    
#if UNITY_EDITOR
    [ContextMenu("Force Reinitialize")]
    public void ForceReinitialize()
    {
        StopAllCoroutines();
        _isInitialized = false;
        _managers.Clear();
        StartCoroutine(InitializationSequence());
    }
    
    [ContextMenu("Log Manager Status")]
    public void LogManagerStatus()
    {
        // Use ManagerExtensions for comprehensive status
        ManagerExtensions.LogManagerPerformance();
        
        Debug.Log($"[GameManager] Detailed Status - Initialized: {_isInitialized}");
        foreach (var kvp in _managers)
        {
            Debug.Log($"  {kvp.Key}: {(kvp.Value?.IsReady ?? false ? "Ready" : "Not Ready")}");
        }
    }
#endif
}

[System.Serializable]
public class ManagerInitConfig
{
    public ManagerType managerType;
    public int priority = 0;
    public bool enabled = true;
}

public enum ManagerType
{
    Card,
    Deck,
    HandLayout,
    Spellcast,
    Combat,
    Enemy,
    Unit
}

// Interface für alle Manager
public interface IGameManager
{
    bool IsReady { get; }
}