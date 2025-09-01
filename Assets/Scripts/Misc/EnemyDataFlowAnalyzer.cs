using UnityEngine;
using System.Collections.Generic;
using System.Linq;


/// <summary>
/// Comprehensive analyzer for Enemy -> UI data flow
/// Add this as a component to diagnose the enemy panel issues
/// </summary>
public class EnemyDataFlowAnalyzer : MonoBehaviour
{
    [Header("Analysis Settings")]
    [SerializeField] private bool enableRealTimeLogging = false;
    [SerializeField] private bool logEventDetails = true;
    [SerializeField] private bool logDataValidation = true;
    
    private Dictionary<EntityBehaviour, EntityAnalysis> _entityAnalysisData = new Dictionary<EntityBehaviour, EntityAnalysis>();
    private List<EventLogEntry> _eventLog = new List<EventLogEntry>();
    private GameUIHandler _uiHandler;
    
    private void Start()
    {
        _uiHandler = FindFirstObjectByType<GameUIHandler>();
        if (_uiHandler == null)
        {
            Debug.LogError("[EnemyAnalyzer] GameUIHandler not found!");
            return;
        }
        
        SetupEventListeners();
        
        // Initial analysis after 1 second
        Invoke(nameof(PerformInitialAnalysis), 1f);
    }
    
    private void SetupEventListeners()
    {
        // Entity static events - ONLY LISTENING
        EntityBehaviour.OnEntityHovered += OnEntityHoveredAnalysis;
        EntityBehaviour.OnEntityUnhovered += OnEntityUnhoveredAnalysis;
        EntityBehaviour.OnEntityTargeted += OnEntityTargetedAnalysis;
        EntityBehaviour.OnEntityUntargeted += OnEntityUntargetedAnalysis;
        EntityBehaviour.OnEntityHealthChanged += OnEntityHealthChangedAnalysis;
        
        // EnemyManager events - ONLY LISTENING
        if (EnemyManager.HasInstance)
        {
            EnemyManager.OnEnemySpawned += OnEnemySpawnedAnalysis;
            EnemyManager.OnEnemyDespawned += OnEnemyDespawnedAnalysis;
            EnemyManager.OnEnemyTargeted += OnEnemyTargetedAnalysis;
            EnemyManager.OnEnemyDamaged += OnEnemyDamagedAnalysis;
        }
        
        Debug.Log("[EnemyAnalyzer] Event listeners setup complete");
    }
    
    private void OnDestroy()
    {
        // Cleanup events
        EntityBehaviour.OnEntityHovered -= OnEntityHoveredAnalysis;
        EntityBehaviour.OnEntityUnhovered -= OnEntityUnhoveredAnalysis;
        EntityBehaviour.OnEntityTargeted -= OnEntityTargetedAnalysis;
        EntityBehaviour.OnEntityUntargeted -= OnEntityUntargetedAnalysis;
        EntityBehaviour.OnEntityHealthChanged -= OnEntityHealthChangedAnalysis;
        
        if (EnemyManager.HasInstance)
        {
            EnemyManager.OnEnemySpawned -= OnEnemySpawnedAnalysis;
            EnemyManager.OnEnemyDespawned -= OnEnemyDespawnedAnalysis;
            EnemyManager.OnEnemyTargeted -= OnEnemyTargetedAnalysis;
            EnemyManager.OnEnemyDamaged -= OnEnemyDamagedAnalysis;
        }
    }
    
    // ===== EVENT ANALYSIS METHODS =====
    
    private void OnEntityHoveredAnalysis(EntityBehaviour entity)
    {
        LogEvent("EntityHovered", entity, $"Entity hovered. IsEnemy: {entity?.IsEnemy()}, IsValidTarget: {entity?.IsValidTarget()}");
        
        if (entity != null && entity.IsEnemy())
        {
            AnalyzeEntityData(entity, "Hover");
            
            if (logEventDetails)
            {
                Debug.Log($"[EnemyAnalyzer] ✅ HOVER EVENT: {entity.EntityName}");
                Debug.Log($"  - Type: {entity.Type}");
                Debug.Log($"  - IsValidTarget: {entity.IsValidTarget()}");
                Debug.Log($"  - Health: {entity.CurrentHealth}/{entity.MaxHealth} ({entity.HealthPercentage:P0})");
                Debug.Log($"  - GameObject Active: {entity.gameObject.activeInHierarchy}");
                Debug.Log($"  - Transform Position: {entity.transform.position}");
            }
        }
    }
    
