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
    
    // Properties
    public int EnemyCount => _enemies.Count;
    public int AliveEnemyCount => _enemies.Values.Count(e => e != null && e.IsAlive);
    public bool HasEnemies => AliveEnemyCount > 0;
    public IReadOnlyList<EntityBehaviour> AllEnemies => _enemies.Values.Where(e => e != null).ToList();
    public IReadOnlyList<EntityBehaviour> AliveEnemies => _enemies.Values.Where(e => e != null && e.IsAlive).ToList();
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
        if (enemy == null || enemy.Type != EntityType.Enemy) return;
        
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
        if (enemy == null || !enemy.IsTargetable || !enemy.IsAlive) return;
        
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
        if (enemy == null || !enemy.IsTargetable || !enemy.IsAlive) return;
        
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
    
    // Damage dealing
    public void DamageAllEnemies(int damage)
    {
        foreach (var enemy in AliveEnemies)
        {
            enemy.Damage(damage);
        }
    }
    
    public void DamageTargetedEnemies(int damage)
    {
        foreach (var enemy in _targetedEnemies.ToList())
        {
            if (enemy != null && enemy.IsAlive)
                enemy.Damage(damage);
        }
    }
    
    public void DamageRandomEnemy(int damage)
    {
        var aliveEnemies = AliveEnemies;
        if (aliveEnemies.Count > 0)
        {
            var randomEnemy = aliveEnemies[Random.Range(0, aliveEnemies.Count)];
            randomEnemy.Damage(damage);
        }
    }
    
    // Event handlers
    private void HandleEntityHealthChanged(EntityBehaviour entity, int oldHealth, int newHealth)
    {
        if (entity == null || entity.Type != EntityType.Enemy) return;
        
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
        if (entity == null || entity.Type != EntityType.Enemy) return;
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
    
    // Utility methods
    public EntityBehaviour GetNearestEnemy(Vector3 position)
    {
        return AliveEnemies
            .OrderBy(e => Vector3.Distance(e.transform.position, position))
            .FirstOrDefault();
    }
    
    public List<EntityBehaviour> GetEnemiesInRadius(Vector3 center, float radius)
    {
        return AliveEnemies
            .Where(e => Vector3.Distance(e.transform.position, center) <= radius)
            .ToList();
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
            Debug.Log($"  - {enemy.EntityName}: {enemy.CurrentHealth}/{enemy.MaxHealth} HP");
        }
    }
#endif
}