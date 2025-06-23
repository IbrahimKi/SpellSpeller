using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public static class EntityExtensions
{
    public static EntityHealthStatus GetHealthStatus(this EntityBehaviour entity)
    {
        if (!entity.IsValidTarget()) return EntityHealthStatus.Invalid;
        
        return entity.HealthPercentage switch
        {
            <= 0.0f => EntityHealthStatus.Dead,
            <= 0.15f => EntityHealthStatus.Critical,
            <= 0.35f => EntityHealthStatus.Low,
            <= 0.65f => EntityHealthStatus.Moderate,
            <= 0.85f => EntityHealthStatus.High,
            _ => EntityHealthStatus.Full
        };
    }
    
    public static bool IsHealthInRange(this EntityBehaviour entity, float min, float max)
    {
        if (!entity.IsValidTarget()) return false;
        return entity.HealthPercentage >= min && entity.HealthPercentage <= max;
    }
    
    public static bool IsLowHealth(this EntityBehaviour entity, float threshold = 0.35f)
        => entity.IsValidTarget() && entity.HealthPercentage <= threshold;
    
    public static bool IsCriticalHealth(this EntityBehaviour entity, float threshold = 0.15f)
        => entity.IsValidTarget() && entity.HealthPercentage <= threshold;
    
    public static bool IsNearDeath(this EntityBehaviour entity, float threshold = 0.05f)
        => entity.IsValidTarget() && entity.HealthPercentage <= threshold;
    
    public static bool IsHealthy(this EntityBehaviour entity, float threshold = 0.8f)
        => entity.IsValidTarget() && entity.HealthPercentage >= threshold;
    
    public static bool IsValidTarget(this EntityBehaviour entity)
        => entity != null && !entity.Equals(null) && entity.IsAlive && entity.IsTargetable;
    
    public static bool IsValidEntity(this EntityBehaviour entity)
        => entity != null && !entity.Equals(null);
    
    public static bool IsActiveEntity(this EntityBehaviour entity)
        => entity.IsValidEntity() && entity.gameObject != null && entity.gameObject.activeInHierarchy;
    
    public static DamageResult TryDamageWithEffects(this EntityBehaviour entity, int damage, 
        DamageType damageType = DamageType.Normal, bool showEffects = true)
    {
        var result = new DamageResult();
        
        if (!entity.IsValidTarget() || damage <= 0)
        {
            result.FailureReason = "Invalid target or damage amount";
            return result;
        }
        
        int originalHealth = entity.CurrentHealth;
        int finalDamage = CalculateFinalDamage(entity, damage, damageType);
        
        entity.TakeDamage(finalDamage, damageType);
        
        result.Success = true;
        result.DamageDealt = originalHealth - entity.CurrentHealth;
        result.FinalHealth = entity.CurrentHealth;
        result.HealthPercentage = entity.HealthPercentage;
        result.WasKilled = entity.CurrentHealth <= 0;
        result.DamageType = damageType;
        
        if (showEffects && result.DamageDealt > 0)
        {
            Debug.Log($"[Damage] {result.DamageDealt} {damageType} damage to {entity.EntityName}");
        }
        
        return result;
    }
    
    public static HealResult TryHeal(this EntityBehaviour entity, int healAmount, bool showEffects = true)
    {
        var result = new HealResult();
        
        if (!entity.IsValidTarget() || healAmount <= 0)
        {
            result.FailureReason = "Invalid target or heal amount";
            return result;
        }
        
        if (entity.CurrentHealth >= entity.MaxHealth)
        {
            result.FailureReason = "Already at full health";
            return result;
        }
        
        int originalHealth = entity.CurrentHealth;
        int maxPossibleHeal = entity.MaxHealth - originalHealth;
        int finalHeal = Mathf.Min(healAmount, maxPossibleHeal);
        
        entity.Heal(finalHeal);
        
        result.Success = true;
        result.HealingDone = finalHeal;
        result.FinalHealth = entity.CurrentHealth;
        result.HealthPercentage = entity.HealthPercentage;
        result.WasFullyHealed = entity.CurrentHealth >= entity.MaxHealth;
        
        if (showEffects && result.HealingDone > 0)
        {
            Debug.Log($"[Heal] {result.HealingDone} HP to {entity.EntityName}");
        }
        
        return result;
    }
    
    public static IEnumerable<EntityBehaviour> FilterByHealth(this IEnumerable<EntityBehaviour> entities, 
        float threshold, HealthComparison comparison)
    {
        if (entities == null) return Enumerable.Empty<EntityBehaviour>();
        
        return entities.Where(entity => entity.IsValidTarget() && comparison switch
        {
            HealthComparison.Below => entity.HealthPercentage < threshold,
            HealthComparison.Above => entity.HealthPercentage > threshold,
            HealthComparison.Equal => Mathf.Abs(entity.HealthPercentage - threshold) < 0.01f,
            HealthComparison.BelowOrEqual => entity.HealthPercentage <= threshold,
            HealthComparison.AboveOrEqual => entity.HealthPercentage >= threshold,
            _ => false
        });
    }
    
    public static IEnumerable<EntityBehaviour> SortBy(this IEnumerable<EntityBehaviour> entities, 
        EntitySortCriteria criteria, bool ascending = true)
    {
        if (entities == null) return Enumerable.Empty<EntityBehaviour>();
        
        var validEntities = entities.Where(e => e.IsValidTarget());
        
        var sorted = criteria switch
        {
            EntitySortCriteria.Health => validEntities.OrderBy(e => e.CurrentHealth),
            EntitySortCriteria.HealthPercentage => validEntities.OrderBy(e => e.HealthPercentage),
            EntitySortCriteria.MaxHealth => validEntities.OrderBy(e => e.MaxHealth),
            EntitySortCriteria.TargetPriority => validEntities.OrderBy(e => e.TargetPriority),
            EntitySortCriteria.Name => validEntities.OrderBy(e => e.EntityName ?? ""),
            _ => validEntities
        };
        
        return ascending ? sorted : sorted.Reverse();
    }
    
    public static EntityBehaviour GetNearestTo(this IEnumerable<EntityBehaviour> entities, Vector3 position)
    {
        if (entities == null) return null;
        
        return entities
            .Where(e => e.IsValidTarget())
            .OrderBy(e => e.GetDistanceTo(position))
            .FirstOrDefault();
    }
    
    public static EntityBehaviour GetWeakest(this IEnumerable<EntityBehaviour> entities)
    {
        if (entities == null) return null;
        
        return entities
            .Where(e => e.IsValidTarget())
            .OrderBy(e => e.HealthPercentage)
            .FirstOrDefault();
    }
    
    public static EntityBehaviour GetStrongest(this IEnumerable<EntityBehaviour> entities)
    {
        if (entities == null) return null;
        
        return entities
            .Where(e => e.IsValidTarget())
            .OrderByDescending(e => e.CurrentHealth)
            .FirstOrDefault();
    }
    
    public static EntityBehaviour GetHighestPriority(this IEnumerable<EntityBehaviour> entities)
    {
        if (entities == null) return null;
        
        return entities
            .Where(e => e.IsValidTarget())
            .OrderByDescending(e => e.TargetPriority)
            .FirstOrDefault();
    }
    
    public static IEnumerable<EntityBehaviour> GetInRadius(this IEnumerable<EntityBehaviour> entities, 
        Vector3 center, float radius)
    {
        if (entities == null) return Enumerable.Empty<EntityBehaviour>();
        
        return entities
            .Where(e => e.IsValidTarget() && e.GetDistanceTo(center) <= radius);
    }
    
    public static EntityBehaviour GetRandom(this IEnumerable<EntityBehaviour> entities)
    {
        if (entities == null) return null;
        
        var validEntities = entities.Where(e => e.IsValidTarget()).ToList();
        return validEntities.Any() ? validEntities[UnityEngine.Random.Range(0, validEntities.Count)] : null;
    }
    
    public static IEnumerable<EntityBehaviour> GetRandom(this IEnumerable<EntityBehaviour> entities, int count)
    {
        if (entities == null || count <= 0) return Enumerable.Empty<EntityBehaviour>();
        
        var validEntities = entities.Where(e => e.IsValidTarget()).ToList();
        if (!validEntities.Any()) return Enumerable.Empty<EntityBehaviour>();
        
        return validEntities.OrderBy(x => System.Guid.NewGuid()).Take(count);
    }
    
    public static float GetDistanceTo(this EntityBehaviour entity, Vector3 position)
    {
        if (!entity.IsValidEntity() || entity.transform == null) 
            return float.MaxValue;
        
        return Vector3.Distance(entity.transform.position, position);
    }
    
    public static float GetDistanceTo(this EntityBehaviour entity, EntityBehaviour other)
    {
        if (!entity.IsValidEntity() || !other.IsValidEntity() || 
            entity.transform == null || other.transform == null)
            return float.MaxValue;
        
        return Vector3.Distance(entity.transform.position, other.transform.position);
    }
    
    public static bool IsInRangeOf(this EntityBehaviour entity, Vector3 position, float range)
    {
        return entity.GetDistanceTo(position) <= range;
    }
    
    public static bool IsInRangeOf(this EntityBehaviour entity, EntityBehaviour other, float range)
    {
        return entity.GetDistanceTo(other) <= range;
    }
    
    public static Vector3 GetDirectionTo(this EntityBehaviour entity, Vector3 position)
    {
        if (!entity.IsValidEntity() || entity.transform == null) 
            return Vector3.zero;
        
        Vector3 direction = position - entity.transform.position;
        return direction.normalized;
    }
    
    public static Vector3 GetDirectionTo(this EntityBehaviour entity, EntityBehaviour other)
    {
        if (!entity.IsValidEntity() || !other.IsValidEntity() || 
            entity.transform == null || other.transform == null) 
            return Vector3.zero;
        
        Vector3 direction = other.transform.position - entity.transform.position;
        return direction.normalized;
    }
    
    public static float CalculateTargetScore(this EntityBehaviour entity, TargetingCriteria criteria)
    {
        if (!entity.IsValidTarget() || criteria == null) return float.MinValue;
        
        float score = 0f;
        
        if (criteria.PreferLowHealth)
        {
            score += (1f - entity.HealthPercentage) * criteria.HealthWeight;
        }
        else if (criteria.PreferHighHealth)
        {
            score += entity.HealthPercentage * criteria.HealthWeight;
        }
        
        if (criteria.ReferencePosition.HasValue)
        {
            float distance = entity.GetDistanceTo(criteria.ReferencePosition.Value);
            float normalizedDistance = Mathf.Clamp01(distance / criteria.MaxDistance);
            
            if (criteria.PreferClose)
                score += (1f - normalizedDistance) * criteria.DistanceWeight;
            else
                score += normalizedDistance * criteria.DistanceWeight;
        }
        
        score += entity.TargetPriority * criteria.PriorityWeight;
        
        if (criteria.PreferredTypes != null && criteria.PreferredTypes.Contains(entity.Type))
        {
            score += criteria.TypeWeight;
        }
        
        return score;
    }
    
    public static EntityBehaviour GetBestTarget(this IEnumerable<EntityBehaviour> entities, 
        TargetingCriteria criteria)
    {
        if (entities == null || criteria == null) return null;
        
        return entities
            .Where(e => e.IsValidTarget())
            .OrderByDescending(e => e.CalculateTargetScore(criteria))
            .FirstOrDefault();
    }
    
    public static bool HasTag(this EntityBehaviour entity, string tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;
        return entity?.Asset?.HasTag(tag) ?? false;
    }
    
    public static bool HasAnyTag(this EntityBehaviour entity, params string[] tags)
    {
        if (tags == null || tags.Length == 0) return false;
        return entity?.Asset?.HasAnyTag(tags) ?? false;
    }
    
    public static bool HasAllTags(this EntityBehaviour entity, params string[] tags)
    {
        if (tags == null || tags.Length == 0) return false;
        return entity?.Asset?.HasAllTags(tags) ?? false;
    }
    
    public static bool IsEnemy(this EntityBehaviour entity)
        => entity.IsValidEntity() && entity.Type == EntityType.Enemy;
    
    public static bool IsUnit(this EntityBehaviour entity)
        => entity.IsValidEntity() && entity.Type == EntityType.Unit;
    
    public static bool IsNeutral(this EntityBehaviour entity)
        => entity.IsValidEntity() && entity.Type == EntityType.Neutral;
    
    public static bool IsBoss(this EntityBehaviour entity)
        => entity?.Asset?.Category == EntityCategory.Boss;
    
    public static bool IsElite(this EntityBehaviour entity)
        => entity?.Asset?.Category == EntityCategory.Elite;
    
    public static bool IsMinion(this EntityBehaviour entity)
        => entity?.Asset?.Category == EntityCategory.Minion;
    
    private static int CalculateFinalDamage(EntityBehaviour entity, int baseDamage, DamageType damageType)
    {
        float multiplier = damageType switch
        {
            DamageType.Fire => GetFireResistance(entity),
            DamageType.True => 1.0f,
            _ => 1.0f
        };
        
        return Mathf.RoundToInt(baseDamage * multiplier);
    }
    
    private static float GetFireResistance(EntityBehaviour entity)
    {
        if (entity.HasTag("FireResistant"))
            return 0.5f;
        if (entity.HasTag("FireVulnerable"))
            return 1.5f;
        
        return 1.0f;
    }
}

