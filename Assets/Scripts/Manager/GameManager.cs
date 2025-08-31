using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameManager : SingletonBehaviour<GameManager>
{
    [Header("Initialization Order")]
    [SerializeField] private List<ManagerInitConfig> managerInitOrder = new List<ManagerInitConfig>()
    {
        new ManagerInitConfig { managerType = ManagerType.Card, priority = 0 },
        new ManagerInitConfig { managerType = ManagerType.Deck, priority = 1 },
        new ManagerInitConfig { managerType = ManagerType.HandLayout, priority = 2 },
        new ManagerInitConfig { managerType = ManagerType.CardSlot, priority = 3 },
        new ManagerInitConfig { managerType = ManagerType.Spellcast, priority = 4 },
        new ManagerInitConfig { managerType = ManagerType.Combat, priority = 5 },
        new ManagerInitConfig { managerType = ManagerType.Enemy, priority = 6 },
        new ManagerInitConfig { managerType = ManagerType.Unit, priority = 7 }
    };
    
    [Header("Settings")]
    [SerializeField] private float initStepDelay = 0.05f;
    [SerializeField] private float timeoutPerManager = 2f;
    [SerializeField] private bool autoStartCombat = true;
    
    // Manager Registry
    private Dictionary<ManagerType, IGameManager> _managers = new Dictionary<ManagerType, IGameManager>();
    private bool _isInitialized = false;
    
    // Events - FIXED: Korrekte System.Action Syntax
    public static event System.Action<ManagerType> OnManagerInitialized;
    public static event System.Action OnAllManagersReady;
    public static event System.Action<string> OnInitializationError;
    
    // Properties
    public bool IsInitialized => _isInitialized;
    public bool IsReady => _isInitialized; // IGameManager implementation
    public IReadOnlyDictionary<ManagerType, IGameManager> Managers => _managers;
    
    
    
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
        
        // Step 3: Verify all critical managers
        if (!GameExtensions.AreAllCriticalManagersReady())
        {
            OnInitializationError?.Invoke("Critical managers missing!");
            yield break;
        }
        
        // Step 4: Complete initialization
        _isInitialized = true;
        OnAllManagersReady?.Invoke();
        Debug.Log("[GameManager] All managers initialized successfully!");
        
        // Log status using GameExtensions
        GameExtensions.LogManagerPerformance();
        
        // Step 5: Auto-start combat if enabled
        if (autoStartCombat)
        {
            yield return new WaitForSeconds(0.1f);
            GameExtensions.TryStartCombat();
        }
    }
    
    private void DiscoverManagers()
    {
        RegisterManagerSafely(ManagerType.Card, GameExtensions.GetManager<CardManager>());
        RegisterManagerSafely(ManagerType.Deck, GameExtensions.GetManager<DeckManager>());
        RegisterManagerSafely(ManagerType.HandLayout, GameExtensions.GetManager<HandLayoutManager>());
        RegisterManagerSafely(ManagerType.CardSlot, GameExtensions.GetManager<CardSlotManager>());
        RegisterManagerSafely(ManagerType.Spellcast, GameExtensions.GetManager<SpellcastManager>());
        RegisterManagerSafely(ManagerType.Combat, GameExtensions.GetManager<CombatManager>());
        RegisterManagerSafely(ManagerType.Enemy, GameExtensions.GetManager<EnemyManager>());
        RegisterManagerSafely(ManagerType.Unit, GameExtensions.GetManager<UnitManager>());
    
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
        // Use GameExtensions for safer combat start
        GameExtensions.TryStartCombat();
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
    public CardSlotManager CardSlotManager => GetManager<CardSlotManager>(ManagerType.CardSlot);
    

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
        // Use GameExtensions for comprehensive status
        GameExtensions.LogManagerPerformance();
        
        Debug.Log($"[GameManager] Detailed Status - Initialized: {_isInitialized}");
        foreach (var kvp in _managers)
        {
            Debug.Log($"  {kvp.Key}: {(kvp.Value?.IsReady ?? false ? "Ready" : "Not Ready")}");
        }
    }
    
    [ContextMenu("Test Card Slot System")]
    public void TestCardSlotSystem()
    {
        var csm = CardSlotManager;
        if (csm != null)
        {
            Debug.Log($"[GameManager] CardSlotManager Test:");
            Debug.Log($"  IsReady: {csm.IsReady}");
            Debug.Log($"  IsEnabled: {csm.IsEnabled}");
            Debug.Log($"  SlotCount: {csm.SlotCount}");
            Debug.Log($"  FilledSlots: {csm.FilledSlotCount}");
        }
        else
        {
            Debug.LogError("[GameManager] CardSlotManager not found!");
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