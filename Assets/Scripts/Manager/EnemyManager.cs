using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

// FIXED: Using Aliases für ambiguous references
using UnityRandom = UnityEngine.Random;

public class EnemyManager : SingletonBehaviour<EnemyManager>, IGameManager
{
    [Header("Enemy Management")]
    [SerializeField] private int maxEnemies = 10;
    [SerializeField] private Transform enemyContainer;
    
    [Header("Targeting")]
    [SerializeField] private bool allowMultiTarget = false;
    [SerializeField] private int maxTargets = 1;
    
    [Header("Spawn Settings")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
    [SerializeField] private float spawnAnimationDuration = 0.5f;
    
    // FIX 1: Enhanced enemy tracking with better validation
    private Dictionary<int, EntityBehaviour> _enemies = new Dictionary<int, EntityBehaviour>();
    private List<EntityBehaviour> _targetedEnemies = new List<EntityBehaviour>();
    private int _nextEnemyId = 0;
    
    // FIX 2: State validation
    private bool _isReady = false;
    private bool _eventsSetup = false;
    
    public bool IsReady => _isReady;
    
    // Events - FIXED: Korrekte System.Action Syntax
    public static event System.Action<EntityBehaviour> OnEnemySpawned;
    public static event System.Action<EntityBehaviour> OnEnemyDespawned;
    public static event System.Action<EntityBehaviour> OnEnemyTargeted;
    public static event System.Action<EntityBehaviour> OnEnemyUntargeted;
    public static event System.Action<List<EntityBehaviour>> OnTargetsChanged;
    public static event System.Action<EntityBehaviour, int> OnEnemyDamaged;
    public static event System.Action<EntityBehaviour> OnEnemyKilled;
    public static event System.Action OnAllEnemiesDefeated;
    
    // Properties - Enhanced with better validation
    public int EnemyCount => _enemies.Values.Count(e => e != null);
    public int AliveEnemyCount => _enemies.Values.Count(e => e != null && e.IsValidTarget());
    public bool HasEnemies => AliveEnemyCount > 0;
    public IReadOnlyList<EntityBehaviour> AllEnemies => _enemies.Values.Where(e => e != null).ToList();
    public IReadOnlyList<EntityBehaviour> AliveEnemies => _enemies.Values.Where(e => e != null && e.IsValidTarget()).ToList();
    public IReadOnlyList<EntityBehaviour> TargetedEnemies => _targetedEnemies.Where(e => e != null).ToList();
    public EntityBehaviour PrimaryTarget => _targetedEnemies.FirstOrDefault(e => e != null);
    
    protected override void OnAwakeInitialize()
    {
        InitializeContainer();
        SetupEvents();
        _isReady = true;
        Debug.Log("[EnemyManager] Initialized and ready");
    }
    
    // FIX 3: Enhanced event setup
    private void SetupEvents()
    {
        if (_eventsSetup) return;
        
        EntityBehaviour.OnEntityHealthChanged += HandleEntityHealthChanged;
        EntityBehaviour.OnEntityDestroyed += HandleEntityDestroyed;
        
        _eventsSetup = true;
        Debug.Log("[EnemyManager] Events setup complete");
    }
    
    private void InitializeContainer()
    {
        if (enemyContainer == null)
        {
            GameObject container = new GameObject("Enemy Container");
            container.transform.SetParent(transform);
            enemyContainer = container.transform;
            Debug.Log("[EnemyManager] Created enemy container");
        }
    }
    
    private void OnDestroy()
    {
        if (_eventsSetup)
        {
            EntityBehaviour.OnEntityHealthChanged -= HandleEntityHealthChanged;
            EntityBehaviour.OnEntityDestroyed -= HandleEntityDestroyed;
        }
    }
    
    // FIX 4: Enhanced enemy spawning with better validation
    public EntityBehaviour SpawnEnemy(EntityAsset enemyAsset, Vector3 position, Quaternion rotation = default)
    {
        if (!ValidateSpawn(enemyAsset)) return null;
        
        if (rotation == default)
            rotation = Quaternion.identity;
        
        Debug.Log($"[EnemyManager] Spawning enemy: {enemyAsset.EntityName} at {position}");
        
        GameObject enemyObject = enemyAsset.CreateInstance(position, rotation, enemyContainer);
        if (enemyObject == null)
        {
            Debug.LogError($"[EnemyManager] Failed to create enemy instance for {enemyAsset.EntityName}");
            return null;
        }
        
        EntityBehaviour enemy = enemyObject.GetComponent<EntityBehaviour>();
        if (enemy == null)
        {
            Debug.LogError($"[EnemyManager] Enemy object missing EntityBehaviour component");
            Destroy(enemyObject);
            return null;
        }
        
        // FIX 5: Ensure enemy is properly configured
        if (!ValidateEnemySetup(enemy))
        {
            Debug.LogError($"[EnemyManager] Enemy {enemy.EntityName} failed validation");
            Destroy(enemyObject);
            return null;
        }
        
        if (enemyAsset.SpawnWithAnimation)
        {
            StartCoroutine(SpawnAnimation(enemy, enemyAsset.SpawnDelay));
        }
        
        Debug.Log($"[EnemyManager] Successfully spawned enemy: {enemy.EntityName}");
        return enemy;
    }
    
    // FIX 6: Enemy validation
    private bool ValidateEnemySetup(EntityBehaviour enemy)
    {
        if (enemy == null) return false;
        
        if (!enemy.IsEnemy())
        {
            Debug.LogError($"[EnemyManager] Entity {enemy.EntityName} is not an Enemy type");
            return false;
        }
        
        if (!enemy.IsTargetable)
        {
            Debug.LogWarning($"[EnemyManager] Enemy {enemy.EntityName} is not targetable");
        }
        
        var collider = enemy.GetComponent<Collider>();
        if (collider == null)
        {
            Debug.LogWarning($"[EnemyManager] Enemy {enemy.EntityName} has no collider - adding BoxCollider");
            var boxCollider = enemy.gameObject.AddComponent<BoxCollider>();
            boxCollider.size = Vector3.one * 2f;
            boxCollider.center = Vector3.up;
        }
        
        return true;
    }
    
    public EntityBehaviour SpawnEnemyAtRandomPoint(EntityAsset enemyAsset)
    {
        if (spawnPoints.Count == 0)
        {
            Debug.LogWarning("[EnemyManager] No spawn points configured, using origin");
            return SpawnEnemy(enemyAsset, Vector3.zero);
        }
        
        // FIXED: UnityRandom instead of Random
        Transform spawnPoint = spawnPoints[UnityRandom.Range(0, spawnPoints.Count)];
        return SpawnEnemy(enemyAsset, spawnPoint.position, spawnPoint.rotation);
    }
    
    private bool ValidateSpawn(EntityAsset enemyAsset)
    {
        if (enemyAsset == null)
        {
            Debug.LogError("[EnemyManager] Cannot spawn NULL enemy asset");
            return false;
        }
        
        if (enemyAsset.Type != EntityType.Enemy)
        {
            Debug.LogError($"[EnemyManager] Asset {enemyAsset.EntityName} is not Enemy type: {enemyAsset.Type}");
            return false;
        }
        
        if (_enemies.Count >= maxEnemies)
        {
            Debug.LogWarning($"[EnemyManager] Max enemy limit reached ({maxEnemies})");
            return false;
        }
        
        if (!enemyAsset.IsValid)
        {
            Debug.LogError($"[EnemyManager] Enemy asset {enemyAsset.EntityName} is not valid");
            return false;
        }
        
        return true;
    }
    
    private IEnumerator SpawnAnimation(EntityBehaviour enemy, float delay)
    {
        if (delay > 0)
            yield return new WaitForSeconds(delay);
        
        Vector3 originalScale = enemy.transform.localScale;
        enemy.transform.localScale = Vector3.zero;
        
        float elapsed = 0f;
        while (elapsed < spawnAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / spawnAnimationDuration;
            enemy.transform.localScale = Vector3.Lerp(Vector3.zero, originalScale, t);
            yield return null;
        }
        
        enemy.transform.localScale = originalScale;
    }
    
    // FIX 7: Enhanced registration with validation
    public void RegisterEnemy(EntityBehaviour enemy)
    {
        if (enemy == null)
        {
            Debug.LogError("[EnemyManager] Cannot register NULL enemy");
            return;
        }
        
        if (!enemy.IsEnemy())
        {
            Debug.LogError($"[EnemyManager] Entity {enemy.EntityName} is not Enemy type: {enemy.Type}");
            return;
        }
        
        // Check if already registered
        if (_enemies.Values.Contains(enemy))
        {
            Debug.LogWarning($"[EnemyManager] Enemy {enemy.EntityName} already registered");
            return;
        }
        
        int id = _nextEnemyId++;
        _enemies[id] = enemy;
        
        Debug.Log($"[EnemyManager] Enemy registered: {enemy.EntityName} (ID: {id}, Total: {_enemies.Count})");
        
        // FIX 8: Fire event after registration
        OnEnemySpawned?.Invoke(enemy);
    }
    
    public void UnregisterEnemy(EntityBehaviour enemy)
    {
        if (enemy == null) return;
        
        var kvp = _enemies.FirstOrDefault(x => x.Value == enemy);
        if (kvp.Value != null)
        {
            _enemies.Remove(kvp.Key);
            _targetedEnemies.Remove(enemy);
            
            Debug.Log($"[EnemyManager] Enemy unregistered: {enemy.EntityName} (Remaining: {_enemies.Count})");
            
            OnEnemyDespawned?.Invoke(enemy);
            
            if (_targetedEnemies.Count > 0)
                OnTargetsChanged?.Invoke(_targetedEnemies.ToList());
            
            CheckAllEnemiesDefeated();
        }
    }
    
    // FIX 9: Enhanced click handling with better logging
    public void HandleEntityClicked(EntityBehaviour enemy)
    {
        Debug.Log($"[EnemyManager] HandleEntityClicked: {enemy?.EntityName ?? "NULL"}");
        
        if (enemy == null)
        {
            Debug.LogWarning("[EnemyManager] Clicked entity is NULL");
            return;
        }
        
        if (!enemy.IsValidTarget())
        {
            Debug.LogWarning($"[EnemyManager] Enemy {enemy.EntityName} is not a valid target");
            Debug.Log($"  - IsAlive: {enemy.IsAlive}");
            Debug.Log($"  - IsTargetable: {enemy.IsTargetable}");
            Debug.Log($"  - Health: {enemy.CurrentHealth}/{enemy.MaxHealth}");
            return;
        }
        
        Debug.Log($"[EnemyManager] Processing valid enemy click: {enemy.EntityName}");
        Debug.Log($"  - Currently targeted enemies: {_targetedEnemies.Count}");
        Debug.Log($"  - Is already targeted: {_targetedEnemies.Contains(enemy)}");
        
        if (_targetedEnemies.Contains(enemy))
        {
            Debug.Log($"[EnemyManager] Untargeting already targeted enemy: {enemy.EntityName}");
            UntargetEnemy(enemy);
        }
        else
        {
            Debug.Log($"[EnemyManager] Targeting new enemy: {enemy.EntityName}");
            TargetEnemy(enemy);
        }
    }
    
    // FIX 10: Enhanced targeting with detailed logging
    public void TargetEnemy(EntityBehaviour enemy)
    {
        Debug.Log($"[EnemyManager] TargetEnemy called: {enemy?.EntityName ?? "NULL"}");
        
        if (enemy == null)
        {
            Debug.LogError("[EnemyManager] Cannot target NULL enemy");
            return;
        }
        
        if (!enemy.IsValidTarget())
        {
            Debug.LogWarning($"[EnemyManager] Cannot target invalid enemy: {enemy.EntityName}");
            return;
        }
        
        Debug.Log($"[EnemyManager] Targeting settings - Multi: {allowMultiTarget}, Max: {maxTargets}");
        Debug.Log($"[EnemyManager] Current targets before change: {_targetedEnemies.Count}");
        
        if (!allowMultiTarget)
        {
            Debug.Log("[EnemyManager] Single target mode - clearing all targets");
            ClearAllTargets();
        }
        else if (_targetedEnemies.Count >= maxTargets)
        {
            Debug.Log($"[EnemyManager] Max targets reached - removing oldest");
            UntargetEnemy(_targetedEnemies[0]);
        }
        
        _targetedEnemies.Add(enemy);
        enemy.SetTargeted(true);
        
        Debug.Log($"[EnemyManager] Enemy {enemy.EntityName} successfully targeted");
        Debug.Log($"[EnemyManager] Total targets now: {_targetedEnemies.Count}");
        
        // FIX 11: Fire events in correct order
        OnEnemyTargeted?.Invoke(enemy);
        OnTargetsChanged?.Invoke(_targetedEnemies.ToList());
    }
    
    public void UntargetEnemy(EntityBehaviour enemy)
    {
        Debug.Log($"[EnemyManager] UntargetEnemy called: {enemy?.EntityName ?? "NULL"}");
        
        if (enemy == null)
        {
            Debug.LogWarning("[EnemyManager] Cannot untarget NULL enemy");
            return;
        }
        
        bool wasTargeted = _targetedEnemies.Contains(enemy);
        Debug.Log($"[EnemyManager] Enemy {enemy.EntityName} was targeted: {wasTargeted}");
        
        if (_targetedEnemies.Remove(enemy))
        {
            enemy.SetTargeted(false);
            
            Debug.Log($"[EnemyManager] Enemy {enemy.EntityName} untargeted");
            Debug.Log($"[EnemyManager] Remaining targets: {_targetedEnemies.Count}");
            
            OnEnemyUntargeted?.Invoke(enemy);
            OnTargetsChanged?.Invoke(_targetedEnemies.ToList());
        }
    }
    
    public void ClearAllTargets()
    {
        Debug.Log($"[EnemyManager] Clearing all targets (Count: {_targetedEnemies.Count})");
        
        var targets = _targetedEnemies.ToList();
        foreach (var enemy in targets)
        {
            if (enemy != null)
            {
                enemy.SetTargeted(false);
                OnEnemyUntargeted?.Invoke(enemy);
            }
        }
        
        _targetedEnemies.Clear();
        OnTargetsChanged?.Invoke(new List<EntityBehaviour>());
        
        Debug.Log("[EnemyManager] All targets cleared");
    }
    
    // FIX 12: Enhanced damage dealing with better event handling
    public void DamageAllEnemies(int damage)
    {
        Debug.Log($"[EnemyManager] Dealing {damage} damage to all enemies");
        
        int enemiesDamaged = 0;
        foreach (var enemy in AliveEnemies.ToList())
        {
            if (enemy != null && enemy.IsValidTarget())
            {
                var result = enemy.TryDamageWithEffects(damage, DamageType.Normal, true);
                if (result.Success)
                {
                    enemiesDamaged++;
                    OnEnemyDamaged?.Invoke(enemy, result.DamageDealt);
                }
            }
        }
        
        Debug.Log($"[EnemyManager] Damaged {enemiesDamaged} enemies");
    }
    
    public void DamageTargetedEnemies(int damage)
    {
        Debug.Log($"[EnemyManager] Dealing {damage} damage to {_targetedEnemies.Count} targeted enemies");
        
        int enemiesDamaged = 0;
        foreach (var enemy in _targetedEnemies.ToList())
        {
            if (enemy != null && enemy.IsValidTarget())
            {
                var result = enemy.TryDamageWithEffects(damage, DamageType.Normal, true);
                if (result.Success)
                {
                    enemiesDamaged++;
                    OnEnemyDamaged?.Invoke(enemy, result.DamageDealt);
                    Debug.Log($"[EnemyManager] Damaged {enemy.EntityName} for {result.DamageDealt}");
                }
            }
        }
        
        Debug.Log($"[EnemyManager] Successfully damaged {enemiesDamaged} targeted enemies");
    }
    
    public void DamageRandomEnemy(int damage)
    {
        var randomEnemy = AliveEnemies.GetRandom();
        if (randomEnemy != null)
        {
            var result = randomEnemy.TryDamageWithEffects(damage, DamageType.Normal, true);
            if (result.Success)
            {
                OnEnemyDamaged?.Invoke(randomEnemy, result.DamageDealt);
                Debug.Log($"[EnemyManager] Damaged random enemy {randomEnemy.EntityName} for {result.DamageDealt}");
            }
        }
        else
        {
            Debug.LogWarning("[EnemyManager] No valid random enemy to damage");
        }
    }
    
    // Smart targeting using EntityExtensions
    public EntityBehaviour GetSmartTarget(TargetingStrategy strategy = TargetingStrategy.Optimal)
    {
        if (!HasEnemies) return null;
        
        return strategy switch
        {
            TargetingStrategy.Weakest => AliveEnemies.GetWeakest(),
            TargetingStrategy.Strongest => AliveEnemies.GetStrongest(),
            TargetingStrategy.Nearest => AliveEnemies.GetNearestTo(Vector3.zero),
            TargetingStrategy.Priority => AliveEnemies.GetHighestPriority(),
            TargetingStrategy.Random => AliveEnemies.GetRandom(),
            _ => GetOptimalTarget()
        };
    }
    
    public List<EntityBehaviour> GetOptimalTargets(int maxTargets = 3, TargetingStrategy strategy = TargetingStrategy.Optimal)
    {
        if (!HasEnemies) return new List<EntityBehaviour>();
        
        var enemies = AliveEnemies;
        
        return strategy switch
        {
            TargetingStrategy.Weakest => enemies.OrderBy(e => e.CurrentHealth).Take(maxTargets).ToList(),
            TargetingStrategy.Strongest => enemies.OrderByDescending(e => e.CurrentHealth).Take(maxTargets).ToList(),
            TargetingStrategy.Priority => enemies.OrderByDescending(e => e.TargetPriority).Take(maxTargets).ToList(),
            // FIXED: UnityRandom instead of System.Guid
            TargetingStrategy.Random => enemies.OrderBy(x => UnityRandom.value).Take(maxTargets).ToList(),
            _ => GetOptimalTargetGroup(enemies, maxTargets)
        };
    }
    
    private EntityBehaviour GetOptimalTarget()
    {
        var criteria = new TargetingCriteria
        {
            PreferLowHealth = true,
            PreferClose = true,
            ReferencePosition = Vector3.zero,
            HealthWeight = 1.5f,
            DistanceWeight = 0.5f,
            PriorityWeight = 0.8f
        };
        
        return AliveEnemies.GetBestTarget(criteria);
    }
    
    private List<EntityBehaviour> GetOptimalTargetGroup(IReadOnlyList<EntityBehaviour> enemies, int maxTargets)
    {
        return enemies.OrderBy(e => e.HealthPercentage).Take(maxTargets).ToList();
    }
    
    // FIX 13: Enhanced event handlers with better validation
    private void HandleEntityHealthChanged(EntityBehaviour entity, int oldHealth, int newHealth)
    {
        if (entity == null || !entity.IsEnemy()) return;
        
        Debug.Log($"[EnemyManager] Enemy health changed: {entity.EntityName} {oldHealth} → {newHealth}");
        
        int damage = oldHealth - newHealth;
        if (damage > 0)
        {
            OnEnemyDamaged?.Invoke(entity, damage);
        }
        
        if (newHealth <= 0 && oldHealth > 0)
        {
            Debug.Log($"[EnemyManager] Enemy killed: {entity.EntityName}");
            OnEnemyKilled?.Invoke(entity);
            
            // Remove from targeting
            if (_targetedEnemies.Remove(entity))
            {
                OnTargetsChanged?.Invoke(_targetedEnemies.ToList());
                Debug.Log($"[EnemyManager] Removed killed enemy from targets");
            }
            
            CheckAllEnemiesDefeated();
        }
    }
    
    private void HandleEntityDestroyed(EntityBehaviour entity)
    {
        if (entity == null || !entity.IsEnemy()) return;
        
        Debug.Log($"[EnemyManager] Enemy destroyed: {entity.EntityName}");
        UnregisterEnemy(entity);
    }
    
    private void CheckAllEnemiesDefeated()
    {
        bool hasAliveEnemies = HasEnemies;
        Debug.Log($"[EnemyManager] Checking if all enemies defeated - Has alive enemies: {hasAliveEnemies}");
        
        if (!hasAliveEnemies)
        {
            Debug.Log("[EnemyManager] All enemies defeated!");
            OnAllEnemiesDefeated?.Invoke();
        }
    }
    
    // Utility methods using EntityExtensions
    public List<EntityBehaviour> GetLowHealthEnemies(float threshold = 0.3f)
        => AliveEnemies.FilterByHealth(threshold, HealthComparison.Below).ToList();
    
    public List<EntityBehaviour> GetCriticalEnemies()
        => AliveEnemies.Where(e => e.IsCriticalHealth()).ToList();
    
    public ThreatAssessment GetThreatAssessment()
    {
        var assessment = new ThreatAssessment();
        
        assessment.TotalEnemies = AliveEnemyCount;
        assessment.BossCount = AliveEnemies.Count(e => e.IsBoss());
        assessment.EliteCount = AliveEnemies.Count(e => e.IsElite());
        assessment.CriticalEnemies = GetCriticalEnemies().Count;
        
        if (AliveEnemies.Count > 0)
        {
            assessment.AverageHealth = AliveEnemies.Average(e => e.HealthPercentage());
            assessment.LowestHealth = AliveEnemies.Min(e => e.HealthPercentage());
            assessment.HighestHealth = AliveEnemies.Max(e => e.HealthPercentage());
            
            float threatScore = assessment.TotalEnemies * 1f +
                              assessment.EliteCount * 2f +
                              assessment.BossCount * 5f;
            
            assessment.ThreatLevel = threatScore switch
            {
                >= 10f => ThreatLevel.Extreme,
                >= 7f => ThreatLevel.High,
                >= 4f => ThreatLevel.Medium,
                >= 1f => ThreatLevel.Low,
                _ => ThreatLevel.None
            };
        }
        else
        {
            assessment.ThreatLevel = ThreatLevel.None;
        }
        
        return assessment;
    }
    
    public void DespawnAllEnemies()
    {
        Debug.Log($"[EnemyManager] Despawning all enemies (Count: {_enemies.Count})");
        
        var enemies = _enemies.Values.ToList();
        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.gameObject != null)
                Destroy(enemy.gameObject);
        }
        