    private void OnEntityUnhoveredAnalysis(EntityBehaviour entity)
    {
        LogEvent("EntityUnhovered", entity, $"Entity unhovered. IsEnemy: {entity?.IsEnemy()}");
        
        if (logEventDetails && entity?.IsEnemy() == true)
        {
            Debug.Log($"[EnemyAnalyzer] ❌ UNHOVER EVENT: {entity.EntityName}");
        }
    }
    
    private void OnEntityTargetedAnalysis(EntityBehaviour entity)
    {
        LogEvent("EntityTargeted", entity, $"Entity targeted. IsEnemy: {entity?.IsEnemy()}");
        
        if (entity != null && entity.IsEnemy())
        {
            AnalyzeEntityData(entity, "Target");
            
            if (logEventDetails)
            {
                Debug.Log($"[EnemyAnalyzer] 🎯 TARGET EVENT: {entity.EntityName}");
                Debug.Log($"  - IsTargeted: {entity.IsTargeted()}");
            }
        }
    }
    
    private void OnEntityUntargetedAnalysis(EntityBehaviour entity)
    {
        LogEvent("EntityUntargeted", entity, $"Entity untargeted. IsEnemy: {entity?.IsEnemy()}");
    }
    
    private void OnEntityHealthChangedAnalysis(EntityBehaviour entity, int oldHealth, int newHealth)
    {
        if (entity?.IsEnemy() == true)
        {
            LogEvent("EntityHealthChanged", entity, $"Health: {oldHealth} → {newHealth}");
            
            if (logEventDetails)
            {
                Debug.Log($"[EnemyAnalyzer] ❤️ HEALTH CHANGE: {entity.EntityName} ({oldHealth} → {newHealth})");
            }
        }
    }
    
    private void OnEnemySpawnedAnalysis(EntityBehaviour enemy)
    {
        LogEvent("EnemySpawned", enemy, "Enemy spawned via EnemyManager");
        AnalyzeEntityData(enemy, "Spawn");
        
        if (logEventDetails)
        {
            Debug.Log($"[EnemyAnalyzer] 🆕 ENEMY SPAWNED: {enemy?.EntityName}");
            LogEntityDetails(enemy);
        }
    }
    
    private void OnEnemyDespawnedAnalysis(EntityBehaviour enemy)
    {
        LogEvent("EnemyDespawned", enemy, "Enemy despawned via EnemyManager");
        
        if (_entityAnalysisData.ContainsKey(enemy))
            _entityAnalysisData.Remove(enemy);
    }
    
    private void OnEnemyTargetedAnalysis(EntityBehaviour enemy)
    {
        LogEvent("EnemyManagerTargeted", enemy, "Enemy targeted via EnemyManager");
        
        if (logEventDetails)
        {
            Debug.Log($"[EnemyAnalyzer] 🎯 ENEMY MANAGER TARGET: {enemy?.EntityName}");
        }
    }
    
    private void OnEnemyDamagedAnalysis(EntityBehaviour enemy, int damage)
    {
        LogEvent("EnemyDamaged", enemy, $"Enemy damaged: {damage}");
    }
    
    // ===== DATA ANALYSIS METHODS =====
    
    private void AnalyzeEntityData(EntityBehaviour entity, string context)
    {
        if (entity == null) return;
        
        var analysis = new EntityAnalysis
        {
            Entity = entity,
            Context = context,
            Timestamp = System.DateTime.Now,
            
            // Basic data
            EntityName = entity.EntityName,
            EntityType = entity.Type,
            IsAlive = entity.IsAlive,
            IsTargetable = entity.IsTargetable,
            IsTargeted = entity.IsTargeted(),
            
            // Health data
            CurrentHealth = entity.CurrentHealth,
            MaxHealth = entity.MaxHealth,
            HealthPercentage = entity.HealthPercentage,
            
            // Asset data
            HasAsset = entity.Asset != null,
            AssetName = entity.Asset?.EntityName ?? "NULL",
            AssetType = entity.Asset?.Type ?? EntityType.Environmental,
            AssetIsTargetable = entity.Asset?.IsTargetable ?? false,
            
            // GameObject data
            IsGameObjectActive = entity.gameObject.activeInHierarchy,
            HasCollider = entity.GetComponent<Collider>() != null,
            ColliderEnabled = entity.GetComponent<Collider>()?.enabled ?? false,
            Position = entity.transform.position,
            
            // Component validation
            HasPointerHandlers = ValidatePointerHandlers(entity),
            ComponentCount = entity.GetComponents<Component>().Length
        };
        
        _entityAnalysisData[entity] = analysis;
        
        if (logDataValidation)
        {
            ValidateEntityData(analysis);
        }
    }
    
