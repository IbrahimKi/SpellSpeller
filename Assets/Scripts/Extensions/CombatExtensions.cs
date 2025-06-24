using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using GameCore.Enums;
using GameCore.Data;

public static class CombatExtensions
{
    public static bool IsInPlayerActionPhase(this CombatManager combat)
    {
        if (!combat.IsManagerReady()) return false;
        
        return combat.IsInCombat && 
               combat.CurrentPhase == TurnPhase.PlayerTurn && 
               !combat.IsProcessingTurn &&
               combat.Life.CurrentValue > 0;
    }
    
    public static bool CanEndTurnSafely(this CombatManager combat)
    {
        if (!combat.IsInPlayerActionPhase()) return false;
        
        bool hasUnresolvedCombo = false;
        if (SpellcastManager.HasInstance && SpellcastManager.Instance.IsReady)
        {
            hasUnresolvedCombo = SpellcastManager.Instance.CurrentComboState == ComboState.Building || 
                               SpellcastManager.Instance.CurrentComboState == ComboState.Ready;
        }
        
        return !hasUnresolvedCombo;
    }
    
    public static CombatDifficulty GetCombatDifficulty(this CombatManager combat)
    {
        if (!combat.IsInCombat) return CombatDifficulty.None;
        
        float healthPercentage = combat.Life.Percentage;
        int enemyCount = 0;
        
        if (EnemyManager.HasInstance && EnemyManager.Instance.IsReady)
            enemyCount = EnemyManager.Instance.AliveEnemyCount;
        
        return (healthPercentage, enemyCount) switch
        {
            (< 0.25f, > 2) => CombatDifficulty.Desperate,
            (< 0.5f, > 1) => CombatDifficulty.Hard,
            (< 0.75f, _) => CombatDifficulty.Moderate,
            (_, > 3) => CombatDifficulty.Moderate,
            _ => CombatDifficulty.Easy
        };
    }
    
    public static CombatSituation GetCombatSituation(this CombatManager combat)
    {
        if (!combat.IsInCombat) return new CombatSituation();
        
        var healthStatus = GetHealthStatusFromPercentage(combat.Life.Percentage);
        var creativityStatus = GetResourceStatusFromPercentage(combat.Creativity.Percentage);
        var enemyThreat = GetEnemyThreatLevel(combat);
        
        var situation = new CombatSituation
        {
            IsPlayerTurn = combat.IsPlayerTurn,
            CanAct = combat.IsInPlayerActionPhase(),
            HealthStatus = healthStatus,
            CreativityStatus = creativityStatus,
            EnemyThreat = enemyThreat,
            RecommendedAction = GetRecommendedAction(combat, healthStatus, creativityStatus, enemyThreat),
            UrgencyLevel = GetUrgencyLevel(combat, healthStatus, creativityStatus, enemyThreat)
        };
        
        situation.IsResourceCrisis = healthStatus <= HealthStatus.Critical || 
                                   creativityStatus <= ResourceStatus.Low;
        
        return situation;
    }
    
