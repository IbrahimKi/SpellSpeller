using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// Resource Extensions für erweiterte Resource Management und Validation
/// Bietet intelligente Resource Operations, Cost Calculations und Budget Management
/// 
/// USAGE:
/// - resource.IsInCriticalState() für erweiterte Resource-Checks
/// - resources.CanAffordCombined() für Multi-Resource Validation
/// - resource.GetOptimalSpending() für intelligente Resource-Allocation
/// - resource.PredictOutcome() für Resource-Planning
/// </summary>
public static class ResourceExtensions
{
    // ===========================================
    // BASIC RESOURCE VALIDATION EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Erweiterte Resource State Detection
    /// </summary>
    public static bool IsInCriticalState(this Resource resource, float criticalThreshold = 0.2f)
        => resource != null && resource.Percentage <= criticalThreshold && resource.CurrentValue > 0;
    
    /// <summary>
    /// Prüft ob Resource "gesund" ist
    /// </summary>
    public static bool IsHealthy(this Resource resource, float healthyThreshold = 0.6f)
        => resource != null && resource.Percentage >= healthyThreshold;
    
    /// <summary>
    /// Prüft ob Resource vollständig aufgebraucht ist
    /// </summary>
    public static bool IsExhausted(this Resource resource)
        => resource == null || resource.CurrentValue <= 0;
    
    /// <summary>
    /// Prüft ob Resource bei Maximum ist
    /// </summary>
    public static bool IsAtMaximum(this Resource resource)
        => resource != null && resource.CurrentValue >= resource.MaxValue;
    
    /// <summary>
    /// Sichere Resource-Verfügbarkeit mit Minimum-Reserve
    /// </summary>
    public static bool HasAvailable(this Resource resource, int amount, int reserve = 0)
        => resource != null && (resource.CurrentValue - reserve) >= amount;
    
    /// <summary>
    /// Berechnet verfügbare Menge unter Berücksichtigung einer Reserve
    /// </summary>
    public static int GetAvailable(this Resource resource, int reserve = 0)
        => resource != null ? Mathf.Max(0, resource.CurrentValue - reserve) : 0;
    
    // ===========================================
    // ADVANCED RESOURCE STATUS EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Erweiterte Resource Status Analysis
    /// </summary>
    public static ResourceHealth GetResourceHealth(this Resource resource)
    {
        if (resource == null || resource.IsExhausted())
            return ResourceHealth.Dead;
        
        return resource.Percentage switch
        {
            >= 0.9f => ResourceHealth.Excellent,
            >= 0.75f => ResourceHealth.Good,
            >= 0.5f => ResourceHealth.Moderate,
            >= 0.25f => ResourceHealth.Low,
            >= 0.1f => ResourceHealth.Critical,
            _ => ResourceHealth.Dying
        };
    }
    
    /// <summary>
    /// Resource Urgency für Prioritization
    /// </summary>
    public static ResourceUrgency GetUrgency(this Resource resource)
    {
        var health = resource.GetResourceHealth();
        
        return health switch
        {
            ResourceHealth.Dead or ResourceHealth.Dying => ResourceUrgency.Immediate,
            ResourceHealth.Critical => ResourceUrgency.High,
            ResourceHealth.Low => ResourceUrgency.Medium,
            ResourceHealth.Moderate => ResourceUrgency.Low,
            _ => ResourceUrgency.None
        };
    }
    
    /// <summary>
    /// Resource Recovery Priority
    /// </summary>
    public static int GetRecoveryPriority(this Resource resource)
    {
        return resource.GetUrgency() switch
        {
            ResourceUrgency.Immediate => 100,
            ResourceUrgency.High => 75,
            ResourceUrgency.Medium => 50,
            ResourceUrgency.Low => 25,
            _ => 0
        };
    }
    
    /// <summary>
    /// Berechnet optimale Recovery-Menge
    /// </summary>
    public static int GetOptimalRecovery(this Resource resource, int maxRecovery)
    {
        if (resource == null) return 0;
        
        int needed = resource.MaxValue - resource.CurrentValue;
        int recommended = resource.GetResourceHealth() switch
        {
            ResourceHealth.Dead or ResourceHealth.Dying => resource.MaxValue,
            ResourceHealth.Critical => Mathf.Min(needed, resource.MaxValue / 2),
            ResourceHealth.Low => Mathf.Min(needed, resource.MaxValue / 3),
            _ => Mathf.Min(needed, resource.MaxValue / 4)
        };
        
        return Mathf.Min(recommended, maxRecovery);
    }
    