    private bool ValidatePointerHandlers(EntityBehaviour entity)
    {
        var hasClick = entity.GetComponent<UnityEngine.EventSystems.IPointerClickHandler>() != null;
        var hasEnter = entity.GetComponent<UnityEngine.EventSystems.IPointerEnterHandler>() != null;
        var hasExit = entity.GetComponent<UnityEngine.EventSystems.IPointerExitHandler>() != null;
        
        return hasClick && hasEnter && hasExit;
    }
    
    private void ValidateEntityData(EntityAnalysis analysis)
    {
        var issues = new List<string>();
        
        // Critical validation checks
        if (string.IsNullOrEmpty(analysis.EntityName))
            issues.Add("Empty EntityName");
            
        if (!analysis.HasAsset)
            issues.Add("Missing EntityAsset");
            
        if (!analysis.IsGameObjectActive)
            issues.Add("GameObject inactive");
            
        if (!analysis.HasCollider)
            issues.Add("Missing Collider");
            
        if (!analysis.ColliderEnabled)
            issues.Add("Collider disabled");
            
        if (!analysis.HasPointerHandlers)
            issues.Add("Missing pointer event handlers");
            
        if (analysis.EntityType != EntityType.Enemy)
            issues.Add($"Wrong entity type: {analysis.EntityType}");
            
        if (!analysis.IsTargetable)
            issues.Add("Not targetable");
            
        if (!analysis.IsAlive)
            issues.Add("Not alive");
        
        // Log results
        if (issues.Count > 0)
        {
            Debug.LogWarning($"[EnemyAnalyzer] ⚠️ VALIDATION ISSUES for {analysis.EntityName}:");
            foreach (var issue in issues)
                Debug.LogWarning($"  - {issue}");
        }
        else if (enableRealTimeLogging)
        {
            Debug.Log($"[EnemyAnalyzer] ✅ {analysis.EntityName} passed all validation checks");
        }
    }
    
    private void LogEvent(string eventType, EntityBehaviour entity, string details)
    {
        var logEntry = new EventLogEntry
        {
            Timestamp = System.DateTime.Now,
            EventType = eventType,
            EntityName = entity?.EntityName ?? "NULL",
            Details = details,
            EntityValid = entity != null && entity.IsEnemy()
        };
        
        _eventLog.Add(logEntry);
        
        // Keep log size manageable
        if (_eventLog.Count > 100)
            _eventLog.RemoveAt(0);
        
        if (enableRealTimeLogging)
        {
            Debug.Log($"[EnemyAnalyzer] EVENT: {eventType} | {entity?.EntityName ?? "NULL"} | {details}");
        }
    }
    
    // ===== PUBLIC ANALYSIS METHODS =====
    
    [ContextMenu("Perform Complete Analysis")]
    public void PerformInitialAnalysis()
    {
        Debug.Log("=== ENEMY DATA FLOW ANALYSIS ===");
        
        AnalyzeGameUIHandler();
        AnalyzeEnemyManager();
        AnalyzeAllEnemies();
        AnalyzeEventSubscriptions();
        
        Debug.Log("=== ANALYSIS COMPLETE ===");
    }
    
    private void AnalyzeGameUIHandler()
    {
        Debug.Log("--- GAME UI HANDLER ANALYSIS ---");
        
        if (_uiHandler == null)
        {
            Debug.LogError("❌ GameUIHandler not found!");
            return;
        }
        
        Debug.Log($"✅ GameUIHandler found: {_uiHandler.name}");
        Debug.Log($"GameObject active: {_uiHandler.gameObject.activeInHierarchy}");
        Debug.Log($"Component enabled: {_uiHandler.enabled}");
        
        // Check for UI elements by searching for common names
        CheckUIFieldAssignments();
    }
    