        _enemies.Clear();
        _targetedEnemies.Clear();
        
        Debug.Log("[EnemyManager] All enemies despawned");
    }

    // FIX 14: Enhanced debug and validation methods
    #if UNITY_EDITOR
    [ContextMenu("DEBUG: Log Enemy Status")]
    public void LogEnemyStatus()
    {
        Debug.Log("=== ENEMY MANAGER STATUS ===");
        Debug.Log($"Total Enemies: {EnemyCount}");
        Debug.Log($"Alive Enemies: {AliveEnemyCount}");
        Debug.Log($"Targeted: {_targetedEnemies.Count}");
        Debug.Log($"Is Ready: {IsReady}");
        Debug.Log($"Events Setup: {_eventsSetup}");
        
        Debug.Log("--- ENEMY DETAILS ---");
        foreach (var kvp in _enemies)
        {
            var enemy = kvp.Value;
            if (enemy != null)
            {
                var healthStatus = enemy.GetHealthStatus();
                Debug.Log($"  ID {kvp.Key}: {enemy.EntityName}");
                Debug.Log($"    - Health: {enemy.CurrentHealth}/{enemy.MaxHealth} ({healthStatus})");
                Debug.Log($"    - IsAlive: {enemy.IsAlive}");
                Debug.Log($"    - IsTargetable: {enemy.IsTargetable}");
                Debug.Log($"    - IsTargeted: {enemy.IsTargeted()}");
                Debug.Log($"    - Position: {enemy.transform.position}");
                
                // Component validation
                var collider = enemy.GetComponent<Collider>();
                Debug.Log($"    - Has Collider: {collider != null} (enabled: {collider?.enabled})");
                Debug.Log($"    - Has Pointer Handlers: {enemy is UnityEngine.EventSystems.IPointerClickHandler}");
            }
            else
            {
                Debug.LogError($"  ID {kvp.Key}: NULL ENEMY!");
            }
        }
        
        Debug.Log("--- TARGETED ENEMIES ---");
        for (int i = 0; i < _targetedEnemies.Count; i++)
        {
            var enemy = _targetedEnemies[i];
            Debug.Log($"  {i}: {enemy?.EntityName ?? "NULL"}");
        }
    }
    
    [ContextMenu("DEBUG: Test First Enemy Targeting")]
    public void TestFirstEnemyTargeting()
    {
        var firstEnemy = AliveEnemies.FirstOrDefault();
        if (firstEnemy != null)
        {
            Debug.Log($"Testing targeting with first alive enemy: {firstEnemy.EntityName}");
            HandleEntityClicked(firstEnemy);
        }
        else
        {
            Debug.LogWarning("No alive enemies to test with");
        }
    }
    
    [ContextMenu("DEBUG: Force Enemy Event")]
    public void ForceEnemyEvent()
    {
        var firstEnemy = AliveEnemies.FirstOrDefault();
        if (firstEnemy != null)
        {
            Debug.Log($"Force firing OnEnemyTargeted for: {firstEnemy.EntityName}");
            OnEnemyTargeted?.Invoke(firstEnemy);
        }
        else
        {
            Debug.LogWarning("No enemies available for event test");
        }
    }
    
    [ContextMenu("DEBUG: Validate All Enemies")]
    public void ValidateAllEnemies()
    {
        Debug.Log("=== VALIDATING ALL ENEMIES ===");
        
        int validEnemies = 0;
        int invalidEnemies = 0;
        
        foreach (var kvp in _enemies)
        {
            var enemy = kvp.Value;
            if (enemy == null)
            {
                Debug.LogError($"Enemy ID {kvp.Key}: NULL REFERENCE");
                invalidEnemies++;
                continue;
            }
            
            Debug.Log($"Validating Enemy ID {kvp.Key}: {enemy.EntityName}");
            
            var issues = new List<string>();
            
            // Basic validation
            if (!enemy.IsEnemy()) issues.Add("Not Enemy type");
            if (!enemy.IsTargetable) issues.Add("Not targetable");
            if (!enemy.gameObject.activeInHierarchy) issues.Add("GameObject inactive");
            
            // Component validation
            var collider = enemy.GetComponent<Collider>();
            if (collider == null) issues.Add("Missing Collider");
            else if (!collider.enabled) issues.Add("Collider disabled");
            
            // Interface validation
            if (!(enemy is UnityEngine.EventSystems.IPointerClickHandler)) issues.Add("Missing IPointerClickHandler");
            if (!(enemy is UnityEngine.EventSystems.IPointerEnterHandler)) issues.Add("Missing IPointerEnterHandler");
            if (!(enemy is UnityEngine.EventSystems.IPointerExitHandler)) issues.Add("Missing IPointerExitHandler");
            
            // Asset validation
            if (enemy.Asset == null) issues.Add("Missing EntityAsset");
            else if (enemy.Asset.Type != EntityType.Enemy) issues.Add("Asset wrong type");
            
            if (issues.Count > 0)
            {
                Debug.LogWarning($"  ⚠️ Issues found:");
                foreach (var issue in issues)
                    Debug.LogWarning($"    - {issue}");
                invalidEnemies++;
            }
            else
            {
                Debug.Log($"  ✅ Enemy validation passed");
                validEnemies++;
            }
        }
        
        Debug.Log($"=== VALIDATION COMPLETE ===");
        Debug.Log($"Valid Enemies: {validEnemies}");
        Debug.Log($"Invalid Enemies: {invalidEnemies}");
    }
    
    [ContextMenu("DEBUG: Get Threat Assessment")]
    public void LogThreatAssessment()
    {
        var assessment = GetThreatAssessment();
        Debug.Log("=== THREAT ASSESSMENT ===");
        Debug.Log($"Threat Level: {assessment.ThreatLevel}");
        Debug.Log($"Total Enemies: {assessment.TotalEnemies}");
        Debug.Log($"Bosses: {assessment.BossCount}");
        Debug.Log($"Elites: {assessment.EliteCount}");
        Debug.Log($"Critical: {assessment.CriticalEnemies}");
        Debug.Log($"Average Health: {assessment.AverageHealth:P0}");
    }
    
    [ContextMenu("DEBUG: Force Health Update")]
    public void ForceHealthUpdate()
    {
        Debug.Log("Forcing health update for all enemies...");
        
        foreach (var enemy in AliveEnemies)
        {
            if (enemy != null)
            {
                // Trigger a small fake health change to force events
                int currentHealth = enemy.CurrentHealth;
                HandleEntityHealthChanged(enemy, currentHealth, currentHealth);
                Debug.Log($"Triggered health update for {enemy.EntityName}: {currentHealth}/{enemy.MaxHealth}");
            }
        }
    }
    #endif
}

[System.Serializable]
public class ThreatAssessment
{
    public int TotalEnemies;
    public int BossCount;
    public int EliteCount;
    public int CriticalEnemies;
    public float AverageHealth;
    public float LowestHealth;
    public float HighestHealth;
    public ThreatLevel ThreatLevel;
}