    // ===========================================
    // COST CALCULATION EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Kann Resource bestimmte Kosten decken?
    /// </summary>
    public static bool CanAfford(this Resource resource, ResourceCost cost)
    {
        if (resource == null || cost == null) return false;
        
        return cost.ResourceType switch
        {
            ResourceType.Life => resource.CurrentValue > cost.Amount, // Don't allow death
            ResourceType.Creativity => resource.CurrentValue >= cost.Amount,
            _ => resource.CurrentValue >= cost.Amount
        };
    }
    
    /// <summary>
    /// Kann Resource mehrere Kosten gleichzeitig decken?
    /// </summary>
    public static bool CanAffordMultiple(this Resource resource, IEnumerable<ResourceCost> costs)
    {
        if (resource == null || costs == null) return false;
        
        var relevantCosts = costs.Where(c => c != null && IsRelevantResourceType(resource, c.ResourceType));
        int totalCost = relevantCosts.Sum(c => c.Amount);
        
        return resource.CanAfford(new ResourceCost { ResourceType = GetResourceType(resource), Amount = totalCost });
    }
    
    /// <summary>
    /// Berechnet Restkosten nach Partial Payment
    /// </summary>
    public static ResourceCost CalculateRemainingCost(this Resource resource, ResourceCost originalCost, int paid)
    {
        if (originalCost == null) return null;
        
        return new ResourceCost
        {
            ResourceType = originalCost.ResourceType,
            Amount = Mathf.Max(0, originalCost.Amount - paid),
            Priority = originalCost.Priority
        };
    }
    
    /// <summary>
    /// Sichere Cost Application mit Validation
    /// </summary>
    public static bool TryApplyCost(this Resource resource, ResourceCost cost, bool allowPartial = false)
    {
        if (resource == null || cost == null) return false;
        
        if (resource.CanAfford(cost))
        {
            resource.ModifyBy(-cost.Amount);
            return true;
        }
        else if (allowPartial && resource.CurrentValue > 0)
        {
            int availableAmount = resource.GetAvailable();
            resource.ModifyBy(-availableAmount);
            return false; // Partial payment
        }
        
        return false;
    }
    
    // ===========================================
    // RESOURCE OPTIMIZATION EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Berechnet optimale Spending-Strategy
    /// </summary>
    public static SpendingStrategy GetOptimalSpending(this Resource resource, IEnumerable<ResourceCost> potentialCosts)
    {
        if (resource == null || potentialCosts == null)
            return new SpendingStrategy();
        
        var availableFunds = resource.GetAvailable(GetRecommendedReserve(resource));
        var sortedCosts = potentialCosts
            .Where(c => c != null && IsRelevantResourceType(resource, c.ResourceType))
            .OrderByDescending(c => c.Priority)
            .ThenBy(c => c.Amount);
        
        var strategy = new SpendingStrategy
        {
            AvailableFunds = availableFunds,
            RecommendedReserve = GetRecommendedReserve(resource)
        };
        
        int remainingFunds = availableFunds;
        
        foreach (var cost in sortedCosts)
        {
            if (remainingFunds >= cost.Amount)
            {
                strategy.AffordableCosts.Add(cost);
                remainingFunds -= cost.Amount;
            }
            else if (cost.Priority >= ResourcePriority.High)
            {
                strategy.UnaffordableHighPriority.Add(cost);
            }
            else
            {
                strategy.UnaffordableLowPriority.Add(cost);
            }
        }
        
        strategy.RemainingFunds = remainingFunds;
        return strategy;
    }
    
