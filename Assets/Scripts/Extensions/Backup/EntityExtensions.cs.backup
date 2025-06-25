using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using GameCore.Enums;
using GameCore.Data;

/// <summary>
/// EntityExtensions - Spezialisierte Extensions für Entity-System  
/// OPTIMIZED: Duplikate entfernt, Performance verbessert, konsistente API
/// DEPENDENCIES: CoreExtensions, SharedEnums
/// </summary>
public static class EntityExtensions
{
    // === ENTITY VALIDATION (Basis für alle Entity-Operations) ===
    
    /// <summary>
    /// Entity ist gültiges Angriffsziel
    /// PERFORMANCE: O(1) - properties sind gecacht
    /// </summary>
    public static bool IsValidTarget(this EntityBehaviour entity)
        => entity.IsValidReference() && entity.IsAlive && entity.IsTargetable;
    
    /// <summary>
    /// Entity Referenz ist gültig (auch für tote Entities)
    /// </summary>
    public static bool IsValidEntity(this EntityBehaviour entity)
        => entity.IsValidReference();
    
    /// <summary>
    /// Entity ist aktiv in Scene
    /// </summary>
    public static bool IsActiveEntity(this EntityBehaviour entity)
        => entity.IsValidEntity() && entity.IsActiveAndValid();

    // === TYPE CHECKING (Vereinfachte Type-Checks) ===
    
    /// <summary>
    /// Entity ist Enemy
    /// </summary>
    public static bool IsEnemy(this EntityBehaviour entity)
        => entity.IsValidEntity() && entity.Type == EntityType.Enemy;
    
    /// <summary>
    /// Entity ist Unit (Spieler-kontrolliert)
    /// FIXED: Direkte Implementierung statt Asset-Abhängigkeit
    /// </summary>
    public static bool IsUnit(this EntityBehaviour entity)
        => entity.IsValidEntity() && entity.Type == EntityType.Unit;
    
    /// <summary>
    /// Entity ist Neutral
    /// </summary>
    public static bool IsNeutral(this EntityBehaviour entity)
        => entity.IsValidEntity() && entity.Type == EntityType.Neutral;
    
    /// <summary>
    /// Entity ist Boss
    /// </summary>
    public static bool IsBoss(this EntityBehaviour entity)
        => entity?.Asset?.Category == EntityCategory.Boss;
    
    /// <summary>
    /// Entity ist Elite
    /// </summary>
    public static bool IsElite(this EntityBehaviour entity)
        => entity?.Asset?.Category == EntityCategory.Elite;
    
    /// <summary>
    /// Entity ist Minion
    /// </summary>
    public static bool IsMinion(this EntityBehaviour entity)
        => entity?.Asset?.Category == EntityCategory.Minion;

    // === HEALTH STATUS ANALYSIS ===
    
    /// <summary>
    /// Detaillierter Health Status
    /// PERFORMANCE: Switch-Expression ist schneller als If-Kette
    /// </summary>
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
    
    /// <summary>
    /// Health in spezifischem Range
    /// </summary>
    public static bool IsHealthInRange(this EntityBehaviour entity, float min, float max)
        => entity.IsValidTarget() && entity.HealthPercentage >= min && entity.HealthPercentage <= max;
    
    /// <summary>
    /// Low Health Check mit konfigurierbarem Threshold
    /// </summary>
    public static bool IsLowHealth(this EntityBehaviour entity, float threshold = 0.35f)
        => entity.IsValidTarget() && entity.HealthPercentage <= threshold;
    
    /// <summary>
    /// Critical Health Check
    /// </summary>
    public static bool IsCriticalHealth(this EntityBehaviour entity, float threshold = 0.15f)
        => entity.IsValidTarget() && entity.HealthPercentage <= threshold;
    
    /// <summary>
    /// Near Death Check
    /// </summary>
    public static bool IsNearDeath(this EntityBehaviour entity, float threshold = 0.05f)
        => entity.IsValidTarget() && entity.HealthPercentage <= threshold;
    
    /// <summary>
    /// Healthy Check
    /// </summary>
    public static bool IsHealthy(this EntityBehaviour entity, float threshold = 0.8f)
        => entity.IsValidTarget() && entity.HealthPercentage >= threshold;

    // === ENHANCED DAMAGE & HEALING ===
    