    private void CheckUIFieldAssignments()
    {
        Debug.Log("--- UI FIELD ASSIGNMENTS ---");
        
        // Search for enemy panel related UI elements
        var allTextComponents = FindObjectsOfType<TMPro.TextMeshProUGUI>(true);
        var allSliders = FindObjectsOfType<UnityEngine.UI.Slider>(true);
        var allGameObjects = FindObjectsOfType<GameObject>(true);
        
        // Look for enemy-related UI elements
        var enemyTexts = allTextComponents.Where(t => t.name.ToLower().Contains("enemy")).ToArray();
        var enemySliders = allSliders.Where(s => s.name.ToLower().Contains("enemy")).ToArray();
        var enemyPanels = allGameObjects.Where(go => go.name.ToLower().Contains("enemy") && go.name.ToLower().Contains("panel")).ToArray();
        
        Debug.Log($"Enemy-related text components: {enemyTexts.Length}");
        foreach (var text in enemyTexts)
            Debug.Log($"  - {text.name} (Active: {text.gameObject.activeInHierarchy})");
        
        Debug.Log($"Enemy-related sliders: {enemySliders.Length}");
        foreach (var slider in enemySliders)
            Debug.Log($"  - {slider.name} (Active: {slider.gameObject.activeInHierarchy})");
        
        Debug.Log($"Enemy panels found: {enemyPanels.Length}");
        foreach (var panel in enemyPanels)
            Debug.Log($"  - {panel.name} (Active: {panel.activeInHierarchy})");
    }
    
    private void AnalyzeEnemyManager()
    {
        Debug.Log("--- ENEMY MANAGER ANALYSIS ---");
        
        if (!EnemyManager.HasInstance)
        {
            Debug.LogError("❌ EnemyManager not found!");
            return;
        }
        
        var em = EnemyManager.Instance;
        Debug.Log($"✅ EnemyManager found");
        Debug.Log($"Is ready: {em.IsReady}");
        Debug.Log($"Total enemies: {em.EnemyCount}");
        Debug.Log($"Alive enemies: {em.AliveEnemyCount}");
        Debug.Log($"Targeted enemies: {em.TargetedEnemies.Count}");
    }
    
    private void AnalyzeAllEnemies()
    {
        Debug.Log("--- ALL ENEMIES ANALYSIS ---");
        
        if (!EnemyManager.HasInstance)
        {
            Debug.LogError("❌ No EnemyManager for enemy analysis");
            return;
        }
        
        var enemies = EnemyManager.Instance.AllEnemies;
        Debug.Log($"Analyzing {enemies.Count} total enemies:");
        
        for (int i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            Debug.Log($"--- ENEMY {i + 1}: {enemy?.EntityName ?? "NULL"} ---");
            
            if (enemy != null)
            {
                LogEntityDetails(enemy);
                AnalyzeEntityData(enemy, "Manual Analysis");
            }
            else
            {
                Debug.LogError("❌ NULL enemy found in collection!");
            }
        }
    }
    
    private void LogEntityDetails(EntityBehaviour entity)
    {
        if (entity == null)
        {
            Debug.LogError("❌ Cannot log details for NULL entity");
            return;
        }
        
        Debug.Log($"Basic Info:");
        Debug.Log($"  - Name: {entity.EntityName}");
        Debug.Log($"  - Type: {entity.Type}");
        Debug.Log($"  - IsAlive: {entity.IsAlive}");
        Debug.Log($"  - IsTargetable: {entity.IsTargetable}");
        Debug.Log($"  - IsTargeted: {entity.IsTargeted()}");
        Debug.Log($"  - Health: {entity.CurrentHealth}/{entity.MaxHealth} ({entity.HealthPercentage:P0})");
        
        Debug.Log($"GameObject Info:");
        Debug.Log($"  - Active: {entity.gameObject.activeInHierarchy}");
        Debug.Log($"  - Layer: {entity.gameObject.layer}");
        Debug.Log($"  - Position: {entity.transform.position}");
        
        Debug.Log($"Components:");
        var collider = entity.GetComponent<Collider>();
        Debug.Log($"  - Collider: {collider != null} (enabled: {collider?.enabled})");
        Debug.Log($"  - Has IPointerClickHandler: {entity is UnityEngine.EventSystems.IPointerClickHandler}");
        Debug.Log($"  - Has IPointerEnterHandler: {entity is UnityEngine.EventSystems.IPointerEnterHandler}");
        Debug.Log($"  - Has IPointerExitHandler: {entity is UnityEngine.EventSystems.IPointerExitHandler}");
        
        Debug.Log($"Asset Info:");
        if (entity.Asset != null)
        {
            Debug.Log($"  - Asset Name: {entity.Asset.EntityName}");
            Debug.Log($"  - Asset Type: {entity.Asset.Type}");
            Debug.Log($"  - Asset IsTargetable: {entity.Asset.IsTargetable}");
            Debug.Log($"  - Asset Valid: {entity.Asset.IsValid}");
        }
        else
        {
            Debug.LogError("❌ EntityAsset is NULL!");
        }
    }
    