    /// <summary>
    /// Resource Budget Planning
    /// </summary>
    public static ResourceBudget CreateBudget(this Resource resource, int plannedSpending, int emergencyReserve = -1)
    {
        if (resource == null) return new ResourceBudget();
        
        if (emergencyReserve < 0)
            emergencyReserve = GetRecommendedReserve(resource);
        
        var budget = new ResourceBudget
        {
            TotalAvailable = resource.CurrentValue,
            EmergencyReserve = emergencyReserve,
            PlannedSpending = plannedSpending,
            AvailableForSpending = resource.GetAvailable(emergencyReserve)
        };
        
        budget.BudgetStatus = budget.PlannedSpending <= budget.AvailableForSpending ? 
            BudgetStatus.Balanced : BudgetStatus.Overbudget;
        
        budget.Shortfall = Mathf.Max(0, budget.PlannedSpending - budget.AvailableForSpending);
        budget.Surplus = Mathf.Max(0, budget.AvailableForSpending - budget.PlannedSpending);
        
        return budget;
    }
    
    /// <summary>
    /// Predicts Resource state after planned operations
    /// </summary>
    public static ResourceOutcome PredictOutcome(this Resource resource, IEnumerable<ResourceOperation> operations)
    {
        if (resource == null) return new ResourceOutcome();
        
        var outcome = new ResourceOutcome
        {
            InitialValue = resource.CurrentValue,
            InitialHealth = resource.GetResourceHealth()
        };
        
        int projectedValue = resource.CurrentValue;
        
        foreach (var operation in operations ?? Enumerable.Empty<ResourceOperation>())
        {
            if (operation != null && IsRelevantResourceType(resource, operation.ResourceType))
            {
                projectedValue += operation.Amount; // Can be negative for costs
                outcome.Operations.Add(operation);
            }
        }
        
        projectedValue = Mathf.Clamp(projectedValue, 0, resource.MaxValue);
        
        outcome.ProjectedValue = projectedValue;
        outcome.ProjectedPercentage = resource.MaxValue > 0 ? (float)projectedValue / resource.MaxValue : 0f;
        outcome.ProjectedHealth = GetHealthFromPercentage(outcome.ProjectedPercentage);
        
        // FIX: Safe enum arithmetic
        int healthChange = (int)outcome.ProjectedHealth - (int)outcome.InitialHealth;
        outcome.HealthChange = healthChange;
        
        outcome.IsImprovement = outcome.ProjectedValue > resource.CurrentValue;
        outcome.IsCriticalChange = Math.Abs(healthChange) >= 2;
        
        return outcome;
    }
    
    // ===========================================
    // MULTI-RESOURCE EXTENSIONS
    // ===========================================
    
