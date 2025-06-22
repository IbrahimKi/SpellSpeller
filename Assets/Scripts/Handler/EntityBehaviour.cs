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
    
    // PERFORMANCE FIX: Use PropertyBlock instead of material instances
    private MaterialPropertyBlock _propBlock;
    
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
            var boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.size = Vector3.one * 2f;
            boxCollider.center = Vector3.up;
            targetCollider = boxCollider;
        }
        
        worldCanvas = GetComponentInChildren<Canvas>();
        
        if (worldCanvas != null && worldCanvas.renderMode == RenderMode.WorldSpace)
        {
            worldCanvas.transform.localScale = Vector3.one * 0.01f;
            worldCanvas.sortingOrder = 10;
        }
        
        renderers = GetComponentsInChildren<Renderer>();
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
        
        // PERFORMANCE FIX: Use PropertyBlock for tint color
        if (asset.TintColor != Color.white)
        {
            ApplyTintColor(asset.TintColor);
        }
        
        if (targetCollider != null)
        {
            targetCollider.enabled = asset.IsTargetable;
        }
        
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
        Destroy(gameObject, 0.1f);
    }
    
    public void TakeDamage(int amount, DamageType damageType = DamageType.Normal)
    {
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
    
    // PERFORMANCE FIX: Use PropertyBlock instead of creating material instances
    private void ApplyTintColor(Color color)
    {
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
        
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].sharedMaterial != null)
            {
                _propBlock.SetColor("_Color", color);
                renderers[i].SetPropertyBlock(_propBlock);
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