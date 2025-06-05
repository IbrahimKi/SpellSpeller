using UnityEngine;
using System.Collections;

public class TestCombatController : MonoBehaviour
{
    [Header("Test Enemy Setup")]
    [SerializeField] private GameObject testEnemyPrefab;
    [SerializeField] private Vector3 spawnPosition = Vector3.zero;
    [SerializeField] private int testEnemyHealth = 50;
    [SerializeField] private bool autoSpawnOnStart = true;
    
    private EntityBehaviour spawnedEnemy;
    
    private void Start()
    {
        if (autoSpawnOnStart)
        {
            StartCoroutine(InitializeAndSpawn());
        }
    }
    
    private void OnEnable()
    {
        // Subscribe to combat events for logging
        if (CombatManager.HasInstance)
        {
            CombatManager.OnCombatStarted += OnCombatStarted;
        }
        
        if (EnemyManager.HasInstance)
        {
            EnemyManager.OnEnemySpawned += OnEnemySpawned;
            EnemyManager.OnEnemyKilled += OnEnemyKilled;
        }
    }
    
    private void OnDisable()
    {
        if (CombatManager.HasInstance)
        {
            CombatManager.OnCombatStarted -= OnCombatStarted;
        }
        
        if (EnemyManager.HasInstance)
        {
            EnemyManager.OnEnemySpawned -= OnEnemySpawned;
            EnemyManager.OnEnemyKilled -= OnEnemyKilled;
        }
    }
    
    private IEnumerator InitializeAndSpawn()
    {
        // Wait for managers
        yield return new WaitForSeconds(0.5f);
        
        // Start combat if needed
        if (CombatManager.HasInstance && !CombatManager.Instance.IsInCombat)
        {
            CombatManager.Instance.StartCombat();
            yield return new WaitForSeconds(0.2f);
        }
        
        // Spawn enemy
        SpawnTestEnemy();
    }
    
    private void SpawnTestEnemy()
    {
        if (!EnemyManager.HasInstance)
        {
            Debug.LogError("[TestCombat] EnemyManager not available");
            return;
        }
        
        var testAsset = CreateTestEnemyAsset();
        spawnedEnemy = EnemyManager.Instance.SpawnEnemy(testAsset, spawnPosition);
        
        if (spawnedEnemy != null)
        {
            Debug.Log($"[TestCombat] Test enemy spawned: {spawnedEnemy.EntityName}");
        }
    }
    
    private EntityAsset CreateTestEnemyAsset()
    {
        var asset = ScriptableObject.CreateInstance<EntityAsset>();
        
        // Use reflection for private fields
        var assetType = typeof(EntityAsset);
        assetType.GetField("entityName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(asset, "Test Enemy");
        assetType.GetField("entityType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(asset, EntityType.Enemy);
        assetType.GetField("baseHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(asset, testEnemyHealth);
        assetType.GetField("isTargetable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(asset, true);
        assetType.GetField("prefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(asset, testEnemyPrefab ?? CreateBasicEnemyPrefab());
        
        return asset;
    }
    
    private GameObject CreateBasicEnemyPrefab()
    {
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        prefab.name = "TestEnemyPrefab";
        prefab.GetComponent<Renderer>().material.color = Color.red;
        prefab.AddComponent<EntityBehaviour>();
        prefab.transform.position = Vector3.up;
        return prefab;
    }
    
    // Event handlers for logging
    private void OnCombatStarted()
    {
        Debug.Log("[TestCombat] Combat started");
    }
    
    private void OnEnemySpawned(EntityBehaviour enemy)
    {
        if (enemy == spawnedEnemy)
        {
            Debug.Log($"[TestCombat] Enemy registered: {enemy.EntityName} ({enemy.CurrentHealth}/{enemy.MaxHealth} HP)");
        }
    }
    
    private void OnEnemyKilled(EntityBehaviour enemy)
    {
        if (enemy == spawnedEnemy)
        {
            Debug.Log($"[TestCombat] Test enemy killed: {enemy.EntityName}");
            spawnedEnemy = null;
        }
    }
    
    // Manual controls
    [ContextMenu("Spawn Test Enemy")]
    public void ManualSpawnEnemy()
    {
        if (spawnedEnemy == null)
        {
            SpawnTestEnemy();
        }
        else
        {
            Debug.Log("[TestCombat] Test enemy already exists");
        }
    }
    
    [ContextMenu("Clear Test Enemy")]
    public void ClearTestEnemy()
    {
        if (spawnedEnemy != null)
        {
            if (spawnedEnemy.gameObject != null)
                DestroyImmediate(spawnedEnemy.gameObject);
            spawnedEnemy = null;
            Debug.Log("[TestCombat] Test enemy cleared");
        }
    }
}