    /// <summary>
    /// Combined Resource Affordability Check
    /// </summary>
    public static bool CanAffordCombined(this IEnumerable<Resource> resources, IEnumerable<ResourceCost> costs)
    {
        if (resources == null || costs == null) return false;
        
        var resourceDict = resources.Where(r => r != null).ToDictionary(r => GetResourceType(r), r => r);
        
        foreach (var cost in costs.Where(c => c != null))
        {
            if (resourceDict.TryGetValue(cost.ResourceType, out Resource resource))
            {
                if (!resource.CanAfford(cost))
                    return false;
            }
            else
            {
                return false; // Required resource not available
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Multi-Resource Health Assessment
    /// </summary>
    public static OverallResourceHealth GetOverallHealth(this IEnumerable<Resource> resources)
    {
        if (resources == null) return new OverallResourceHealth();
        
        var validResources = resources.Where(r => r != null).ToList();
        if (!validResources.Any()) return new OverallResourceHealth();
        
        var healthStats = validResources.Select(r => r.GetResourceHealth()).ToList();
        
        return new OverallResourceHealth
        {
            TotalResources = validResources.Count,
            CriticalResources = healthStats.Count(h => h <= ResourceHealth.Critical),
            LowResources = healthStats.Count(h => h == ResourceHealth.Low),
            HealthyResources = healthStats.Count(h => h >= ResourceHealth.Good),
            AveragePercentage = validResources.Average(r => r.Percentage),
            WorstResource = validResources.OrderBy(r => r.GetResourceHealth()).First(),
            BestResource = validResources.OrderByDescending(r => r.GetResourceHealth()).First()
        };
    }
    
    /// <summary>
    /// Resource Portfolio Optimization
    /// </summary>
    public static ResourcePortfolio OptimizePortfolio(this IEnumerable<Resource> resources, IEnumerable<ResourceCost> plannedCosts)
    {
        var portfolio = new ResourcePortfolio();
        
        var validResources = resources?.Where(r => r != null).ToList() ?? new List<Resource>();
        var validCosts = plannedCosts?.Where(c => c != null).ToList() ?? new List<ResourceCost>();
        
        portfolio.Resources = validResources;
        portfolio.TotalValue = validResources.Sum(r => r.CurrentValue);
        portfolio.OverallHealth = validResources.GetOverallHealth();
        
        // Group costs by resource type
        var costsByType = validCosts.GroupBy(c => c.ResourceType)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Amount));
        
        foreach (var resource in validResources)
        {
            var resourceType = GetResourceType(resource);
            var plannedSpending = costsByType.GetValueOrDefault(resourceType, 0);
            
            var recommendation = new ResourceRecommendation
            {
                ResourceType = resourceType,
                CurrentHealth = resource.GetResourceHealth(),
                PlannedSpending = plannedSpending,
                RecommendedAction = GetRecommendedAction(resource, plannedSpending)
            };
            
            portfolio.Recommendations.Add(recommendation);
        }
        
        return portfolio;
    }
    
    // ===========================================
    // HELPER METHODS
    // ===========================================
    
    private static int GetRecommendedReserve(Resource resource)
    {
        return resource.GetResourceHealth() switch
        {
            ResourceHealth.Excellent => resource.MaxValue / 10, // 10% reserve
            ResourceHealth.Good => resource.MaxValue / 8,       // 12.5% reserve
            ResourceHealth.Moderate => resource.MaxValue / 6,   // 16.7% reserve
            ResourceHealth.Low => resource.MaxValue / 4,        // 25% reserve
            _ => resource.MaxValue / 3                          // 33% reserve
        };
    }
    
    private static ResourceType GetResourceType(Resource resource)
    {
        // This would need to be implemented based on your specific Resource setup
        // For now, returning a default - you'd implement this based on how you identify resource types
        return ResourceType.Creativity; // Placeholder
    }
    
    private static bool IsRelevantResourceType(Resource resource, ResourceType type)
    {
        return GetResourceType(resource) == type;
    }
    
    private static ResourceHealth GetHealthFromPercentage(float percentage)
    {
        return percentage switch
        {
            >= 0.9f => ResourceHealth.Excellent,
            >= 0.75f => ResourceHealth.Good,
            >= 0.5f => ResourceHealth.Moderate,
            >= 0.25f => ResourceHealth.Low,
            >= 0.1f => ResourceHealth.Critical,
            > 0f => ResourceHealth.Dying,
            _ => ResourceHealth.Dead
        };
    }
    
    private static ResourceAction GetRecommendedAction(Resource resource, int plannedSpending)
    {
        var health = resource.GetResourceHealth();
        var wouldBeAffordable = resource.GetAvailable() >= plannedSpending;
        
        if (health <= ResourceHealth.Critical)
            return ResourceAction.RecoverImmediately;
        else if (health == ResourceHealth.Low && plannedSpending > resource.GetAvailable(GetRecommendedReserve(resource)))
            return ResourceAction.RecoverBeforeSpending;
        else if (!wouldBeAffordable)
            return ResourceAction.ReduceSpending;
        else if (health >= ResourceHealth.Good && resource.GetAvailable() > plannedSpending * 2)
            return ResourceAction.ConsiderIncreaseSpending;
        else
            return ResourceAction.Maintain;
    }
}

// ===========================================
// SUPPORTING ENUMS & CLASSES
// ===========================================

public enum ResourceHealth
{
    Dead = 0,
    Dying = 1,
    Critical = 2,
    Low = 3,
    Moderate = 4,
    Good = 5,
    Excellent = 6
}

public enum ResourceUrgency
{
    None,
    Low,
    Medium,
    High,
    Immediate
}

public enum ResourcePriority
{
    VeryLow = 1,
    Low = 2,
    Medium = 3,
    High = 4,
    Critical = 5
}

public enum ResourceAction
{
    Maintain,
    RecoverImmediately,
    RecoverBeforeSpending,
    ReduceSpending,
    ConsiderIncreaseSpending,
    Optimize
}

public enum BudgetStatus
{
    Balanced,
    Overbudget,
    Underbudget
}

public class ResourceCost
{
    public ResourceType ResourceType { get; set; }
    public int Amount { get; set; }
    public ResourcePriority Priority { get; set; } = ResourcePriority.Medium;
    public string Description { get; set; } = "";
    
