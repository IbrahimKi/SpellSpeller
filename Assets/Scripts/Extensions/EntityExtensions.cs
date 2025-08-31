using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class EntityExtensions
{
    // Type checks
    public static bool IsEnemy(this EntityBehaviour entity)
        => entity.IsValid() && entity.Type == EntityType.Enemy;
    
    public static bool IsUnit(this EntityBehaviour entity)
        => entity.IsValid() && entity.Type == EntityType.Unit;
    
    // Status checks
    public static bool IsBoss(this EntityBehaviour entity)
        => entity.IsValid() && entity.Category == EntityCategory.Boss;
    
    public static bool IsElite(this EntityBehaviour entity)
        => entity.IsValid() && entity.Category == EntityCategory.Elite;
    
    public static bool IsCriticalHealth(this EntityBehaviour entity)
        => entity.IsValid() && entity.HealthPercentage < 0.2f;
    
    // Collection queries
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
            .OrderBy(e => Vector3.Distance(e.transform.position, position))
            .FirstOrDefault();
    
    public static EntityBehaviour GetHighestPriority(this IEnumerable<EntityBehaviour> entities)
        => entities?.Where(e => e.IsValidTarget())
            .OrderByDescending(e => e.TargetPriority)
            .FirstOrDefault();
    
    public static IEnumerable<EntityBehaviour> FilterByHealth(this IEnumerable<EntityBehaviour> entities, 
        float threshold, HealthComparison comparison)
    {
        return comparison switch
        {
            HealthComparison.Below => entities.Where(e => e.HealthPercentage < threshold),
            HealthComparison.Above => entities.Where(e => e.HealthPercentage > threshold),
            HealthComparison.BelowOrEqual => entities.Where(e => e.HealthPercentage <= threshold),
            HealthComparison.AboveOrEqual => entities.Where(e => e.HealthPercentage >= threshold),
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
            score += entity.HealthPercentage * criteria.HealthWeight;
        else
            score += (1f - entity.HealthPercentage) * criteria.HealthWeight;
        
        if (criteria.PreferClose && criteria.ReferencePosition != null)
        {
            float distance = Vector3.Distance(entity.transform.position, criteria.ReferencePosition);
            score += distance * criteria.DistanceWeight;
        }
        
        score -= entity.TargetPriority * criteria.PriorityWeight;
        
        return score;
    }
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