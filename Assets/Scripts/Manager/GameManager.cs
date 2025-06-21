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
        
        // Step 1: Discover all managers
        DiscoverManagers();
        
        // Step 2: Initialize in order
        foreach (var config in managerInitOrder)
        {
            if (!config.enabled) continue;
            
            yield return StartCoroutine(InitializeManager(config.managerType));
            yield return new WaitForSeconds(initStepDelay);
        }
        
        // Step 3: Verify all critical managers
        if (!VerifyCriticalManagers())
        {
            OnInitializationError?.Invoke("Critical managers missing!");
            yield break;
        }
        
        // Step 4: Complete initialization
        _isInitialized = true;
        OnAllManagersReady?.Invoke();
        Debug.Log("[GameManager] All managers initialized successfully!");
        
        // Step 5: Auto-start combat if enabled
        if (autoStartCombat)
        {
            yield return new WaitForSeconds(0.1f);
            StartCombat();
        }
    }
    
    private void DiscoverManagers()
    {
        // Find all manager instances - use HasInstance check
        if (CardManager.HasInstance)
            RegisterManager(ManagerType.Card, CardManager.Instance);
        if (DeckManager.HasInstance)
            RegisterManager(ManagerType.Deck, DeckManager.Instance);
        if (HandLayoutManager.HasInstance)
            RegisterManager(ManagerType.HandLayout, HandLayoutManager.Instance);
        if (SpellcastManager.HasInstance)
            RegisterManager(ManagerType.Spellcast, SpellcastManager.Instance);
        if (CombatManager.HasInstance)
            RegisterManager(ManagerType.Combat, CombatManager.Instance);
        if (EnemyManager.HasInstance)
            RegisterManager(ManagerType.Enemy, EnemyManager.Instance);
        if (UnitManager.HasInstance)
            RegisterManager(ManagerType.Unit, UnitManager.Instance);
            
        Debug.Log($"[GameManager] Discovered {_managers.Count} managers");
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
    
    private bool VerifyCriticalManagers()
    {
        var criticalTypes = new[] { ManagerType.Card, ManagerType.Deck, ManagerType.Combat };
        
        Debug.Log("[GameManager] Verifying critical managers...");
        
        foreach (var type in criticalTypes)
        {
            if (!_managers.TryGetValue(type, out var manager))
            {
                Debug.LogError($"[GameManager] Critical manager {type} not found in registry!");
                return false;
            }
            
            if (manager == null)
            {
                Debug.LogError($"[GameManager] Critical manager {type} is null!");
                return false;
            }
            
            if (!manager.IsReady)
            {
                Debug.LogError($"[GameManager] Critical manager {type} not ready! (Instance exists: {manager != null})");
                
                // Spezial-Check für CombatManager
                if (type == ManagerType.Combat && manager is CombatManager cm)
                {
                    Debug.LogError($"  CombatManager._isReady = {cm.IsReady}");
                    Debug.LogError($"  CombatManager instance = {cm.GetInstanceID()}");
                }
                
                return false;
            }
            
            Debug.Log($"[GameManager] {type} manager verified ✓");
        }
        
        return true;
    }
    
    public void StartCombat()
    {
        if (!_isInitialized)
        {
            Debug.LogWarning("[GameManager] Cannot start combat - not initialized");
            return;
        }
        
        if (_managers.TryGetValue(ManagerType.Combat, out var combatManager))
        {
            (combatManager as CombatManager)?.StartCombat();
        }
    }
    
    
    public T GetManager<T>(ManagerType type) where T : class, IGameManager
    {
        return _managers.TryGetValue(type, out var manager) ? manager as T : null;
    }
    
    // Quick access properties
    public CardManager CardManager => GetManager<CardManager>(ManagerType.Card);
    public DeckManager DeckManager => GetManager<DeckManager>(ManagerType.Deck);
    public CombatManager CombatManager => GetManager<CombatManager>(ManagerType.Combat);
    public SpellcastManager SpellcastManager => GetManager<SpellcastManager>(ManagerType.Spellcast);
    
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
        Debug.Log($"[GameManager] Status - Initialized: {_isInitialized}");
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