    public static bool TryEndTurn(this CombatManager combat, bool force = false)
    {
        if (!combat.IsManagerReady()) return false;
        
        if (!force && !combat.CanEndTurnSafely())
        {
            Debug.LogWarning("[CombatExtensions] Turn end blocked - unsafe conditions");
            return false;
        }
        
        try
        {
            combat.EndPlayerTurn();
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CombatExtensions] Turn end failed: {ex.Message}");
            return false;
        }
    }
    
    public static bool CanAffordAction(this CombatManager combat, ActionCost cost)
    {
        if (!combat.IsManagerReady() || cost == null) return false;
        
        bool canAfford = true;
        
        if (cost.Amount > 0)
        {
            var resource = combat.GetResourceByType(cost.ResourceType);
            canAfford &= resource.IsValidResource() && resource.HasAvailable(cost.Amount);
        }
        
        return canAfford;
    }
    
    public static bool TrySpendResources(this CombatManager combat, ActionCost cost)
    {
        if (!combat.CanAffordAction(cost)) return false;
        
        try
        {
            if (cost.Amount > 0)
                combat.TryModifyResource(cost.ResourceType, -cost.Amount);
            
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CombatExtensions] Resource spending failed: {ex.Message}");
            return false;
        }
    }
    
    public static bool TrySmartHeal(this CombatManager combat, int healAmount, HealingMode mode = HealingMode.Self)
    {
        if (!combat.IsManagerReady()) return false;
        
        switch (mode)
        {
            case HealingMode.Self:
                combat.ModifyLife(healAmount);
                return true;
                
            case HealingMode.MostDamaged:
                if (UnitManager.HasInstance && UnitManager.Instance.IsReady)
                {
                    var target = UnitManager.Instance.GetLowestHealthUnit();
                    if (target != null)
                    {
                        target.Heal(healAmount);
                        return true;
                    }
                }
                return false;
                
            case HealingMode.All:
                combat.ModifyLife(healAmount / 2);
                if (UnitManager.HasInstance && UnitManager.Instance.IsReady)
                {
                    UnitManager.Instance.HealAllUnits(healAmount / 2);
                }
                return true;
                
            case HealingMode.Critical:
                if (UnitManager.HasInstance && UnitManager.Instance.IsReady)
                {
                    var criticalUnits = UnitManager.Instance.AliveUnits
                        .Where(u => u != null && u.IsAlive && u.HealthPercentage <= 0.25f)
                        .Take(3);
                    
                    foreach (var unit in criticalUnits)
                    {
                        unit.Heal(healAmount / 3);
                    }
                    return true;
                }
                return false;
                
            default:
                return false;
        }
    }
    
    public static EntityBehaviour GetSmartTarget(this EnemyManager enemyManager, TargetingStrategy strategy = TargetingStrategy.Optimal)
    {
        if (!enemyManager.IsManagerReady()) return null;
        
        var enemies = enemyManager.AliveEnemies;
        if (!enemies.Any()) return null;
        
        return strategy switch
        {
            TargetingStrategy.Weakest => enemies.OrderBy(e => e.CurrentHealth).FirstOrDefault(),
            TargetingStrategy.Strongest => enemies.OrderByDescending(e => e.CurrentHealth).FirstOrDefault(),
            TargetingStrategy.Nearest => GetNearestEnemy(enemies),
            TargetingStrategy.Priority => enemies.OrderByDescending(e => e.TargetPriority).FirstOrDefault(),
            TargetingStrategy.Random => enemies.OrderBy(x => System.Guid.NewGuid()).FirstOrDefault(),
            _ => GetOptimalTarget(enemies)
        };
    }
    
    public static List<EntityBehaviour> GetOptimalTargets(this EnemyManager enemyManager, int maxTargets = 3, TargetingStrategy strategy = TargetingStrategy.Optimal)
    {
        if (!enemyManager.IsManagerReady()) return new List<EntityBehaviour>();
        
        var enemies = enemyManager.AliveEnemies;
        if (!enemies.Any()) return new List<EntityBehaviour>();
        
        return strategy switch
        {
            TargetingStrategy.Weakest => enemies.OrderBy(e => e.CurrentHealth).Take(maxTargets).ToList(),
            TargetingStrategy.Strongest => enemies.OrderByDescending(e => e.CurrentHealth).Take(maxTargets).ToList(),
            TargetingStrategy.Priority => enemies.OrderByDescending(e => e.TargetPriority).Take(maxTargets).ToList(),
            TargetingStrategy.Random => enemies.OrderBy(x => System.Guid.NewGuid()).Take(maxTargets).ToList(),
            _ => GetOptimalTargetGroup(enemies, maxTargets)
        };
    }
    
    private static HealthStatus GetHealthStatusFromPercentage(float percentage)
    {
        return percentage switch
        {
            >= 0.9f => HealthStatus.Excellent,
            >= 0.75f => HealthStatus.Good,
            >= 0.5f => HealthStatus.Moderate,
            >= 0.25f => HealthStatus.Low,
            >= 0.1f => HealthStatus.Critical,
            _ => HealthStatus.Dying
        };
    }
    
    private static ResourceStatus GetResourceStatusFromPercentage(float percentage)
    {
        return percentage switch
        {
            >= 0.8f => ResourceStatus.High,
            >= 0.5f => ResourceStatus.Medium,
            >= 0.25f => ResourceStatus.Low,
            _ => ResourceStatus.Critical
        };
    }
    
    private static ThreatLevel GetEnemyThreatLevel(CombatManager combat)
    {
        int enemyCount = 0;
        if (EnemyManager.HasInstance && EnemyManager.Instance.IsReady)
            enemyCount = EnemyManager.Instance.AliveEnemyCount;
        
        float playerHealth = combat.Life.Percentage;
        
        return (enemyCount, playerHealth) switch
        {
            (> 3, < 0.5f) => ThreatLevel.Extreme,
            (> 2, < 0.3f) => ThreatLevel.High,
            (> 1, < 0.5f) => ThreatLevel.Medium,
            (1, _) => ThreatLevel.Low,
            _ => ThreatLevel.None
        };
    }
    
    private static ActionType GetRecommendedAction(CombatManager combat, HealthStatus healthStatus, ResourceStatus creativityStatus, ThreatLevel threatLevel)
    {
        if (healthStatus <= HealthStatus.Critical)
            return ActionType.Heal;
        
        if (creativityStatus <= ResourceStatus.Low)
        {
            bool handNotFull = true;
            if (CardManager.HasInstance && CardManager.Instance.IsReady)
                handNotFull = !CardManager.Instance.IsHandFull;
                
            if (handNotFull)
                return ActionType.DrawCard;
        }
        
        return threatLevel switch
        {
            ThreatLevel.High or ThreatLevel.Extreme => ActionType.Attack,
            ThreatLevel.Medium when healthStatus <= HealthStatus.Moderate => ActionType.Defend,
            _ => ActionType.Attack
        };
    }
    
    private static UrgencyLevel GetUrgencyLevel(CombatManager combat, HealthStatus healthStatus, ResourceStatus creativityStatus, ThreatLevel threatLevel)
    {
        if (healthStatus <= HealthStatus.Critical)
            return UrgencyLevel.Critical;
        
        if (creativityStatus <= ResourceStatus.Critical)
            return UrgencyLevel.High;
        
        return threatLevel switch
        {
            ThreatLevel.Extreme => UrgencyLevel.Critical,
            ThreatLevel.High => UrgencyLevel.High,
            ThreatLevel.Medium when healthStatus <= HealthStatus.Low => UrgencyLevel.High,
            ThreatLevel.Medium => UrgencyLevel.Medium,
            _ => UrgencyLevel.Low
        };
    }
    
    private static EntityBehaviour GetNearestEnemy(IReadOnlyList<EntityBehaviour> enemies)
    {
        return enemies.FirstOrDefault();
    }
    
    private static EntityBehaviour GetOptimalTarget(IReadOnlyList<EntityBehaviour> enemies)
    {
        return enemies.OrderBy(e => e.HealthPercentage).FirstOrDefault();
    }
    
    private static List<EntityBehaviour> GetOptimalTargetGroup(IReadOnlyList<EntityBehaviour> enemies, int maxTargets)
    {
        return enemies.OrderBy(e => e.HealthPercentage).Take(maxTargets).ToList();
    }
}