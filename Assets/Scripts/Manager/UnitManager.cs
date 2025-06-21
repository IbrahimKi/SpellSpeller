using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public class UnitManager : SingletonBehaviour<UnitManager>, IGameManager
{
    [Header("Unit Management")]
    [SerializeField] private int maxUnits = 5;
    [SerializeField] private Transform unitContainer;
    
    [Header("Targeting")]
    [SerializeField] private bool allowUnitSelection = true;
    [SerializeField] private Color selectedTintColor = new Color(0.8f, 1f, 0.8f);
    
    [Header("Formation")]
    [SerializeField] private float unitSpacing = 2f;
    [SerializeField] private Vector3 formationCenter = new Vector3(-5f, 0f, 0f);
    [SerializeField] private FormationType formationType = FormationType.Line;
    
    [Header("Spawn Settings")]
    [SerializeField] private float spawnAnimationDuration = 0.5f;
    [SerializeField] private float formationUpdateSpeed = 2f;
    
    // Unit tracking
    private Dictionary<int, EntityBehaviour> _units = new Dictionary<int, EntityBehaviour>();
    private EntityBehaviour _selectedUnit;
    private int _nextUnitId = 0;
    
    // Manager state
    private bool _isReady = false;
    public bool IsReady => _isReady;
    
    // Events
    public static event System.Action<EntityBehaviour> OnUnitSpawned;
    public static event System.Action<EntityBehaviour> OnUnitDespawned;
    public static event System.Action<EntityBehaviour> OnUnitSelected;
    public static event System.Action<EntityBehaviour> OnUnitDeselected;
    public static event System.Action<EntityBehaviour, int> OnUnitDamaged;
    public static event System.Action<EntityBehaviour> OnUnitKilled;
    public static event System.Action OnAllUnitsDefeated;
    public static event System.Action OnFormationUpdated;
    
    // Properties
    public int UnitCount => _units.Count;
    public int AliveUnitCount => _units.Values.Count(u => u != null && u.IsAlive);
    public bool HasUnits => AliveUnitCount > 0;
    public IReadOnlyList<EntityBehaviour> AllUnits => _units.Values.Where(u => u != null).ToList();
    public IReadOnlyList<EntityBehaviour> AliveUnits => _units.Values.Where(u => u != null && u.IsAlive).ToList();
    public EntityBehaviour SelectedUnit => _selectedUnit;
    public bool HasSelectedUnit => _selectedUnit != null && _selectedUnit.IsAlive;
    
    protected override void OnAwakeInitialize()
    {
        InitializeContainer();
        _isReady = true;
    }
    
    private void InitializeContainer()
    {
        if (unitContainer == null)
        {
            GameObject container = new GameObject("Unit Container");
            container.transform.SetParent(transform);
            unitContainer = container.transform;
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
    
    // Unit spawning
    public EntityBehaviour SpawnUnit(EntityAsset unitAsset, Vector3 position = default, Quaternion rotation = default)
    {
        if (unitAsset == null || unitAsset.Type != EntityType.Unit)
        {
            Debug.LogError("[UnitManager] Invalid unit asset");
            return null;
        }
        
        if (_units.Count >= maxUnits)
        {
            Debug.LogWarning($"[UnitManager] Max unit limit reached ({maxUnits})");
            return null;
        }
        
        // Use formation position if no position specified
        if (position == default)
            position = GetNextFormationPosition();
        
        if (rotation == default)
            rotation = Quaternion.identity;
        
        GameObject unitObject = unitAsset.CreateInstance(position, rotation, unitContainer);
        if (unitObject == null) return null;
        
        EntityBehaviour unit = unitObject.GetComponent<EntityBehaviour>();
        if (unit == null)
        {
            Destroy(unitObject);
            return null;
        }
        
        // Apply spawn animation if needed
        if (unitAsset.SpawnWithAnimation)
        {
            StartCoroutine(SpawnAnimation(unit, unitAsset.SpawnDelay));
        }
        
        // Update formation for all units
        UpdateFormation();
        
        return unit;
    }
    
    private IEnumerator SpawnAnimation(EntityBehaviour unit, float delay)
    {
        if (delay > 0)
            yield return new WaitForSeconds(delay);
        
        // Simple fade-in animation
        Vector3 originalScale = unit.transform.localScale;
        unit.transform.localScale = Vector3.zero;
        
        float elapsed = 0f;
        while (elapsed < spawnAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / spawnAnimationDuration;
            unit.transform.localScale = Vector3.Lerp(Vector3.zero, originalScale, t);
            yield return null;
        }
        
        unit.transform.localScale = originalScale;
    }
    
    // Registration (called by EntityBehaviour)
    public void RegisterUnit(EntityBehaviour unit)
    {
        if (unit == null || unit.Type != EntityType.Unit) return;
        
        int id = _nextUnitId++;
        _units[id] = unit;
        
        OnUnitSpawned?.Invoke(unit);
        Debug.Log($"[UnitManager] Unit registered: {unit.EntityName} (ID: {id})");
    }
    
    public void UnregisterUnit(EntityBehaviour unit)
    {
        if (unit == null) return;
        
        var kvp = _units.FirstOrDefault(x => x.Value == unit);
        if (kvp.Value != null)
        {
            _units.Remove(kvp.Key);
            
            if (_selectedUnit == unit)
            {
                _selectedUnit = null;
                OnUnitDeselected?.Invoke(unit);
            }
            
            OnUnitDespawned?.Invoke(unit);
            CheckAllUnitsDefeated();
            UpdateFormation();
        }
    }
    
    // Selection
    public void HandleEntityClicked(EntityBehaviour unit)
    {
        if (unit == null || !unit.IsAlive || !allowUnitSelection) return;
        
        if (_selectedUnit == unit)
        {
            // Already selected - deselect
            DeselectUnit();
        }
        else
        {
            // Select new unit
            SelectUnit(unit);
        }
    }
    
    public void SelectUnit(EntityBehaviour unit)
    {
        if (unit == null || unit.Type != EntityType.Unit || !unit.IsAlive) return;
        
        // Deselect current unit
        if (_selectedUnit != null)
        {
            _selectedUnit.SetTargeted(false);
            OnUnitDeselected?.Invoke(_selectedUnit);
        }
        
        _selectedUnit = unit;
        unit.SetTargeted(true);
        
        OnUnitSelected?.Invoke(unit);
    }
    
    public void DeselectUnit()
    {
        if (_selectedUnit != null)
        {
            var unit = _selectedUnit;
            _selectedUnit.SetTargeted(false);
            _selectedUnit = null;
            OnUnitDeselected?.Invoke(unit);
        }
    }
    
    // Formation management
    private Vector3 GetNextFormationPosition()
    {
        int index = _units.Count;
        return GetFormationPosition(index);
    }
    
    private Vector3 GetFormationPosition(int index)
    {
        switch (formationType)
        {
            case FormationType.Line:
                return formationCenter + Vector3.right * (index * unitSpacing);
                
            case FormationType.Circle:
                float angle = (index * 360f / maxUnits) * Mathf.Deg2Rad;
                return formationCenter + new Vector3(
                    Mathf.Cos(angle) * unitSpacing,
                    0f,
                    Mathf.Sin(angle) * unitSpacing
                );
                
            case FormationType.Grid:
                int gridSize = Mathf.CeilToInt(Mathf.Sqrt(maxUnits));
                int row = index / gridSize;
                int col = index % gridSize;
                return formationCenter + new Vector3(
                    col * unitSpacing,
                    0f,
                    row * unitSpacing
                );
                
            default:
                return formationCenter;
        }
    }
    
    public void UpdateFormation()
    {
        StartCoroutine(UpdateFormationCoroutine());
    }
    
    private IEnumerator UpdateFormationCoroutine()
    {
        var aliveUnits = AliveUnits;
        
        for (int i = 0; i < aliveUnits.Count; i++)
        {
            var unit = aliveUnits[i];
            if (unit != null)
            {
                Vector3 targetPos = GetFormationPosition(i);
                StartCoroutine(MoveUnitToPosition(unit, targetPos));
            }
        }
        
        yield return new WaitForSeconds(0.1f);
        OnFormationUpdated?.Invoke();
    }
    
    private IEnumerator MoveUnitToPosition(EntityBehaviour unit, Vector3 targetPosition)
    {
        Vector3 startPos = unit.transform.position;
        float elapsed = 0f;
        float duration = 1f / formationUpdateSpeed;
        
        while (elapsed < duration && unit != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            unit.transform.position = Vector3.Lerp(startPos, targetPosition, t);
            yield return null;
        }
        
        if (unit != null)
            unit.transform.position = targetPosition;
    }
    
    // Combat utilities
    public void HealAllUnits(int amount)
    {
        foreach (var unit in AliveUnits)
        {
            unit.Heal(amount);
        }
    }
    
    public void HealSelectedUnit(int amount)
    {
        if (_selectedUnit != null && _selectedUnit.IsAlive)
        {
            _selectedUnit.Heal(amount);
        }
    }
    
    public EntityBehaviour GetLowestHealthUnit()
    {
        return AliveUnits
            .OrderBy(u => u.HealthPercentage)
            .FirstOrDefault();
    }
    
    public EntityBehaviour GetHighestHealthUnit()
    {
        return AliveUnits
            .OrderByDescending(u => u.HealthPercentage)
            .FirstOrDefault();
    }
    
    // Event handlers
    private void HandleEntityHealthChanged(EntityBehaviour entity, int oldHealth, int newHealth)
    {
        if (entity == null || entity.Type != EntityType.Unit) return;
        
        int damage = oldHealth - newHealth;
        if (damage > 0)
        {
            OnUnitDamaged?.Invoke(entity, damage);
        }
        
        if (newHealth <= 0)
        {
            OnUnitKilled?.Invoke(entity);
            
            if (_selectedUnit == entity)
            {
                _selectedUnit = null;
                OnUnitDeselected?.Invoke(entity);
            }
            
            CheckAllUnitsDefeated();
        }
    }
    
    private void HandleEntityDestroyed(EntityBehaviour entity)
    {
        if (entity == null || entity.Type != EntityType.Unit) return;
        UnregisterUnit(entity);
    }
    
    private void CheckAllUnitsDefeated()
    {
        if (!HasUnits)
        {
            OnAllUnitsDefeated?.Invoke();
            Debug.Log("[UnitManager] All units defeated!");
        }
    }
    
    // Utility methods
    public void DespawnAllUnits()
    {
        var units = _units.Values.ToList();
        foreach (var unit in units)
        {
            if (unit != null && unit.gameObject != null)
                Destroy(unit.gameObject);
        }
        
        _units.Clear();
        _selectedUnit = null;
    }
    
    public void SetFormationType(FormationType type)
    {
        if (formationType != type)
        {
            formationType = type;
            UpdateFormation();
        }
    }
    
#if UNITY_EDITOR
    [ContextMenu("Log Unit Status")]
    public void LogUnitStatus()
    {
        Debug.Log($"[UnitManager] Status:");
        Debug.Log($"  Total Units: {UnitCount}");
        Debug.Log($"  Alive Units: {AliveUnitCount}");
        Debug.Log($"  Selected: {(_selectedUnit != null ? _selectedUnit.EntityName : "None")}");
        
        foreach (var unit in AllUnits)
        {
            Debug.Log($"  - {unit.EntityName}: {unit.CurrentHealth}/{unit.MaxHealth} HP");
        }
    }
    
    [ContextMenu("Update Formation")]
    public void DebugUpdateFormation()
    {
        UpdateFormation();
    }
    
    private void OnDrawGizmos()
    {
        // Draw formation positions
        Gizmos.color = Color.green;
        for (int i = 0; i < maxUnits; i++)
        {
            Vector3 pos = GetFormationPosition(i);
            Gizmos.DrawWireSphere(pos, 0.5f);
        }
        
        // Draw formation center
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(formationCenter, Vector3.one);
    }
#endif
}

public enum FormationType
{
    Line,
    Circle,
    Grid,
    Custom
}