    private void AnalyzeEventSubscriptions()
    {
        Debug.Log("--- EVENT SUBSCRIPTION ANALYSIS ---");
        Debug.Log($"Event log entries: {_eventLog.Count}");
        
        if (_eventLog.Count > 0)
        {
            Debug.Log("Recent events:");
            var recentEvents = _eventLog.TakeLast(5);
            foreach (var evt in recentEvents)
            {
                Debug.Log($"  - {evt.EventType}: {evt.EntityName} | {evt.Details}");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ No events logged yet - events may not be firing!");
        }
    }
    
    // FIX: Use public test methods from the classes themselves
    [ContextMenu("Test Enemy Click (via EnemyManager)")]
    public void TestEnemyClick()
    {
        if (!EnemyManager.HasInstance)
        {
            Debug.LogError("❌ No EnemyManager found for testing");
            return;
        }
        
        var firstEnemy = EnemyManager.Instance.AliveEnemies.FirstOrDefault();
        if (firstEnemy != null)
        {
            Debug.Log($"🧪 TESTING: Triggering click through EnemyManager for {firstEnemy.EntityName}");
            EnemyManager.Instance.HandleEntityClicked(firstEnemy);
        }
        else
        {
            Debug.LogWarning("⚠️ No alive enemies to test with");
        }
    }
    
    [ContextMenu("Test Entity Hover (via EntityBehaviour)")]
    public void TestEntityHover()
    {
        if (!EnemyManager.HasInstance) return;
        
        var firstEnemy = EnemyManager.Instance.AliveEnemies.FirstOrDefault();
        if (firstEnemy != null)
        {
            Debug.Log($"🧪 TESTING: Using EntityBehaviour test method for {firstEnemy.EntityName}");
            
            // Try to call the public test method if it exists
            var method = firstEnemy.GetType().GetMethod("TestEntityEvents", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(firstEnemy, null);
            }
            else
            {
                Debug.LogWarning("⚠️ TestEntityEvents method not found on EntityBehaviour");
            }
        }
    }
    
    [ContextMenu("Simulate Pointer Enter")]
    public void SimulatePointerEnter()
    {
        if (!EnemyManager.HasInstance) return;
        
        var firstEnemy = EnemyManager.Instance.AliveEnemies.FirstOrDefault();
        if (firstEnemy != null)
        {
            Debug.Log($"🧪 TESTING: Simulating OnPointerEnter for {firstEnemy.EntityName}");
            
            // Create a fake PointerEventData
            var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            
            // Call OnPointerEnter directly if possible
            var pointerHandler = firstEnemy as UnityEngine.EventSystems.IPointerEnterHandler;
            if (pointerHandler != null)
            {
                pointerHandler.OnPointerEnter(eventData);
            }
            else
            {
                Debug.LogWarning("⚠️ Enemy does not implement IPointerEnterHandler");
            }
        }
    }
    
    [ContextMenu("Log Current State")]
    public void LogCurrentState()
    {
        Debug.Log("=== CURRENT STATE SNAPSHOT ===");
        Debug.Log($"Analyzed entities: {_entityAnalysisData.Count}");
        Debug.Log($"Event log entries: {_eventLog.Count}");
        Debug.Log($"GameUIHandler exists: {_uiHandler != null}");
        
        if (EnemyManager.HasInstance)
        {
            Debug.Log($"EnemyManager alive enemies: {EnemyManager.Instance.AliveEnemyCount}");
            Debug.Log($"EnemyManager targeted enemies: {EnemyManager.Instance.TargetedEnemies.Count}");
        }
    }
}

// Supporting data classes
[System.Serializable]
public class EntityAnalysis
{
    public EntityBehaviour Entity;
    public string Context;
    public System.DateTime Timestamp;
    
    // Basic data
    public string EntityName;
    public EntityType EntityType;
    public bool IsAlive;
    public bool IsTargetable;
    public bool IsTargeted;
    
    // Health data
    public int CurrentHealth;
    public int MaxHealth;
    public float HealthPercentage;
    
    // Asset data
    public bool HasAsset;
    public string AssetName;
    public EntityType AssetType;
    public bool AssetIsTargetable;
    
    // GameObject data
    public bool IsGameObjectActive;
    public bool HasCollider;
    public bool ColliderEnabled;
    public Vector3 Position;
    
    // Component validation
    public bool HasPointerHandlers;
    public int ComponentCount;
}

[System.Serializable]
public class EventLogEntry
{
    public System.DateTime Timestamp;
    public string EventType;
    public string EntityName;
    public string Details;
    public bool EntityValid;
}