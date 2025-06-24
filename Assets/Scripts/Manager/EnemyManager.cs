using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using GameCore.Enums;
using GameCore.Data;

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
    
    // Properties - Simplified using EntityExtensions
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
        if (!ValidateSpawn(enemyAsset)) return null;
        
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
        
        if (enemyAsset.SpawnWithAnimation)
            StartCoroutine(SpawnAnimation(enemy, enemyAsset.SpawnDelay));
        
        return enemy;
    }
    
    public EntityBehaviour SpawnEnemyAtRandomPoint(EntityAsset enemyAsset)
    {
        if (spawnPoints.Count == 0)
            return SpawnEnemy(enemyAsset, Vector3.zero);
        
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
        return SpawnEnemy(enemyAsset, spawnPoint.position, spawnPoint.rotation);
    }
    
    private bool ValidateSpawn(EntityAsset enemyAsset)
    {
        if (enemyAsset == null || enemyAsset.Type != EntityType.Enemy)
        {
            Debug.LogError("[EnemyManager] Invalid enemy asset");
            return false;
        }
        
        if (_enemies.Count >= maxEnemies)
        {
            Debug.LogWarning($"[EnemyManager] Max enemy limit reached ({maxEnemies})");
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
    
    public void RegisterEnemy(EntityBehaviour enemy)
    {
        if (enemy == null || !enemy.IsEnemy()) return;
        
        int id = _nextEnemyId++;
        _enemies[id] = enemy;
        
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
            UntargetEnemy(enemy);
        else
            TargetEnemy(enemy);
    }
    
    public void TargetEnemy(EntityBehaviour enemy)
    {
        if (!enemy.IsValidTarget()) return;
        
        if (!allowMultiTarget)
            ClearAllTargets();
        else if (_targetedEnemies.Count >= maxTargets)
            UntargetEnemy(_targetedEnemies[0]);
        
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
            UntargetEnemy(enemy);
    }
    
    // Damage dealing - simplified using EntityExtensions
    public void DamageAllEnemies(int damage)
    {
        foreach (var enemy in AliveEnemies)
            enemy.TryDamageWithEffects(damage, DamageType.Normal, true);
    }
    
    public void DamageTargetedEnemies(int damage)
    {
        foreach (var enemy in _targetedEnemies.ToList())
        {
            if (enemy.IsValidTarget())
                enemy.TryDamageWithEffects(damage, DamageType.Normal, true);
        }
    }
    
    public void DamageRandomEnemy(int damage)
    {
        var randomEnemy = AliveEnemies.GetRandom();
        randomEnemy?.TryDamageWithEffects(damage, DamageType.Normal, true);
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
    
    // Event handlers
    private void HandleEntityHealthChanged(EntityBehaviour entity, int oldHealth, int newHealth)
    {
        if (!entity.IsEnemy()) return;
        
        int damage = oldHealth - newHealth;
        if (damage > 0)
            OnEnemyDamaged?.Invoke(entity, damage);
        
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
            OnAllEnemiesDefeated?.Invoke();
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
            assessment.AverageHealth = AliveEnemies.Average(e => e.HealthPercentage);
            assessment.LowestHealth = AliveEnemies.Min(e => e.HealthPercentage);
            assessment.HighestHealth = AliveEnemies.Max(e => e.HealthPercentage);
            
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