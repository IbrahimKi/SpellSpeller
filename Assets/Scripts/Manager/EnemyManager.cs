using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public class EnemyManager : SingletonBehaviour<EnemyManager>, IGameManager
{
    [Header("Enemy Management")]
    [SerializeField] private int maxEnemies = 10;
    [SerializeField] private Transform enemyContainer;
    
    [Header("Targeting")]
    [SerializeField] private bool allowMultiTarget = false;
    [SerializeField] private int maxTargets = 1;
    [SerializeField] private Color targetedTintColor = new Color(1f, 0.8f, 0.8f);
    
    [Header("Spawn Settings")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
    [SerializeField] private float spawnAnimationDuration = 0.5f;
    
    // Enemy tracking
    private Dictionary<int, EntityBehaviour> _enemies = new Dictionary<int, EntityBehaviour>();
    private List<EntityBehaviour> _targetedEnemies = new List<EntityBehaviour>();
    private int _nextEnemyId = 0;
    
    // Manager state
    private bool _isReady = false;
    public bool IsReady => _isReady;
    
    // Events
    public static event System.Action<EntityBehaviour> OnEnemySpawned;
    public static event System.Action<EntityBehaviour> OnEnemyDespawned;
    public static event System.Action<EntityBehaviour> OnEnemyTargeted;
    public static event System.Action<EntityBehaviour> OnEnemyUntargeted;
    public static event System.Action<List<EntityBehaviour>> OnTargetsChanged;
    public static event System.Action<EntityBehaviour, int> OnEnemyDamaged;
    public static event System.Action<EntityBehaviour> OnEnemyKilled;
    public static event System.Action OnAllEnemiesDefeated;
    
    // Properties - INTEGRATION: Enhanced with EntityExtensions
    public int EnemyCount => _enemies.Count;
    public int AliveEnemyCount => _enemies.Values.Count(e => e.IsValidTarget());
    public bool HasEnemies => AliveEnemyCount > 0;
    public IReadOnlyList<EntityBehaviour> AllEnemies => _enemies.Values.Where(e => e != null).ToList();
    public IReadOnlyList<EntityBehaviour> AliveEnemies => _enemies.Values.Where(e => e.IsValidTarget()).ToList();
    public IReadOnlyList<EntityBehaviour> TargetedEnemies => _targetedEnemies.AsReadOnly();
    public EntityBehaviour PrimaryTarget => _targetedEnemies.FirstOrDefault();
    
    protected override void OnAwakeInitialize()
    {
        InitializeContainer();
        _isReady = true;
    }
    
    private void InitializeContainer()
    {
        if (enemyContainer == null)
        {
            GameObject container = new GameObject("Enemy Container");
            container.transform.SetParent(transform);
            enemyContainer = container.transform;
        }
    }
    
    private void OnEnable()
    {
        EntityBehaviour.OnEntityHealthChanged += HandleEntityHealthChanged;
        EntityBehaviour.OnEntityDestroyed += HandleEntityDestroyed;
    }
    
    private void OnDisable()
    {
        EntityBehaviour.OnEntityHealthChanged -= HandleEntityHealthChanged;
        EntityBehaviour.OnEntityDestroyed -= HandleEntityDestroyed;
    }
    
    // Enemy spawning
    public EntityBehaviour SpawnEnemy(EntityAsset enemyAsset, Vector3 position, Quaternion rotation = default)
    {
        if (enemyAsset == null || enemyAsset.Type != EntityType.Enemy)
        {
            Debug.LogError("[EnemyManager] Invalid enemy asset");
            return null;
        }
        
        if (_enemies.Count >= maxEnemies)
        {
            Debug.LogWarning($"[EnemyManager] Max enemy limit reached ({maxEnemies})");
            return null;
        }
        
        if (rotation == default)
            rotation = Quaternion.identity;
        
        GameObject enemyObject = enemyAsset.CreateInstance(position, rotation, enemyContainer);
        if (enemyObject == null) return null;
        
        EntityBehaviour enemy = enemyObject.GetComponent<EntityBehaviour>();
        if (enemy == null)
        {
            Destroy(enemyObject);
            return null;
        }
        
        // Apply spawn animation if needed
        if (enemyAsset.SpawnWithAnimation)
        {
            StartCoroutine(SpawnAnimation(enemy, enemyAsset.SpawnDelay));
        }
        
        return enemy;
    }
    
    public EntityBehaviour SpawnEnemyAtRandomPoint(EntityAsset enemyAsset)
    {
        if (spawnPoints.Count == 0)
        {
            Debug.LogWarning("[EnemyManager] No spawn points defined");
            return SpawnEnemy(enemyAsset, Vector3.zero);
        }
        
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
        return SpawnEnemy(enemyAsset, spawnPoint.position, spawnPoint.rotation);
    }
    
    private IEnumerator SpawnAnimation(EntityBehaviour enemy, float delay)
    {
        if (delay > 0)
            yield return new WaitForSeconds(delay);
        
        // Simple scale-in animation
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
    
    // Registration (called by EntityBehaviour)
    public void RegisterEnemy(EntityBehaviour enemy)
    {
        if (enemy == null || !enemy.IsEnemy()) return;
        
        int id = _nextEnemyId++;
        _enemies[id] = enemy;
        
        OnEnemySpawned?.Invoke(enemy);
        Debug.Log($"[EnemyManager] Enemy registered: {enemy.EntityName} (ID: {id})");
    }
    
    public void UnregisterEnemy(EntityBehaviour enemy)
    {
        if (enemy == null) return;
        
        var kvp = _enemies.FirstOrDefault(x => x.Value == enemy);
        if (kvp.Value != null)
        {
            _enemies.Remove(kvp.Key);
            _targetedEnemies.Remove(enemy);
            
            OnEnemyDespawned?.Invoke(enemy);
            
            if (_targetedEnemies.Count > 0)
                OnTargetsChanged?.Invoke(_targetedEnemies);
            
            CheckAllEnemiesDefeated();
        }
    }
    
    // Targeting
    public void HandleEntityClicked(EntityBehaviour enemy)
    {
        if (!enemy.IsValidTarget()) return;
        
        if (_targetedEnemies.Contains(enemy))
        {
            // Already targeted - untarget
            UntargetEnemy(enemy);
        }
        else
        {
            // Not targeted - target
            TargetEnemy(enemy);
        }
    }
    
    public void TargetEnemy(EntityBehaviour enemy)
    {
        if (!enemy.IsValidTarget()) return;
        
        if (!allowMultiTarget)
        {
            // Clear existing targets
            ClearAllTargets();
        }
        else if (_targetedEnemies.Count >= maxTargets)
        {
            // Remove oldest target
            UntargetEnemy(_targetedEnemies[0]);
        }
        
        _targetedEnemies.Add(enemy);
        enemy.SetTargeted(true);
        
        OnEnemyTargeted?.Invoke(enemy);
        OnTargetsChanged?.Invoke(_targetedEnemies);
    }
    
    public void UntargetEnemy(EntityBehaviour enemy)
    {
        if (enemy == null) return;
        
        if (_targetedEnemies.Remove(enemy))
        {
            enemy.SetTargeted(false);
            OnEnemyUntargeted?.Invoke(enemy);
            OnTargetsChanged?.Invoke(_targetedEnemies);
        }
    }
    
    public void ClearAllTargets()
    {
        var targets = _targetedEnemies.ToList();
        foreach (var enemy in targets)
        {
            UntargetEnemy(enemy);
        }
    }
    
    // INTEGRATION: Enhanced damage dealing with EntityExtensions
    public void DamageAllEnemies(int damage)
    {
        foreach (var enemy in AliveEnemies)
        {
            var result = enemy.TryDamageWithEffects(damage, DamageType.Normal, true);
            if (result.Success)
            {
                Debug.Log($"[EnemyManager] Dealt {result.DamageDealt} damage to {enemy.EntityName}");
            }
        }
    }
    
    public void DamageTargetedEnemies(int damage)
    {
        foreach (var enemy in _targetedEnemies.ToList())
        {
            if (enemy.IsValidTarget())
            {
                var result = enemy.TryDamageWithEffects(damage, DamageType.Normal, true);
                if (result.Success)
                {
                    Debug.Log($"[EnemyManager] Dealt {result.DamageDealt} damage to targeted enemy {enemy.EntityName}");
                }
            }
        }
    }
    
    public void DamageRandomEnemy(int damage)
    {
        var randomEnemy = AliveEnemies.GetRandom();
        if (randomEnemy != null)
        {
            var result = randomEnemy.TryDamageWithEffects(damage, DamageType.Normal, true);
            if (result.Success)
            {
                Debug.Log($"[EnemyManager] Dealt {result.DamageDealt} damage to random enemy {randomEnemy.EntityName}");
            }
        }
    }
    
    // INTEGRATION: Smart targeting using EntityExtensions
    public EntityBehaviour GetSmartTarget(TargetingStrategy strategy = TargetingStrategy.Optimal)
    {
        if (!HasEnemies) return null;
        
        return strategy switch
        {
            TargetingStrategy.Weakest => AliveEnemies.GetWeakest(),
            TargetingStrategy.Strongest => AliveEnemies.GetStrongest(),
            TargetingStrategy.Nearest => GetNearestEnemyToPlayer(),
            TargetingStrategy.Priority => AliveEnemies.GetHighestPriority(),
            TargetingStrategy.Random => AliveEnemies.GetRandom(),
            _ => GetOptimalTarget()
        };
    }
    
    private EntityBehaviour GetNearestEnemyToPlayer()
    {
        // Assume player is at origin or get from CombatManager
        Vector3 playerPosition = Vector3.zero;
        return AliveEnemies.GetNearestTo(playerPosition);
    }
    
    private EntityBehaviour GetOptimalTarget()
    {
        // Create smart targeting criteria
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
    
    // Event handlers
    private void HandleEntityHealthChanged(EntityBehaviour entity, int oldHealth, int newHealth)
    {
        if (!entity.IsEnemy()) return;
        
        int damage = oldHealth - newHealth;
        if (damage > 0)
        {
            OnEnemyDamaged?.Invoke(entity, damage);
        }
        
        if (newHealth <= 0)
        {
            OnEnemyKilled?.Invoke(entity);
            _targetedEnemies.Remove(entity);
            CheckAllEnemiesDefeated();
        }
    }
    
    private void HandleEntityDestroyed(EntityBehaviour entity)
    {
        if (!entity.IsEnemy()) return;
        UnregisterEnemy(entity);
    }
    
    private void CheckAllEnemiesDefeated()
    {
        if (!HasEnemies)
        {
            OnAllEnemiesDefeated?.Invoke();
            Debug.Log("[EnemyManager] All enemies defeated!");
        }
    }
    
    // INTEGRATION: Enhanced utility methods using EntityExtensions
    public EntityBehaviour GetNearestEnemy(Vector3 position)
    {
        return AliveEnemies.GetNearestTo(position);
    }
    
    public List<EntityBehaviour> GetEnemiesInRadius(Vector3 center, float radius)
    {
        return AliveEnemies.GetInRadius(center, radius).ToList();
    }
    
    // INTEGRATION: Advanced enemy queries using EntityExtensions
    public List<EntityBehaviour> GetLowHealthEnemies(float threshold = 0.3f)
    {
        return AliveEnemies.FilterByHealth(threshold, HealthComparison.Below).ToList();
    }
    
    public List<EntityBehaviour> GetCriticalEnemies()
    {
        return AliveEnemies.Where(e => e.IsCriticalHealth()).ToList();
    }
    
    public List<EntityBehaviour> GetEliteEnemies()
    {
        return AliveEnemies.Where(e => e.IsElite()).ToList();
    }
    
    public List<EntityBehaviour> GetBossEnemies()
    {
        return AliveEnemies.Where(e => e.IsBoss()).ToList();
    }
    
    // INTEGRATION: Threat assessment using EntityExtensions
    public ThreatAssessment GetThreatAssessment()
    {
        var assessment = new ThreatAssessment();
        
        assessment.TotalEnemies = AliveEnemyCount;
        assessment.BossCount = GetBossEnemies().Count;
        assessment.EliteCount = GetEliteEnemies().Count;
        assessment.CriticalEnemies = GetCriticalEnemies().Count;
        
        if (AliveEnemies.Count > 0)
        {
            assessment.AverageHealth = AliveEnemies.Average(e => e.HealthPercentage);
            assessment.LowestHealth = AliveEnemies.Min(e => e.HealthPercentage);
            assessment.HighestHealth = AliveEnemies.Max(e => e.HealthPercentage);
            
            // Calculate threat level
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
        var enemies = _enemies.Values.ToList();
        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.gameObject != null)
                Destroy(enemy.gameObject);
        }
        
        _enemies.Clear();
        _targetedEnemies.Clear();
    }
    
    // INTEGRATION: Smart multi-targeting using EntityExtensions
    public List<EntityBehaviour> GetOptimalTargets(int count, TargetingStrategy strategy = TargetingStrategy.Optimal)
    {
        if (!HasEnemies || count <= 0) return new List<EntityBehaviour>();
        
        return strategy switch
        {
            TargetingStrategy.Weakest => AliveEnemies.SortBy(EntitySortCriteria.HealthPercentage).Take(count).ToList(),
            TargetingStrategy.Strongest => AliveEnemies.SortBy(EntitySortCriteria.Health, false).Take(count).ToList(),
            TargetingStrategy.Priority => AliveEnemies.SortBy(EntitySortCriteria.TargetPriority, false).Take(count).ToList(),
            TargetingStrategy.Random => AliveEnemies.GetRandom(count).ToList(),
            _ => GetOptimalTargetGroup(count)
        };
    }
    
    private List<EntityBehaviour> GetOptimalTargetGroup(int count)
    {
        // Balanced approach: mix of weak and high-priority targets
        var weakTargets = AliveEnemies.GetWeakest();
        var priorityTargets = AliveEnemies.GetHighestPriority();
        var targets = new HashSet<EntityBehaviour>();
        
        if (weakTargets != null) targets.Add(weakTargets);
        if (priorityTargets != null && priorityTargets != weakTargets) targets.Add(priorityTargets);
        
        // Fill remaining slots with sorted enemies
        var remaining = AliveEnemies
            .Where(e => !targets.Contains(e))
            .SortBy(EntitySortCriteria.HealthPercentage)
            .Take(count - targets.Count);
        
        foreach (var enemy in remaining)
            targets.Add(enemy);
        
        return targets.Take(count).ToList();
    }

#if UNITY_EDITOR
    [ContextMenu("Log Enemy Status")]
    public void LogEnemyStatus()
    {
        Debug.Log($"[EnemyManager] Status:");
        Debug.Log($"  Total Enemies: {EnemyCount}");
        Debug.Log($"  Alive Enemies: {AliveEnemyCount}");
        Debug.Log($"  Targeted: {_targetedEnemies.Count}");
        
        foreach (var enemy in AllEnemies)
        {
            var healthStatus = enemy.GetHealthStatus();
            Debug.Log($"  - {enemy.EntityName}: {enemy.CurrentHealth}/{enemy.MaxHealth} HP ({healthStatus})");
        }
    }
    
    [ContextMenu("Get Threat Assessment")]
    public void LogThreatAssessment()
    {
        var assessment = GetThreatAssessment();
        Debug.Log($"[EnemyManager] Threat Assessment:");
        Debug.Log($"  Threat Level: {assessment.ThreatLevel}");
        Debug.Log($"  Total Enemies: {assessment.TotalEnemies}");
        Debug.Log($"  Bosses: {assessment.BossCount}");
        Debug.Log($"  Elites: {assessment.EliteCount}");
        Debug.Log($"  Critical: {assessment.CriticalEnemies}");
        Debug.Log($"  Average Health: {assessment.AverageHealth:P0}");
    }
    
    [ContextMenu("Target Weakest Enemy")]
    public void DebugTargetWeakest()
    {
        var weakest = GetSmartTarget(TargetingStrategy.Weakest);
        if (weakest != null)
        {
            TargetEnemy(weakest);
            Debug.Log($"[EnemyManager] Targeted weakest: {weakest.EntityName} ({weakest.HealthPercentage:P0} HP)");
        }
    }
    
    [ContextMenu("Damage All Critical Enemies")]
    public void DebugDamageCritical()
    {
        var criticalEnemies = GetCriticalEnemies();
        foreach (var enemy in criticalEnemies)
        {
            enemy.TryDamageWithEffects(10, DamageType.True, true);
        }
        Debug.Log($"[EnemyManager] Damaged {criticalEnemies.Count} critical enemies");
    }
#endif
}

// INTEGRATION: Supporting class for threat assessment
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