    /// <summary>
    /// Erweiterte Damage-Funktion mit Resistenzen und Effekten
    /// PERFORMANCE: Inline damage calculation ohne separate Methode
    /// </summary>
    public static DamageResult TryDamageWithEffects(this EntityBehaviour entity, int damage, 
        DamageType damageType = DamageType.Normal, bool showEffects = true)
    {
        var result = new DamageResult { DamageType = damageType };
        
        if (!entity.IsValidTarget() || damage <= 0)
        {
            result.FailureReason = "Invalid target or damage amount";
            return result;
        }
        
        int originalHealth = entity.CurrentHealth;
        
        // Damage calculation with resistances
        float multiplier = damageType switch
        {
            DamageType.Fire when entity.HasTag("FireResistant") => 0.5f,
            DamageType.Fire when entity.HasTag("FireVulnerable") => 1.5f,
            DamageType.True => 1.0f, // True damage ignores resistances
            _ => 1.0f
        };
        
        int finalDamage = Mathf.RoundToInt(damage * multiplier);
        
        // Apply damage
        entity.TakeDamage(finalDamage, damageType);
        
        // Calculate results
        result.Success = true;
        result.DamageDealt = originalHealth - entity.CurrentHealth;
        result.FinalHealth = entity.CurrentHealth;
        result.HealthPercentage = entity.HealthPercentage;
        result.WasKilled = entity.CurrentHealth <= 0;
        
        if (showEffects && result.DamageDealt > 0)
        {
            entity.LogDebug($"Took {result.DamageDealt} {damageType} damage");
        }
        
        return result;
    }
    
