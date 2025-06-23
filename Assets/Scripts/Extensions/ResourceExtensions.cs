using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public static class ResourceExtensions
{
    public static bool IsInCriticalState(this Resource resource, float criticalThreshold = 0.2f)
        => resource != null && resource.Percentage <= criticalThreshold && resource.CurrentValue > 0;
    
    public static bool IsHealthy(this Resource resource, float healthyThreshold = 0.6f)
        => resource != null && resource.Percentage >= healthyThreshold;
    
    public static bool IsExhausted(this Resource resource)
        => resource == null || resource.CurrentValue <= 0;
    
    public static bool IsAtMaximum(this Resource resource)
        => resource != null && resource.CurrentValue >= resource.MaxValue;
    
    public static bool HasAvailable(this Resource resource, int amount, int reserve = 0)
        => resource != null && (resource.CurrentValue - reserve) >= amount;
    
    public static int GetAvailable(this Resource resource, int reserve = 0)
        => resource != null ? Mathf.Max(0, resource.CurrentValue - reserve) : 0;
    
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
    
    public static bool CanAfford(this Resource resource, ResourceCost cost)
    {
        if (resource == null || cost == null) return false;
        
        return cost.ResourceType switch
        {
            ResourceType.Life => resource.CurrentValue > cost.Amount,
            ResourceType.Creativity => resource.CurrentValue >= cost.Amount,
            _ => resource.CurrentValue >= cost.Amount
        };
    }
    
    public static bool CanAffordMultiple(this Resource resource, IEnumerable<ResourceCost> costs)
    {
        if (resource == null || costs == null) return false;
        
        var relevantCosts = costs.Where(c => c != null && IsRelevantResourceType(resource, c.ResourceType));
        int totalCost = relevantCosts.Sum(c => c.Amount);
        
        return resource.CanAfford(new ResourceCost { ResourceType = GetResourceType(resource), Amount = totalCost });
    }
    
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
            return false;
        }
        
        return false;
    }
    
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
                projectedValue += operation.Amount;
                outcome.Operations.Add(operation);
            }
        }
        
        projectedValue = Mathf.Clamp(projectedValue, 0, resource.MaxValue);
        
        outcome.ProjectedValue = projectedValue;
        outcome.ProjectedPercentage = resource.MaxValue > 0 ? (float)projectedValue / resource.MaxValue : 0f;
        outcome.ProjectedHealth = GetHealthFromPercentage(outcome.ProjectedPercentage);
        
        int healthChange = (int)outcome.ProjectedHealth - (int)outcome.InitialHealth;
        outcome.HealthChange = healthChange;
        
        outcome.IsImprovement = outcome.ProjectedValue > resource.CurrentValue;
        outcome.IsCriticalChange = Math.Abs(healthChange) >= 2;
        
        return outcome;
    }
    
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
                return false;
            }
        }
        
        return true;
    }
    
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
    
    public static ResourcePortfolio OptimizePortfolio(this IEnumerable<Resource> resources, IEnumerable<ResourceCost> plannedCosts)
    {
        var portfolio = new ResourcePortfolio();
        
        var validResources = resources?.Where(r => r != null).ToList() ?? new List<Resource>();
        var validCosts = plannedCosts?.Where(c => c != null).ToList() ?? new List<ResourceCost>();
        
        portfolio.Resources = validResources;
        portfolio.TotalValue = validResources.Sum(r => r.CurrentValue);
        portfolio.OverallHealth = validResources.GetOverallHealth();
        
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
    
    private static int GetRecommendedReserve(Resource resource)
    {
        return resource.GetResourceHealth() switch
        {
            ResourceHealth.Excellent => resource.MaxValue / 10,
            ResourceHealth.Good => resource.MaxValue / 8,
            ResourceHealth.Moderate => resource.MaxValue / 6,
            ResourceHealth.Low => resource.MaxValue / 4,
            _ => resource.MaxValue / 3
        };
    }
    
    private static ResourceType GetResourceType(Resource resource)
    {
        return ResourceType.Creativity;
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
    public int Amount { get; set; }
    public string Description { get; set; } = "";
    public float Probability { get; set; } = 1f;
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
    public int HealthChange { get; set; }
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