    public static ResourceCost Create(ResourceType type, int amount, ResourcePriority priority = ResourcePriority.Medium)
        => new ResourceCost { ResourceType = type, Amount = amount, Priority = priority };
}

public class ResourceOperation
{
    public ResourceType ResourceType { get; set; }
    public int Amount { get; set; } // Positive for gains, negative for costs
    public string Description { get; set; } = "";
    public float Probability { get; set; } = 1f; // For uncertain operations
}

public class SpendingStrategy
{
    public int AvailableFunds { get; set; }
    public int RecommendedReserve { get; set; }
    public int RemainingFunds { get; set; }
    public List<ResourceCost> AffordableCosts { get; set; } = new List<ResourceCost>();
    public List<ResourceCost> UnaffordableHighPriority { get; set; } = new List<ResourceCost>();
    public List<ResourceCost> UnaffordableLowPriority { get; set; } = new List<ResourceCost>();
    
    public bool HasUnaffordableCritical => UnaffordableHighPriority.Any(c => c.Priority >= ResourcePriority.Critical);
    public int TotalAffordableCost => AffordableCosts.Sum(c => c.Amount);
    public int TotalUnaffordableCost => UnaffordableHighPriority.Sum(c => c.Amount) + UnaffordableLowPriority.Sum(c => c.Amount);
}

public class ResourceBudget
{
    public int TotalAvailable { get; set; }
    public int EmergencyReserve { get; set; }
    public int PlannedSpending { get; set; }
    public int AvailableForSpending { get; set; }
    public int Shortfall { get; set; }
    public int Surplus { get; set; }
    public BudgetStatus BudgetStatus { get; set; }
    
    public bool CanExecutePlan => BudgetStatus == BudgetStatus.Balanced || BudgetStatus == BudgetStatus.Underbudget;
    public float UtilizationRate => AvailableForSpending > 0 ? (float)PlannedSpending / AvailableForSpending : 0f;
}

public class ResourceOutcome
{
    public int InitialValue { get; set; }
    public ResourceHealth InitialHealth { get; set; }
    public int ProjectedValue { get; set; }
    public float ProjectedPercentage { get; set; }
    public ResourceHealth ProjectedHealth { get; set; }
    public int HealthChange { get; set; }  // FIX: Changed from ResourceHealth to int
    public bool IsImprovement { get; set; }
    public bool IsCriticalChange { get; set; }
    public List<ResourceOperation> Operations { get; set; } = new List<ResourceOperation>();
    
    public bool IsRisky => ProjectedHealth <= ResourceHealth.Critical;
    public bool IsSignificantChange => Math.Abs(ProjectedValue - InitialValue) > InitialValue * 0.25f;
}

public class OverallResourceHealth
{
    public int TotalResources { get; set; }
    public int CriticalResources { get; set; }
    public int LowResources { get; set; }
    public int HealthyResources { get; set; }
    public float AveragePercentage { get; set; }
    public Resource WorstResource { get; set; }
    public Resource BestResource { get; set; }
    
    public bool IsInCrisis => CriticalResources > 0;
    public bool NeedsAttention => CriticalResources + LowResources >= TotalResources / 2;
    public float HealthScore => TotalResources > 0 ? (float)HealthyResources / TotalResources : 0f;
}

public class ResourceRecommendation
{
    public ResourceType ResourceType { get; set; }
    public ResourceHealth CurrentHealth { get; set; }
    public int PlannedSpending { get; set; }
    public ResourceAction RecommendedAction { get; set; }
    public string Reasoning { get; set; } = "";
    public int Priority { get; set; }
}

public class ResourcePortfolio
{
    public List<Resource> Resources { get; set; } = new List<Resource>();
    public int TotalValue { get; set; }
    public OverallResourceHealth OverallHealth { get; set; }
    public List<ResourceRecommendation> Recommendations { get; set; } = new List<ResourceRecommendation>();
    
