using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class EntityExtensions
{
    // === BASIC ENTITY VALIDATION ===
    public static bool IsValid(this EntityBehaviour entity)
        => entity != null && entity.gameObject != null;
    
    public static bool IsValidEntity(this EntityBehaviour entity)
        => entity.IsValid() && entity.gameObject.activeInHierarchy;
    
    public static bool IsAlive(this EntityBehaviour entity)
        => entity.IsValid() && entity.CurrentHealth > 0;
    
    public static bool IsValidTarget(this EntityBehaviour entity)
        => entity.IsValid() && entity.IsAlive() && entity.IsTargetable;

    // === TYPE CHECKS ===
    public static bool IsEnemy(this EntityBehaviour entity)
        => entity.IsValid() && entity.Type == EntityType.Enemy;
    
    public static bool IsUnit(this EntityBehaviour entity)
        => entity.IsValid() && entity.Type == EntityType.Unit;

    // === CATEGORY CHECKS ===
    public static bool IsBoss(this EntityBehaviour entity)
        => entity.IsValid() && entity.Category == EntityCategory.Boss;
    
    public static bool IsElite(this EntityBehaviour entity)
        => entity.IsValid() && entity.Category == EntityCategory.Elite;

    // === HEALTH STATUS ===
    public static float HealthPercentage(this EntityBehaviour entity)
        => entity.IsValid() ? (entity.MaxHealth > 0 ? (float)entity.CurrentHealth / entity.MaxHealth : 0f) : 0f;
    
    public static bool IsCriticalHealth(this EntityBehaviour entity)
        => entity.IsValid() && entity.HealthPercentage() < 0.2f;

    public static HealthStatus GetHealthStatus(this EntityBehaviour entity)
    {
        if (!entity.IsValid()) return HealthStatus.Dead;
        
        float percentage = entity.HealthPercentage();
        return percentage switch
        {
            <= 0f => HealthStatus.Dead,
            < 0.2f => HealthStatus.Critical,
            < 0.4f => HealthStatus.Low,
            < 0.7f => HealthStatus.Moderate,
            < 1f => HealthStatus.Healthy,
            _ => HealthStatus.Full
        };
    }

    // === ENHANCED DAMAGE SYSTEM ===
    public static DamageResult TryDamageWithEffects(this EntityBehaviour entity, int damage, DamageType damageType = DamageType.Normal, bool showEffects = false)
    {
        var result = new DamageResult();
        
        if (!entity.IsValid())
        {
            result.Success = false;
            result.FailureReason = "Invalid entity";
            return result;
        }

        if (damage <= 0)
        {
            result.Success = false;
            result.FailureReason = "Invalid damage amount";
            return result;
        }

        int healthBefore = entity.CurrentHealth;
        entity.TakeDamage(damage, damageType);
        int healthAfter = entity.CurrentHealth;
        
        result.Success = true;
        result.DamageDealt = healthBefore - healthAfter;
        result.WasKilled = healthAfter <= 0;
        result.HealthBefore = healthBefore;
        result.HealthAfter = healthAfter;

        return result;
    }

    public static HealResult TryHeal(this EntityBehaviour entity, int amount, bool canOverheal = false)
    {
        var result = new HealResult();
        
        if (!entity.IsValid() || !entity.IsAlive())
        {
            result.Success = false;
            result.FailureReason = "Entity cannot be healed";
            return result;
        }

        if (amount <= 0)
        {
            result.Success = false;
            result.FailureReason = "Invalid heal amount";
            return result;
        }

        int healthBefore = entity.CurrentHealth;
        int maxPossibleHeal = canOverheal ? amount : (entity.MaxHealth - healthBefore);
        int actualHeal = Mathf.Min(amount, maxPossibleHeal);
        
        if (actualHeal > 0)
        {
            entity.Heal(actualHeal);
            result.Success = true;
            result.HealingDone = actualHeal;
            result.HealthBefore = healthBefore;
            result.HealthAfter = entity.CurrentHealth;
        }
        else
        {
            result.Success = false;
            result.FailureReason = "No healing needed";
        }

        return result;
    }

    // === TARGETING AND POSITION ===
    public static Vector3 TargetPosition(this EntityBehaviour entity)
        => entity.IsValid() ? entity.transform.position + entity.Asset?.TargetOffset ?? Vector3.up : Vector3.zero;

    public static float TargetPriority(this EntityBehaviour entity)
        => entity.IsValid() ? entity.Asset?.TargetPriority ?? 1f : 0f;

    public static bool IsTargeted(this EntityBehaviour entity)
        => entity?.isTargeted ?? false;

    // === ASSET ACCESS ===
    public static EntityAsset Asset(this EntityBehaviour entity)
        => entity?.entityAsset;

    public static string EntityName(this EntityBehaviour entity)
        => entity?.Asset()?.EntityName ?? "Unknown Entity";

    public static EntityType Type(this EntityBehaviour entity)
        => entity?.Asset()?.Type ?? EntityType.Neutral;

    public static EntityCategory Category(this EntityBehaviour entity)
        => entity?.Asset()?.Category ?? EntityCategory.Standard;

    public static bool IsTargetable(this EntityBehaviour entity)
        => entity?.Asset()?.IsTargetable ?? false;

    // === COLLECTION QUERIES ===
    public static EntityBehaviour GetWeakest(this IEnumerable<EntityBehaviour> entities)
        => entities?.Where(e => e.IsValidTarget()).OrderBy(e => e.CurrentHealth).FirstOrDefault();
    
    public static EntityBehaviour GetStrongest(this IEnumerable<EntityBehaviour> entities)
        => entities?.Where(e => e.IsValidTarget()).OrderByDescending(e => e.CurrentHealth).FirstOrDefault();
    
    public static EntityBehaviour GetRandom(this IEnumerable<EntityBehaviour> entities)
    {
        var valid = entities?.Where(e => e.IsValidTarget()).ToList();
        return valid?.Count > 0 ? valid[Random.Range(0, valid.Count)] : null;
    }
    
    public static EntityBehaviour GetNearestTo(this IEnumerable<EntityBehaviour> entities, Vector3 position)
        => entities?.Where(e => e.IsValidTarget())
            .OrderBy(e => Vector3.Distance(e.TargetPosition(), position))
            .FirstOrDefault();
    
    public static EntityBehaviour GetHighestPriority(this IEnumerable<EntityBehaviour> entities)
        => entities?.Where(e => e.IsValidTarget())
            .OrderByDescending(e => e.TargetPriority())
            .FirstOrDefault();
    
    public static IEnumerable<EntityBehaviour> FilterByHealth(this IEnumerable<EntityBehaviour> entities, 
        float threshold, HealthComparison comparison)
    {
        return comparison switch
        {
            HealthComparison.Below => entities.Where(e => e.HealthPercentage() < threshold),
            HealthComparison.Above => entities.Where(e => e.HealthPercentage() > threshold),
            HealthComparison.BelowOrEqual => entities.Where(e => e.HealthPercentage() <= threshold),
            HealthComparison.AboveOrEqual => entities.Where(e => e.HealthPercentage() >= threshold),
            _ => entities
        };
    }
    
    public static EntityBehaviour GetBestTarget(this IEnumerable<EntityBehaviour> entities, TargetingCriteria criteria)
    {
        return entities?.Where(e => e.IsValidTarget())
            .OrderBy(e => CalculateTargetScore(e, criteria))
            .FirstOrDefault();
    }
    
    private static float CalculateTargetScore(EntityBehaviour entity, TargetingCriteria criteria)
    {
        float score = 0f;
        
        if (criteria.PreferLowHealth)
            score += entity.HealthPercentage() * criteria.HealthWeight;
        else
            score += (1f - entity.HealthPercentage()) * criteria.HealthWeight;
        
        if (criteria.PreferClose && criteria.ReferencePosition != null)
        {
            float distance = Vector3.Distance(entity.TargetPosition(), criteria.ReferencePosition);
            score += distance * criteria.DistanceWeight;
        }
        
        score -= entity.TargetPriority() * criteria.PriorityWeight;
        
        return score;
    }
}

// === RESULT CLASSES ===
[System.Serializable]
public class DamageResult
{
    public bool Success;
    public string FailureReason = "";
    public int DamageDealt;
    public bool WasKilled;
    public int HealthBefore;
    public int HealthAfter;
}

[System.Serializable]
public class HealResult
{
    public bool Success;
    public string FailureReason = "";
    public int HealingDone;
    public int HealthBefore;
    public int HealthAfter;
}

[System.Serializable]
public class TargetingCriteria
{
    public bool PreferLowHealth = true;
    public bool PreferClose = true;
    public Vector3 ReferencePosition = Vector3.zero;
    public float HealthWeight = 1f;
    public float DistanceWeight = 0.5f;
    public float PriorityWeight = 0.8f;
}