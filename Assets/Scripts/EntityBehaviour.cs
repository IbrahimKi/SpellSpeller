using UnityEngine;
using UnityEngine.EventSystems;

public class EntityBehaviour : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Runtime Data")]
    [SerializeField] private EntityAsset entityAsset;
    [SerializeField] private int currentHealth;
    [SerializeField] private int maxHealth;
    [SerializeField] private bool isTargeted = false;
    [SerializeField] private bool isHovered = false;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject selectionIndicator;
    [SerializeField] private GameObject hoverIndicator;
    [SerializeField] private Renderer[] renderers;
    
    // Cached components
    private Collider targetCollider;
    private Canvas worldCanvas;
    private Material[] originalMaterials; // Store originals for cleanup
    
    // Events
    public static event System.Action<EntityBehaviour> OnEntityTargeted;
    public static event System.Action<EntityBehaviour> OnEntityUntargeted;
    public static event System.Action<EntityBehaviour> OnEntityHovered;
    public static event System.Action<EntityBehaviour> OnEntityUnhovered;
    public static event System.Action<EntityBehaviour> OnEntityDestroyed;
    public static event System.Action<EntityBehaviour, int, int> OnEntityHealthChanged;
    
    // Properties
    public EntityAsset Asset => entityAsset;
    public EntityType Type => entityAsset?.Type ?? EntityType.Enemy;
    public string EntityName => entityAsset?.EntityName ?? "Unknown";
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public float HealthPercentage => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
    public bool IsAlive => currentHealth > 0;
    public bool IsTargeted => isTargeted;
    public bool IsTargetable => entityAsset?.IsTargetable ?? false;
    public float TargetPriority => entityAsset?.TargetPriority ?? 1f;
    public Vector3 TargetPosition => transform.position + (entityAsset?.TargetOffset ?? Vector3.up);
    
    private void Awake()
    {
        CacheComponents();
    }
    
    private void CacheComponents()
    {
        targetCollider = GetComponent<Collider>();
        if (targetCollider == null)
        {
            // Add default collider if none exists
            var boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.size = Vector3.one * 2f;
            boxCollider.center = Vector3.up;
            targetCollider = boxCollider;
        }
        
        // Find existing world canvas (don't create one automatically)
        worldCanvas = GetComponentInChildren<Canvas>();
        
        // Only setup if canvas already exists in prefab
        if (worldCanvas != null && worldCanvas.renderMode == RenderMode.WorldSpace)
        {
            worldCanvas.transform.localScale = Vector3.one * 0.01f;
            worldCanvas.sortingOrder = 10; // Ensure proper sorting
        }
        
        // Cache renderers
        renderers = GetComponentsInChildren<Renderer>();
        
        // Store original materials for cleanup
        if (renderers.Length > 0)
        {
            originalMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalMaterials[i] = renderers[i].sharedMaterial;
            }
        }
    }
    
    public void Initialize(EntityAsset asset)
    {
        if (asset == null)
        {
            Debug.LogError("[EntityBehaviour] Cannot initialize with null asset");
            return;
        }
        
        entityAsset = asset;
        maxHealth = asset.BaseHealth;
        currentHealth = maxHealth;
        
        // Apply tint color
        if (asset.TintColor != Color.white)
        {
            ApplyTintColor(asset.TintColor);
        }
        
        // Setup targeting
        if (targetCollider != null)
        {
            targetCollider.enabled = asset.IsTargetable;
        }
        
        // Register with appropriate manager
        RegisterWithManager();
    }
    
    private void RegisterWithManager()
    {
        switch (Type)
        {
            case EntityType.Enemy:
                if (EnemyManager.HasInstance)
                    EnemyManager.Instance.RegisterEnemy(this);
                break;
            case EntityType.Unit:
                if (UnitManager.HasInstance)
                    UnitManager.Instance.RegisterUnit(this);
                break;
        }
    }
    
    private void OnDestroy()
    {
        // Clean up instanced materials
        CleanupMaterials();
        
        // Unregister from manager
        switch (Type)
        {
            case EntityType.Enemy:
                if (EnemyManager.HasInstance)
                    EnemyManager.Instance.UnregisterEnemy(this);
                break;
            case EntityType.Unit:
                if (UnitManager.HasInstance)
                    UnitManager.Instance.UnregisterUnit(this);
                break;
        }
        
        OnEntityDestroyed?.Invoke(this);
    }
    
    // Health management
    public void SetHealth(int health)
    {
        int oldHealth = currentHealth;
        currentHealth = Mathf.Clamp(health, 0, maxHealth);
        
        if (oldHealth != currentHealth)
        {
            OnEntityHealthChanged?.Invoke(this, oldHealth, currentHealth);
            Debug.Log($"[Entity Behaviour] Entity is now at {currentHealth}) life!");
            if (currentHealth <= 0)
            {
                Die();
            }
        }
    }
    
    public void ModifyHealth(int delta)
    {
        SetHealth(currentHealth + delta);
    }
    
    public void Damage(int amount)
    {
        if (amount > 0)
            ModifyHealth(-amount);
    }
    
    public void Heal(int amount)
    {
        if (amount > 0)
            ModifyHealth(amount);
    }
    
    private void Die()
    {
        // TODO: Death animation, effects, etc.
        Destroy(gameObject, 0.1f);
    }
    
    public void TakeDamage(int amount, DamageType damageType = DamageType.Normal)
    {
        // Future: Apply damage resistances based on type
        Damage(amount);
    }
    
    // Targeting
    public void SetTargeted(bool targeted)
    {
        if (isTargeted == targeted || !IsTargetable) return;
        
        isTargeted = targeted;
        
        if (selectionIndicator != null)
            selectionIndicator.SetActive(targeted);
        
        if (targeted)
            OnEntityTargeted?.Invoke(this);
        else
            OnEntityUntargeted?.Invoke(this);
    }
    
    // UI Event handlers
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsTargetable || !IsAlive) return;
        
        // Notify the appropriate manager about the click
        switch (Type)
        {
            case EntityType.Enemy:
                if (EnemyManager.HasInstance)
                    EnemyManager.Instance.HandleEntityClicked(this);
                break;
            case EntityType.Unit:
                if (UnitManager.HasInstance)
                    UnitManager.Instance.HandleEntityClicked(this);
                break;
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsTargetable || !IsAlive) return;
        
        isHovered = true;
        if (hoverIndicator != null)
            hoverIndicator.SetActive(true);
        
        OnEntityHovered?.Invoke(this);
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        if (hoverIndicator != null)
            hoverIndicator.SetActive(false);
        
        OnEntityUnhovered?.Invoke(this);
    }
    
    // Visual utilities - FIXED: Creates instance materials instead of modifying shared ones
    private void ApplyTintColor(Color color)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].sharedMaterial != null)
            {
                // Create instance material to avoid affecting other objects
                Material instanceMaterial = new Material(renderers[i].sharedMaterial);
                instanceMaterial.color = color;
                renderers[i].material = instanceMaterial;
            }
        }
    }
    
    private void CleanupMaterials()
    {
        // Destroy instanced materials to prevent memory leaks
        if (renderers != null)
        {
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.material != null)
                {
                    // Only destroy if it's not the original shared material
                    if (renderer.material != renderer.sharedMaterial)
                    {
                        DestroyImmediate(renderer.material);
                    }
                }
            }
        }
    }
    
    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        if (entityAsset != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(TargetPosition, 0.5f);
        }
    }
}