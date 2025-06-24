using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using GameCore.Enums;
using GameCore.Data;

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
    
    // Properties - INTEGRATION: Enhanced with EntityExtensions
    public int UnitCount => _units.Count;
    public int AliveUnitCount => _units.Values.Count(u => u.IsValidTarget());
    public bool HasUnits => AliveUnitCount > 0;
    public IReadOnlyList<EntityBehaviour> AllUnits => _units.Values.Where(u => u.IsValidEntity()).ToList();
    public IReadOnlyList<EntityBehaviour> AliveUnits => _units.Values.Where(u => u.IsValidTarget()).ToList();
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
        if (unitAsset == null || !IsUnitAsset(unitAsset))
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
    
    /// <summary>
    /// Helper method to check if asset is unit type
    /// FALLBACK: Direct type check if EntityExtensions not available
    /// </summary>
    private bool IsUnitAsset(EntityAsset asset)
    {
        return asset != null && asset.Type == EntityType.Unit;
    }
    
    /// <summary>
    /// Helper method to check if entity is unit type
    /// FALLBACK: Direct type check if EntityExtensions not available
    /// </summary>
    private bool IsUnitEntity(EntityBehaviour entity)
    {
        return entity != null && entity.Type == EntityType.Unit;
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
        if (unit == null || !IsUnitEntity(unit)) return;
        
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
        if (!unit.IsValidTarget() || !allowUnitSelection) return;
        
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
        if (!unit.IsValidTarget() || !IsUnitEntity(unit)) return;
        
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
    
    // INTEGRATION: Enhanced healing with EntityExtensions
    public void HealAllUnits(int amount)
    {
        foreach (var unit in AliveUnits)
        {
            var result = unit.TryHeal(amount, true);
            if (result.Success)
            {
                Debug.Log($"[UnitManager] Healed {unit.EntityName} for {result.HealingDone} HP");
            }
        }
    }
    
    public void HealSelectedUnit(int amount)
    {
        if (_selectedUnit != null && _selectedUnit.IsAlive)
        {
            var result = _selectedUnit.TryHeal(amount, true);
            if (result.Success)
            {
                Debug.Log($"[UnitManager] Healed selected unit {_selectedUnit.EntityName} for {result.HealingDone} HP");
            }
        }
    }
    
    // INTEGRATION: Enhanced unit queries using EntityExtensions
    public EntityBehaviour GetLowestHealthUnit()
    {
        return AliveUnits.GetWeakest();
    }
    
    public EntityBehaviour GetHighestHealthUnit()
    {
        return AliveUnits.GetStrongest();
    }
    
    public List<EntityBehaviour> GetCriticalUnits()
    {
        return AliveUnits.Where(u => u.IsCriticalHealth()).ToList();
    }
    
    public List<EntityBehaviour> GetHealthyUnits(float threshold = 0.8f)
    {
        return AliveUnits.FilterByHealth(threshold, HealthComparison.AboveOrEqual).ToList();
    }
    
    // INTEGRATION: Smart unit selection using EntityExtensions
    public EntityBehaviour GetBestHealTarget()
    {
        // Prioritize units in critical health, then lowest health percentage
        var criticalUnits = GetCriticalUnits();
        if (criticalUnits.Count > 0)
        {
            return criticalUnits.OrderBy(u => u.HealthPercentage).First();
        }
        
        return GetLowestHealthUnit();
    }
    
    public EntityBehaviour GetBestBuffTarget()
    {
        // Prioritize healthy units that can make best use of buffs
        return AliveUnits
            .Where(u => u.HealthPercentage > 0.5f)
            .OrderByDescending(u => u.TargetPriority)
            .FirstOrDefault();
    }
    
    // Event handlers
    private void HandleEntityHealthChanged(EntityBehaviour entity, int oldHealth, int newHealth)
    {
        if (!IsUnitEntity(entity)) return;
        
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
        if (!IsUnitEntity(entity)) return;
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
    
    // INTEGRATION: Unit status assessment using EntityExtensions
    public UnitStatusReport GetUnitStatusReport()
    {
        var report = new UnitStatusReport();
        
        report.TotalUnits = AliveUnitCount;
        report.HealthyUnits = GetHealthyUnits().Count;
        report.CriticalUnits = GetCriticalUnits().Count;
        
        if (AliveUnits.Count > 0)
        {
            report.AverageHealth = AliveUnits.Average(u => u.HealthPercentage);
            report.LowestHealth = AliveUnits.Min(u => u.HealthPercentage);
            report.HighestHealth = AliveUnits.Max(u => u.HealthPercentage);
            
            // Overall unit status
            if (report.CriticalUnits > report.TotalUnits / 2)
            {
                report.OverallStatus = UnitGroupStatus.Critical;
            }
            else if (report.AverageHealth < 0.5f)
            {
                report.OverallStatus = UnitGroupStatus.Damaged;
            }
            else if (report.AverageHealth > 0.8f)
            {
                report.OverallStatus = UnitGroupStatus.Healthy;
            }
            else
            {
                report.OverallStatus = UnitGroupStatus.Moderate;
            }
        }
        else
        {
            report.OverallStatus = UnitGroupStatus.None;
        }
        
        return report;
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
            var healthStatus = unit.GetHealthStatus();
            Debug.Log($"  - {unit.EntityName}: {unit.CurrentHealth}/{unit.MaxHealth} HP ({healthStatus})");
        }
    }
    
    [ContextMenu("Get Status Report")]
    public void LogStatusReport()
    {
        var report = GetUnitStatusReport();
        Debug.Log($"[UnitManager] Unit Status Report:");
        Debug.Log($"  Overall Status: {report.OverallStatus}");
        Debug.Log($"  Total: {report.TotalUnits}");
        Debug.Log($"  Healthy: {report.HealthyUnits}");
        Debug.Log($"  Critical: {report.CriticalUnits}");
        Debug.Log($"  Average Health: {report.AverageHealth:P0}");
    }
    
    [ContextMenu("Heal Best Target")]
    public void DebugHealBestTarget()
    {
        var target = GetBestHealTarget();
        if (target != null)
        {
            target.TryHeal(50, true);
            Debug.Log($"[UnitManager] Healed best target: {target.EntityName}");
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

// INTEGRATION: Supporting class for unit status
[System.Serializable]
public class UnitStatusReport
{
    public int TotalUnits;
    public int HealthyUnits;
    public int CriticalUnits;
    public float AverageHealth;
    public float LowestHealth;
    public float HighestHealth;
    public UnitGroupStatus OverallStatus;
}