    /// <summary>
    /// Erweiterte Heal-Funktion mit Overheal-Protection
    /// </summary>
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
            entity.LogDebug($"Healed for {result.HealingDone} HP");
        }
        
        return result;
    }

    // === COLLECTION FILTERING (Performance-optimierte LINQ) ===
    
    /// <summary>
    /// Filter Collection nach Health-Kriterien
    /// PERFORMANCE: Single LINQ pass
    /// </summary>
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
    
    /// <summary>
    /// Sortierung nach verschiedenen Kriterien
    /// </summary>
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

    // === SPATIAL OPERATIONS (Distance & Position) ===
    
    /// <summary>
    /// Distanz zu Position
    /// PERFORMANCE: Vector3.Distance ist optimiert
    /// </summary>
    public static float GetDistanceTo(this EntityBehaviour entity, Vector3 position)
        => !entity.IsValidEntity() ? float.MaxValue : Vector3.Distance(entity.transform.position, position);
    
    /// <summary>
    /// Distanz zu anderer Entity
    /// </summary>
    public static float GetDistanceTo(this EntityBehaviour entity, EntityBehaviour other)
        => !entity.IsValidEntity() || !other.IsValidEntity() ? float.MaxValue 
         : Vector3.Distance(entity.transform.position, other.transform.position);
    
    /// <summary>
    /// Entity in Range einer Position
    /// </summary>
    public static bool IsInRangeOf(this EntityBehaviour entity, Vector3 position, float range)
        => entity.GetDistanceTo(position) <= range;
    
    /// <summary>
    /// Entity in Range einer anderen Entity
    /// </summary>
    public static bool IsInRangeOf(this EntityBehaviour entity, EntityBehaviour other, float range)
        => entity.GetDistanceTo(other) <= range;

    // === SMART SELECTION (AI & Targeting) ===
    
    /// <summary>
    /// Nächste Entity zu Position finden
    /// PERFORMANCE: Single LINQ pass mit OrderBy
    /// </summary>
    public static EntityBehaviour GetNearestTo(this IEnumerable<EntityBehaviour> entities, Vector3 position)
        => entities?.Where(e => e.IsValidTarget()).OrderBy(e => e.GetDistanceTo(position)).FirstOrDefault();
    
    /// <summary>
    /// Schwächste Entity (niedrigste Health%)
    /// </summary>
    public static EntityBehaviour GetWeakest(this IEnumerable<EntityBehaviour> entities)
        => entities?.Where(e => e.IsValidTarget()).OrderBy(e => e.HealthPercentage).FirstOrDefault();
    
    /// <summary>
    /// Stärkste Entity (höchste absolute Health)
    /// </summary>
    public static EntityBehaviour GetStrongest(this IEnumerable<EntityBehaviour> entities)
        => entities?.Where(e => e.IsValidTarget()).OrderByDescending(e => e.CurrentHealth).FirstOrDefault();
    
    /// <summary>
    /// Höchste Target-Priority
    /// </summary>
    public static EntityBehaviour GetHighestPriority(this IEnumerable<EntityBehaviour> entities)
        => entities?.Where(e => e.IsValidTarget()).OrderByDescending(e => e.TargetPriority).FirstOrDefault();
    
    /// <summary>
    /// Entities in Radius um Position
    /// </summary>
    public static IEnumerable<EntityBehaviour> GetInRadius(this IEnumerable<EntityBehaviour> entities, 
        Vector3 center, float radius)
        => entities?.Where(e => e.IsValidTarget() && e.GetDistanceTo(center) <= radius) ?? Enumerable.Empty<EntityBehaviour>();
    
    /// <summary>
    /// Zufällige Entity aus Collection
    /// PERFORMANCE: ToList nur wenn nötig
    /// </summary>
    public static EntityBehaviour GetRandom(this IEnumerable<EntityBehaviour> entities)
    {
        var validEntities = entities?.Where(e => e.IsValidTarget()).ToList();
        return validEntities?.Count > 0 ? validEntities[UnityEngine.Random.Range(0, validEntities.Count)] : null;
    }
    
    /// <summary>
    /// Mehrere zufällige Entities
    /// </summary>
    public static IEnumerable<EntityBehaviour> GetRandom(this IEnumerable<EntityBehaviour> entities, int count)
    {
        if (entities == null || count <= 0) return Enumerable.Empty<EntityBehaviour>();
        
        var validEntities = entities.Where(e => e.IsValidTarget()).ToList();
        return validEntities.Count > 0 ? validEntities.OrderBy(x => System.Guid.NewGuid()).Take(count) : Enumerable.Empty<EntityBehaviour>();
    }

    // === ADVANCED TARGETING (Für AI-Systeme) ===
    
    /// <summary>
    /// Target-Score basierend auf komplexen Kriterien berechnen
    /// PERFORMANCE: Inline calculations, keine separaten Methodenaufrufe
    /// </summary>
    public static float CalculateTargetScore(this EntityBehaviour entity, TargetingCriteria criteria)
    {
        if (!entity.IsValidTarget() || criteria == null) return float.MinValue;
        
        float score = 0f;
        
        // Health scoring
        if (criteria.PreferLowHealth)
            score += (1f - entity.HealthPercentage) * criteria.HealthWeight;
        else if (criteria.PreferHighHealth)
            score += entity.HealthPercentage * criteria.HealthWeight;
        
        // Distance scoring
        if (criteria.ReferencePosition.HasValue)
        {
            float distance = entity.GetDistanceTo(criteria.ReferencePosition.Value);
            float normalizedDistance = Mathf.Clamp01(distance / criteria.MaxDistance);
            
            score += criteria.PreferClose 
                ? (1f - normalizedDistance) * criteria.DistanceWeight
                : normalizedDistance * criteria.DistanceWeight;
        }
        
        // Priority scoring
        score += entity.TargetPriority * criteria.PriorityWeight;
        
        // Type preference - FIXED: Using LINQ Contains
        if (criteria.PreferredTypes?.Any(t => (GameCore.Enums.EntityType)t == (GameCore.Enums.EntityType)entity.Type) == true)
            score += criteria.TypeWeight;
        
        return score;
    }
    
    /// <summary>
    /// Beste Entity basierend auf Targeting-Kriterien
    /// </summary>
    public static EntityBehaviour GetBestTarget(this IEnumerable<EntityBehaviour> entities, 
        TargetingCriteria criteria)
        => entities?.Where(e => e.IsValidTarget()).OrderByDescending(e => e.CalculateTargetScore(criteria)).FirstOrDefault();

    // === TAG SYSTEM (Für flexible Entity-Klassifikation) ===
    
    /// <summary>
    /// Entity hat spezifischen Tag
    /// </summary>
    public static bool HasTag(this EntityBehaviour entity, string tag)
        => !string.IsNullOrEmpty(tag) && entity?.Asset?.HasTag(tag) == true;
    
    /// <summary>
    /// Entity hat einen der Tags
    /// PERFORMANCE: Short-circuit evaluation
    /// </summary>
    public static bool HasAnyTag(this EntityBehaviour entity, params string[] tags)
        => tags?.Length > 0 && entity?.Asset?.HasAnyTag(tags) == true;
    
    /// <summary>
    /// Entity hat alle Tags
    /// </summary>
    public static bool HasAllTags(this EntityBehaviour entity, params string[] tags)
        => tags?.Length > 0 && entity?.Asset?.HasAllTags(tags) == true;
}