public enum EntityHealthStatus
{
    Invalid,
    Dead,
    Critical,
    Low,
    Moderate,
    High,
    Full
}

public enum HealthComparison
{
    Below,
    Above,
    Equal,
    BelowOrEqual,
    AboveOrEqual
}

public enum EntitySortCriteria
{
    Health,
    HealthPercentage,
    MaxHealth,
    TargetPriority,
    Name
}

public class DamageResult
{
    public bool Success { get; set; }
    public int DamageDealt { get; set; }
    public int FinalHealth { get; set; }
    public float HealthPercentage { get; set; }
    public bool WasKilled { get; set; }
    public DamageType DamageType { get; set; }
    public string FailureReason { get; set; } = "";
}

public class HealResult
{
    public bool Success { get; set; }
    public int HealingDone { get; set; }
    public int FinalHealth { get; set; }
    public float HealthPercentage { get; set; }
    public bool WasFullyHealed { get; set; }
    public string FailureReason { get; set; } = "";
}

public enum DamageType
{
    Normal,
    Fire,
    Ice,
    Lightning,
    True
}

public enum EntityType
{
    Unit,
    Enemy,
    Neutral
}

public class TargetingCriteria
{
    public bool PreferLowHealth { get; set; } = true;
    public bool PreferHighHealth { get; set; } = false;
    public bool PreferClose { get; set; } = true;
    public Vector3? ReferencePosition { get; set; }
    public float MaxDistance { get; set; } = 100f;
    public List<EntityType> PreferredTypes { get; set; } = new List<EntityType>();
    
    public float HealthWeight { get; set; } = 1f;
    public float DistanceWeight { get; set; } = 0.5f;
    public float PriorityWeight { get; set; } = 0.3f;
    public float TypeWeight { get; set; } = 0.2f;
}