using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Entity", menuName = "Entity System/Entity Asset")]
public class EntityAsset : ScriptableObject
{
    [Header("Basic Information")]
    [SerializeField] private string entityName = "New Entity";
    [SerializeField] private EntityType entityType = EntityType.Enemy;
    [SerializeField] private Sprite entityIcon;
    
    [Header("Prefab")]
    [SerializeField] private GameObject prefab;
    
    [Header("Base Stats")]
    [SerializeField] private int baseHealth = 100;
    [SerializeField] private int baseArmor = 0;
    
    [Header("Visual Settings")]
    [SerializeField] private Color tintColor = Color.white;
    [SerializeField] private float scale = 1f;
    
    [Header("Targeting")]
    [SerializeField] private bool isTargetable = true;
    [SerializeField] private float targetPriority = 1f;
    [SerializeField] private Vector3 targetOffset = Vector3.up;
    
    [Header("Spawn Settings")]
    [SerializeField] private bool spawnWithAnimation = true;
    [SerializeField] private float spawnDelay = 0f;
    
    [Header("Tags & Categories")]
    [SerializeField] private List<string> tags = new List<string>();
    [SerializeField] private EntityCategory category = EntityCategory.Standard;
    
    // Properties
    public string EntityName => entityName;
    public EntityType Type => entityType;
    public Sprite Icon => entityIcon;
    public GameObject Prefab => prefab;
    public int BaseHealth => baseHealth;
    public int BaseArmor => baseArmor;
    public Color TintColor => tintColor;
    public float Scale => scale;
    public bool IsTargetable => isTargetable;
    public float TargetPriority => targetPriority;
    public Vector3 TargetOffset => targetOffset;
    public bool SpawnWithAnimation => spawnWithAnimation;
    public float SpawnDelay => spawnDelay;
    public IReadOnlyList<string> Tags => tags.AsReadOnly();
    public EntityCategory Category => category;
    
    // Validation
    public bool IsValid => !string.IsNullOrEmpty(entityName) && prefab != null && baseHealth > 0;
    
    // Tag utilities
    public bool HasTag(string tag) => tags.Contains(tag);
    public bool HasAnyTag(params string[] checkTags) => System.Array.Exists(checkTags, tag => HasTag(tag));
    public bool HasAllTags(params string[] checkTags) => System.Array.TrueForAll(checkTags, tag => HasTag(tag));
    
    // Factory method for creating entity instances
    public GameObject CreateInstance(Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (!IsValid)
        {
            Debug.LogError($"[EntityAsset] Cannot create instance of invalid entity: {entityName}");
            return null;
        }
        
        GameObject instance = Instantiate(prefab, position, rotation, parent);
        instance.name = $"{entityName}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
        
        // Apply visual settings
        if (scale != 1f)
            instance.transform.localScale = Vector3.one * scale;
        
        // Setup entity component
        var entityComponent = instance.GetComponent<EntityBehaviour>();
        if (entityComponent == null)
            entityComponent = instance.AddComponent<EntityBehaviour>();
        
        entityComponent.Initialize(this);
        
        return instance;
    }
    
    // Editor validation
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(entityName))
            entityName = name;
        
        baseHealth = Mathf.Max(1, baseHealth);
        baseArmor = Mathf.Max(0, baseArmor);
        scale = Mathf.Max(0.1f, scale);
        targetPriority = Mathf.Max(0f, targetPriority);
        spawnDelay = Mathf.Max(0f, spawnDelay);
    }
}