    public ResourceRecommendation GetHighestPriorityRecommendation()
        => Recommendations.OrderByDescending(r => r.Priority).FirstOrDefault();
}

// ===========================================
// VERWENDUNGS-BEISPIELE
// ===========================================

/*
// BASIC RESOURCE VALIDATION:
public void CheckResourceHealth()
{
    var life = CombatManager.Instance.Life;
    var creativity = CombatManager.Instance.Creativity;
    
    if (life.IsInCriticalState())
    {
        Debug.LogWarning("Life is critical!");
        // Prioritize healing
    }
    
    if (creativity.GetUrgency() >= ResourceUrgency.High)
    {
        Debug.LogWarning("Creativity needs attention!");
        // Consider recovery actions
    }
}

// COST MANAGEMENT:
public bool TryExecuteAction(ActionCost actionCost)
{
    var resources = new[] { CombatManager.Instance.Life, CombatManager.Instance.Creativity };
    var costs = actionCost.GetResourceCosts();
    
    if (resources.CanAffordCombined(costs))
    {
        // Execute action
        foreach (var cost in costs)
        {
            var resource = GetResourceByType(cost.ResourceType);
            resource.TryApplyCost(cost);
        }
        return true;
    }
    return false;
}

// RESOURCE PLANNING:
public void PlanResourceUsage()
{
    var creativity = CombatManager.Instance.Creativity;
    var plannedCosts = new[]
    {
        ResourceCost.Create(ResourceType.Creativity, 2, ResourcePriority.High),
        ResourceCost.Create(ResourceType.Creativity, 1, ResourcePriority.Medium),
        ResourceCost.Create(ResourceType.Creativity, 3, ResourcePriority.Low)
    };
    
    var strategy = creativity.GetOptimalSpending(plannedCosts);
    
    Debug.Log($"Can afford {strategy.AffordableCosts.Count} actions");
    Debug.Log($"Remaining funds: {strategy.RemainingFunds}");
    
    if (strategy.HasUnaffordableCritical)
    {
        Debug.LogWarning("Cannot afford critical actions!");
    }
}

// RESOURCE OUTCOME PREDICTION:
public void PredictTurnOutcome()
{
    var creativity = CombatManager.Instance.Creativity;
    var operations = new[]
    {
        new ResourceOperation { ResourceType = ResourceType.Creativity, Amount = -2, Description = "Play Cards" },
        new ResourceOperation { ResourceType = ResourceType.Creativity, Amount = -1, Description = "Draw Card" },
        new ResourceOperation { ResourceType = ResourceType.Creativity, Amount = 3, Description = "Turn Reset" }
    };
    
    var outcome = creativity.PredictOutcome(operations);
    
    Debug.Log($"Current: {outcome.InitialValue} -> Projected: {outcome.ProjectedValue}");
    Debug.Log($"Health change: {outcome.HealthChange}");
    
    if (outcome.IsRisky)
    {
        Debug.LogWarning("This plan would put creativity in critical state!");
    }
}

// MULTI-RESOURCE PORTFOLIO:
public void AnalyzeResourcePortfolio()
{
    var resources = new[] 
    { 
        CombatManager.Instance.Life, 
        CombatManager.Instance.Creativity 
    };
    
    var plannedCosts = GetAllPlannedCosts();
    var portfolio = resources.OptimizePortfolio(plannedCosts);
    
    Debug.Log($"Portfolio health score: {portfolio.OverallHealth.HealthScore:P0}");
    
    if (portfolio.OverallHealth.IsInCrisis)
    {
        Debug.LogError("Resource portfolio in crisis!");
        
        var priority = portfolio.GetHighestPriorityRecommendation();
        Debug.Log($"Priority action: {priority.RecommendedAction} for {priority.ResourceType}");
    }
}

// UI INTEGRATION:
public void UpdateResourceUI()
{
    var life = CombatManager.Instance.Life;
    var creativity = CombatManager.Instance.Creativity;
    
    // Health-based color coding
    lifeBar.color = life.GetResourceHealth() switch
    {
        ResourceHealth.Critical or ResourceHealth.Dying => Color.red,
        ResourceHealth.Low => Color.orange,
        ResourceHealth.Moderate => Color.yellow,
        _ => Color.green
    };
    
    // Urgency indicators
    if (creativity.GetUrgency() >= ResourceUrgency.High)
    {
        creativityAlert.SetActive(true);
        creativityAlert.GetComponent<Text>().text = "LOW CREATIVITY!";
    }
    
    // Budget display
    var budget = creativity.CreateBudget(GetPlannedSpending());
    budgetText.text = $"Budget: {budget.PlannedSpending}/{budget.AvailableForSpending}";
    budgetText.color = budget.CanExecutePlan ? Color.green : Color.red;
}
*/