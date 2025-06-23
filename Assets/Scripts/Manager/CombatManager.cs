using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Linq;

[System.Serializable]
public class Resource
{
    [SerializeField] private int _currentValue;
    [SerializeField] private int _maxValue;
    
    public int CurrentValue => _currentValue;
    public int MaxValue => _maxValue;
    public float Percentage => _maxValue > 0 ? (float)_currentValue / _maxValue : 0f;
    
    public Resource(int startValue, int maxValue = -1)
    {
        _currentValue = startValue;
        _maxValue = maxValue > 0 ? maxValue : startValue;
    }
    
    public void SetCurrent(int value) => _currentValue = Mathf.Clamp(value, 0, _maxValue);
    public void ModifyBy(int delta) => SetCurrent(_currentValue + delta);
    public void Reset() => _currentValue = _maxValue;
}

public enum TurnPhase
{
    PlayerTurn,
    EnemyTurn,
    TurnTransition,
    CombatEnd
}

public class CombatManager : SingletonBehaviour<CombatManager>, IGameManager
{
    [Header("Resources")]
    [SerializeField] private int startLife = 100;
    [SerializeField] private int startCreativity = 3;
    [SerializeField] private int maxCreativity = 10;
    
    [Header("Combat Settings")]
    [SerializeField] private int startingHandSize = 5;
    [SerializeField] private float turnTransitionDelay = 0.5f;
    
    // Resources
    private Resource _life;
    private Resource _creativity;
    
    // Turn state
    private int _currentTurn = 1;
    private TurnPhase _currentPhase = TurnPhase.PlayerTurn;
    private bool _isProcessingTurn = false;
    private bool _isInCombat = false;
    private bool _isReady = false;
    
    // Targeting
    private List<EntityBehaviour> _currentTargets = new List<EntityBehaviour>();
    
    // Events
    public static event Action<Resource> OnLifeChanged;
    public static event Action<Resource> OnCreativityChanged;
    public static event Action<int> OnDeckSizeChanged;
    public static event Action<int> OnTurnChanged;
    public static event Action<TurnPhase> OnTurnPhaseChanged;
    public static event Action OnCombatStarted;
    public static event Action OnCombatEnded;
    public static event Action OnPlayerDeath;
    public static event Action<int> OnPlayerTurnStarted;
    public static event Action<int> OnPlayerTurnEnded;
    public static event Action<int> OnEnemyTurnStarted;
    
    // Properties
    public bool IsReady => _isReady;
    public bool IsInCombat => _isInCombat;
    public bool IsProcessingTurn => _isProcessingTurn;
    public Resource Life => _life;
    public Resource Creativity => _creativity;
    public int CurrentTurn => _currentTurn;
    public TurnPhase CurrentPhase => _currentPhase;
    public int DeckSize => this.TryWithManager<DeckManager, int>(dm => dm.DeckSize);
    public int DiscardSize => this.TryWithManager<DeckManager, int>(dm => dm.DiscardSize);
    public IReadOnlyList<EntityBehaviour> CurrentTargets => _currentTargets.AsReadOnly();
    public bool CanEndTurn => _isInCombat && _currentPhase == TurnPhase.PlayerTurn && !_isProcessingTurn;
    public bool IsPlayerTurn => _currentPhase == TurnPhase.PlayerTurn;
    
    protected override void OnAwakeInitialize()
    {
        InitializeResources();
        _currentPhase = TurnPhase.PlayerTurn;
        _isReady = true;
    }
    
    private void OnEnable()
    {
        if (EnemyManager.HasInstance)
        {
            EnemyManager.OnEnemyKilled += HandleEnemyKilled;
            EnemyManager.OnAllEnemiesDefeated += HandleAllEnemiesDefeated;
        }
        
        this.TryWithManager<DeckManager>(dm => 
        {
            DeckManager.OnDeckSizeChanged += size => OnDeckSizeChanged?.Invoke(size);
        });
    }
    
    private void OnDisable()
    {
        if (EnemyManager.HasInstance)
        {
            EnemyManager.OnEnemyKilled -= HandleEnemyKilled;
            EnemyManager.OnAllEnemiesDefeated -= HandleAllEnemiesDefeated;
        }
    }
    
    private void InitializeResources()
    {
        _life = new Resource(startLife);
        _creativity = new Resource(startCreativity, maxCreativity);
    }
    
    public void StartCombat()
    {
        if (_isInCombat) return;
        StartCoroutine(StartCombatSequence());
    }
    
    private IEnumerator StartCombatSequence()
    {
        _isInCombat = true;
        _currentTurn = 1;
        _currentPhase = TurnPhase.PlayerTurn;
        _isProcessingTurn = false;
        
        // INTEGRATION: Use ManagerExtensions for safe deck check
        this.TryWithManager<DeckManager>(dm => 
        {
            if (dm.DeckSize == 0)
            {
                dm.GenerateTestDeck();
            }
        });
        yield return null;
        
        // Draw starting hand using ManagerExtensions
        if (startingHandSize > 0)
        {
            yield return DrawCards(startingHandSize);
        }
        
        // Auto-target first enemy using ManagerExtensions
        this.TryWithManager<EnemyManager>(em => 
        {
            var firstEnemy = em.AliveEnemies.FirstOrDefault();
            if (firstEnemy != null)
                _currentTargets.Add(firstEnemy);
        });
        
        // Fire events
        OnLifeChanged?.Invoke(_life);
        OnCreativityChanged?.Invoke(_creativity);
        OnDeckSizeChanged?.Invoke(DeckSize);
        OnTurnChanged?.Invoke(_currentTurn);
        OnTurnPhaseChanged?.Invoke(_currentPhase);
        OnCombatStarted?.Invoke();
        OnPlayerTurnStarted?.Invoke(_currentTurn);
    }
    
    // INTEGRATION: Enhanced turn management with CombatExtensions
    public void EndPlayerTurn()
    {
        if (!this.CanEndTurnSafely()) return;
        
        OnPlayerTurnEnded?.Invoke(_currentTurn);
        StartCoroutine(ProcessTurnTransition());
    }
    
    private IEnumerator ProcessTurnTransition()
    {
        _currentPhase = TurnPhase.TurnTransition;
        _isProcessingTurn = true;
        OnTurnPhaseChanged?.Invoke(_currentPhase);
        
        // INTEGRATION: Smart resource reset using CombatExtensions
        var situation = this.GetCombatSituation();
        if (situation.UrgencyLevel >= UrgencyLevel.High)
        {
            // Emergency resource boost in critical situations
            int emergencyBoost = Mathf.RoundToInt(_creativity.MaxValue * 0.2f);
            _creativity.Reset();
            _creativity.ModifyBy(emergencyBoost);
            Debug.Log($"[CombatManager] Emergency creativity boost: +{emergencyBoost}");
        }
        else
        {
            _creativity.Reset();
        }
        OnCreativityChanged?.Invoke(_creativity);
        
        // INTEGRATION: Smart hand refill using ManagerExtensions + CombatExtensions
        int cardsToRefill = Mathf.Max(0, startingHandSize - this.TryWithManager<CardManager, int>(cm => cm.HandSize));
        if (cardsToRefill > 0)
        {
            yield return DrawCards(cardsToRefill);
        }
        
        yield return new WaitForSeconds(turnTransitionDelay);
        
        // Start enemy turn
        _currentPhase = TurnPhase.EnemyTurn;
        _isProcessingTurn = true;
        OnTurnPhaseChanged?.Invoke(_currentPhase);
        OnEnemyTurnStarted?.Invoke(_currentTurn);
        
        // INTEGRATION: Smart enemy action using CombatExtensions
        yield return ProcessEnemyTurn();
        
        // Next turn
        _currentTurn++;
        OnTurnChanged?.Invoke(_currentTurn);
        
        // Back to player turn
        _currentPhase = TurnPhase.PlayerTurn;
        _isProcessingTurn = false;
        OnTurnPhaseChanged?.Invoke(_currentPhase);
        OnPlayerTurnStarted?.Invoke(_currentTurn);
    }
    
    // INTEGRATION: Enhanced enemy turn processing with CombatExtensions
    private IEnumerator ProcessEnemyTurn()
    {
        yield return new WaitForSeconds(1f);
        
        if (_life.CurrentValue > 0)
        {
            var difficulty = this.GetCombatDifficulty();
            var situation = this.GetCombatSituation();
            
            // Scale damage based on combat difficulty and situation
            int baseDamage = difficulty switch
            {
                CombatDifficulty.Easy => UnityEngine.Random.Range(3, 8),
                CombatDifficulty.Moderate => UnityEngine.Random.Range(5, 12),
                CombatDifficulty.Hard => UnityEngine.Random.Range(8, 18),
                CombatDifficulty.Desperate => UnityEngine.Random.Range(12, 25),
                _ => UnityEngine.Random.Range(5, 15)
            };
            
            // Reduce damage if player is in critical state (mercy mechanic)
            if (situation.HealthStatus <= HealthStatus.Critical)
            {
                baseDamage = Mathf.RoundToInt(baseDamage * 0.7f);
                Debug.Log("[CombatManager] Enemy shows mercy - reduced damage");
            }
            
            ModifyLife(-baseDamage);
            yield return new WaitForSeconds(0.5f);
            
            // INTEGRATION: Auto-trigger emergency recovery if needed
            if (situation.UrgencyLevel >= UrgencyLevel.Critical)
            {
                TryEmergencyRecovery();
            }
        }
    }
    
    private IEnumerator DrawCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            // INTEGRATION: Use ManagerExtensions for safe card drawing
            if (this.TryWithManager<DeckManager>(dm => 
            {
                var cardData = dm.DrawCard();
                if (cardData != null)
                {
                    this.TryWithManager<CardManager>(cm => cm.SpawnCard(cardData, null, true));
                }
            }))
            {
                yield return null;
            }
        }
    }
    
    // INTEGRATION: Enhanced resource modification with ResourceExtensions
    public void ModifyLife(int delta)
    {
        int oldValue = _life.CurrentValue;
        _life.ModifyBy(delta);
        
        if (_life.CurrentValue != oldValue)
        {
            OnLifeChanged?.Invoke(_life);
            
            // INTEGRATION: Use ResourceExtensions for health assessment
            if (_life.IsExhausted())
            {
                OnPlayerDeath?.Invoke();
            }
            else if (_life.IsInCriticalState())
            {
                Debug.LogWarning("[CombatManager] Life is in critical state!");
            }
        }
    }
    
    public void ModifyCreativity(int delta)
    {
        int oldValue = _creativity.CurrentValue;
        _creativity.ModifyBy(delta);
        
        if (_creativity.CurrentValue != oldValue)
        {
            OnCreativityChanged?.Invoke(_creativity);
            
            // INTEGRATION: Use ResourceExtensions for creativity assessment
            if (_creativity.IsInCriticalState())
            {
                Debug.LogWarning("[CombatManager] Creativity is running low!");
            }
        }
    }
    
    // INTEGRATION: Enhanced spending validation with ResourceExtensions
    public bool CanSpendCreativity(int amount) 
    {
        var cost = new ResourceCost 
        { 
            ResourceType = ResourceType.Creativity, 
            Amount = amount,
            Priority = ResourcePriority.Medium
        };
        return _creativity.CanAfford(cost);
    }
    
    public bool SpendCreativity(int amount)
    {
        var cost = new ResourceCost 
        { 
            ResourceType = ResourceType.Creativity, 
            Amount = amount,
            Priority = ResourcePriority.Medium
        };
        
        return _creativity.TryApplyCost(cost);
    }
    
    // INTEGRATION: Enhanced targeting with CombatExtensions
    public void AddTarget(EntityBehaviour target)
    {
        if (target != null && target.IsAlive && !_currentTargets.Contains(target))
        {
            _currentTargets.Clear(); // Single target for now
            _currentTargets.Add(target);
        }
    }
    
    // INTEGRATION: Smart targeting using CombatExtensions
    public void AddSmartTarget(TargetingStrategy strategy = TargetingStrategy.Optimal)
    {
        this.TryWithManager<EnemyManager>(em => 
        {
            var smartTarget = em.GetSmartTarget(strategy);
            if (smartTarget != null)
            {
                AddTarget(smartTarget);
            }
        });
    }
    
    // INTEGRATION: Enhanced damage dealing with CombatExtensions
    public void DealDamageToTargets(int damage, DamageType damageType = DamageType.Normal)
    {
        foreach (var target in _currentTargets.ToList())
        {
            if (target != null && target.IsAlive)
            {
                // INTEGRATION: Use EntityExtensions for enhanced damage
                var result = target.TryDamageWithEffects(damage, damageType, true);
                if (result.Success)
                {
                    Debug.Log($"[CombatManager] Dealt {result.DamageDealt} {damageType} damage to {target.EntityName}");
                }
            }
        }
    }
    
    // INTEGRATION: Smart healing using CombatExtensions
    public bool TrySmartHealing(int healAmount, HealingMode mode = HealingMode.Self)
    {
        return this.TrySmartHeal(healAmount, mode);
    }
    
    private void HandleEnemyKilled(EntityBehaviour enemy)
    {
        _currentTargets.Remove(enemy);
        
        // INTEGRATION: Use ManagerExtensions for safe auto-targeting
        if (_currentTargets.Count == 0)
        {
            this.TryWithManager<EnemyManager>(em => 
            {
                var nextEnemy = em.AliveEnemies.FirstOrDefault();
                if (nextEnemy != null)
                    AddTarget(nextEnemy);
            });
        }
    }
    
    private void HandleAllEnemiesDefeated()
    {
        _currentTargets.Clear();
        // Victory handling
    }
    
    // INTEGRATION: New resource management methods using ResourceExtensions
    
    /// <summary>
    /// Get comprehensive resource status
    /// </summary>
    public ResourcePortfolio GetResourcePortfolio()
    {
        var resources = new[] { _life, _creativity };
        return resources.OptimizePortfolio(GetPlannedResourceCosts());
    }
    
    /// <summary>
    /// Get resource health overview
    /// </summary>
    public OverallResourceHealth GetOverallResourceHealth()
    {
        var resources = new[] { _life, _creativity };
        return resources.GetOverallHealth();
    }
    
    /// <summary>
    /// Plan resource usage for upcoming actions
    /// </summary>
    public ResourceBudget PlanResourceBudget(int plannedCreativitySpending)
    {
        return _creativity.CreateBudget(plannedCreativitySpending);
    }
    
    /// <summary>
    /// Get resource recommendations
    /// </summary>
    public List<ResourceRecommendation> GetResourceRecommendations()
    {
        var portfolio = GetResourcePortfolio();
        return portfolio.Recommendations;
    }
    
    /// <summary>
    /// Try emergency resource recovery
    /// </summary>
    public bool TryEmergencyRecovery()
    {
        bool recoveryNeeded = false;
        
        // Life emergency recovery
        if (_life.IsInCriticalState())
        {
            int optimalRecovery = _life.GetOptimalRecovery(_life.MaxValue / 3);
            if (optimalRecovery > 0)
            {
                _life.ModifyBy(optimalRecovery);
                Debug.Log($"[CombatManager] Emergency life recovery: +{optimalRecovery}");
                recoveryNeeded = true;
            }
        }
        
        // Creativity emergency recovery  
        if (_creativity.IsInCriticalState())
        {
            int optimalRecovery = _creativity.GetOptimalRecovery(_creativity.MaxValue / 2);
            if (optimalRecovery > 0)
            {
                _creativity.ModifyBy(optimalRecovery);
                Debug.Log($"[CombatManager] Emergency creativity recovery: +{optimalRecovery}");
                recoveryNeeded = true;
            }
        }
        
        return recoveryNeeded;
    }
    
    /// <summary>
    /// Predict turn outcome with planned operations
    /// </summary>
    public TurnOutcomePrediction PredictTurnOutcome(List<PlannedAction> plannedActions)
    {
        var prediction = new TurnOutcomePrediction();
        
        // Convert planned actions to resource operations
        var lifeOperations = new List<ResourceOperation>();
        var creativityOperations = new List<ResourceOperation>();
        
        foreach (var action in plannedActions ?? new List<PlannedAction>())
        {
            if (action.LifeCost > 0)
            {
                lifeOperations.Add(new ResourceOperation 
                { 
                    ResourceType = ResourceType.Life, 
                    Amount = -action.LifeCost,
                    Description = action.Description
                });
            }
            
            if (action.CreativityCost > 0)
            {
                creativityOperations.Add(new ResourceOperation 
                { 
                    ResourceType = ResourceType.Creativity, 
                    Amount = -action.CreativityCost,
                    Description = action.Description
                });
            }
            
            if (action.LifeGain > 0)
            {
                lifeOperations.Add(new ResourceOperation 
                { 
                    ResourceType = ResourceType.Life, 
                    Amount = action.LifeGain,
                    Description = $"Heal from {action.Description}"
                });
            }
        }
        
        // Add turn reset for creativity
        creativityOperations.Add(new ResourceOperation 
        { 
            ResourceType = ResourceType.Creativity, 
            Amount = _creativity.MaxValue - _creativity.CurrentValue,
            Description = "Turn reset"
        });
        
        // Predict outcomes
        prediction.LifeOutcome = _life.PredictOutcome(lifeOperations);
        prediction.CreativityOutcome = _creativity.PredictOutcome(creativityOperations);
        
        // Overall assessment
        prediction.IsRisky = prediction.LifeOutcome.IsRisky || 
                           prediction.CreativityOutcome.IsRisky;
        
        prediction.Recommendation = prediction.IsRisky ? 
            "Risky turn - consider safer actions" : 
            "Turn plan looks safe";
        
        return prediction;
    }
    
    /// <summary>
    /// Get current planned resource costs
    /// </summary>
    private List<ResourceCost> GetPlannedResourceCosts()
    {
        var costs = new List<ResourceCost>();
        
        // Add typical turn costs
        costs.Add(ResourceCost.Create(ResourceType.Creativity, 2, ResourcePriority.Medium)); // Typical card play
        costs.Add(ResourceCost.Create(ResourceType.Creativity, 1, ResourcePriority.Low));    // Optional draw
        
        return costs;
    }
    
    /// <summary>
    /// Smart resource allocation for optimal play
    /// </summary>
    public ResourceAllocationPlan GetOptimalResourceAllocation()
    {
        var plan = new ResourceAllocationPlan();
        var creativityBudget = _creativity.CreateBudget(3); // Plan for 3 creativity spending
        
        plan.CanExecutePlan = creativityBudget.CanExecutePlan;
        plan.AvailableCreativity = creativityBudget.AvailableForSpending;
        plan.RecommendedReserve = creativityBudget.EmergencyReserve;
        
        // Prioritize actions based on resource state
        var healthStatus = _life.GetResourceHealth();
        var creativityStatus = _creativity.GetResourceHealth();
        
        if (healthStatus <= ResourceHealth.Critical)
        {
            plan.PriorityAction = "Focus on healing - life critical";
            plan.MaxSpendingAllowed = 1; // Conservative spending
        }
        else if (creativityStatus <= ResourceHealth.Low)
        {
            plan.PriorityAction = "Conserve creativity - consider drawing cards";
            plan.MaxSpendingAllowed = 2;
        }
        else
        {
            plan.PriorityAction = "Good resource state - play aggressively";
            plan.MaxSpendingAllowed = creativityBudget.AvailableForSpending;
        }
        
        return plan;
    }
    
    // INTEGRATION: Advanced combat state assessment
    public CombatAssessment GetCombatAssessment()
    {
        var assessment = new CombatAssessment();
        
        // Basic situation
        assessment.Situation = this.GetCombatSituation();
        assessment.Difficulty = this.GetCombatDifficulty();
        assessment.ResourcePortfolio = GetResourcePortfolio();
        
        // Enemy analysis
        this.TryWithManager<EnemyManager>(em => 
        {
            assessment.EnemyCount = em.AliveEnemyCount;
            assessment.HasBossEnemy = em.AliveEnemies.Any(e => e.IsBoss());
            assessment.HasEliteEnemies = em.AliveEnemies.Any(e => e.IsElite());
            assessment.AverageEnemyHealth = em.AliveEnemies.Any() ? 
                em.AliveEnemies.Average(e => e.HealthPercentage) : 0f;
        });
        
        // Strategic recommendations
        assessment.RecommendedActions = GetStrategicRecommendations(assessment);
        
        return assessment;
    }
    
    private List<ActionRecommendation> GetStrategicRecommendations(CombatAssessment assessment)
    {
        var recommendations = new List<ActionRecommendation>();
        
        // Health-based recommendations
        if (assessment.Situation.HealthStatus <= HealthStatus.Critical)
        {
            recommendations.Add(new ActionRecommendation
            {
                Action = ActionType.Heal,
                Priority = ActionPriority.Critical,
                Description = "Emergency healing required",
                Urgency = UrgencyLevel.Critical
            });
        }
        
        // Resource-based recommendations
        if (assessment.Situation.CreativityStatus <= ResourceStatus.Low)
        {
            recommendations.Add(new ActionRecommendation
            {
                Action = ActionType.DrawCard,
                Priority = ActionPriority.High,
                Description = "Draw cards to restore creativity options",
                Urgency = UrgencyLevel.High
            });
        }
        
        // Enemy-based recommendations
        if (assessment.EnemyCount > 2 && assessment.Difficulty >= CombatDifficulty.Hard)
        {
            recommendations.Add(new ActionRecommendation
            {
                Action = ActionType.Attack,
                Priority = ActionPriority.High,
                Description = "Focus fire to reduce enemy numbers",
                Urgency = UrgencyLevel.High
            });
        }
        
        // Defensive recommendations
        if (assessment.HasBossEnemy && assessment.Situation.HealthStatus <= HealthStatus.Moderate)
        {
            recommendations.Add(new ActionRecommendation
            {
                Action = ActionType.Defend,
                Priority = ActionPriority.Medium,
                Description = "Consider defensive spells against boss",
                Urgency = UrgencyLevel.Medium
            });
        }
        
        return recommendations.OrderByDescending(r => r.Priority).ToList();
    }
    
    /// <summary>
    /// Combat performance metrics
    /// </summary>
    public CombatMetrics GetCombatMetrics()
    {
        var metrics = new CombatMetrics();
        
        metrics.TurnsElapsed = _currentTurn;
        metrics.LifePercentage = _life.Percentage;
        metrics.CreativityPercentage = _creativity.Percentage;
        metrics.CombatDuration = Time.time; // Would need actual start time tracking
        
        // Performance calculations
        this.TryWithManager<EnemyManager>(em => 
        {
            metrics.EnemiesDefeated = 0; // Would need tracking
            metrics.TotalEnemies = em.EnemyCount;
            metrics.CombatEfficiency = metrics.EnemiesDefeated / (float)Mathf.Max(1, metrics.TurnsElapsed);
        });
        
        // Resource efficiency
        var portfolio = GetResourcePortfolio();
        metrics.ResourceEfficiency = portfolio.OverallHealth.HealthScore;
        
        return metrics;
    }
    
    public void EndCombat()
    {
        _isInCombat = false;
        _currentTargets.Clear();
        _currentPhase = TurnPhase.CombatEnd;
        OnCombatEnded?.Invoke();
    }

#if UNITY_EDITOR
    [ContextMenu("Analyze Resources")]
    public void DebugAnalyzeResources()
    {
        var portfolio = GetResourcePortfolio();
        Debug.Log($"[CombatManager] Resource Analysis:");
        Debug.Log($"  Life: {_life.GetResourceHealth()} ({_life.Percentage:P0})");
        Debug.Log($"  Creativity: {_creativity.GetResourceHealth()} ({_creativity.Percentage:P0})");
        Debug.Log($"  Overall Health Score: {portfolio.OverallHealth.HealthScore:P0}");
        
        if (portfolio.OverallHealth.IsInCrisis)
        {
            Debug.LogWarning("  RESOURCE CRISIS DETECTED!");
        }
        
        foreach (var rec in portfolio.Recommendations)
        {
            Debug.Log($"  Recommendation: {rec.RecommendedAction} for {rec.ResourceType}");
        }
    }
    
    [ContextMenu("Test Emergency Recovery")]
    public void DebugEmergencyRecovery()
    {
        bool recovered = TryEmergencyRecovery();
        Debug.Log($"[CombatManager] Emergency recovery: {recovered}");
    }
    
    [ContextMenu("Get Resource Allocation Plan")]
    public void DebugResourceAllocation()
    {
        var plan = GetOptimalResourceAllocation();
        Debug.Log($"[CombatManager] Resource Allocation Plan:");
        Debug.Log($"  Can Execute: {plan.CanExecutePlan}");
        Debug.Log($"  Available Creativity: {plan.AvailableCreativity}");
        Debug.Log($"  Max Spending: {plan.MaxSpendingAllowed}");
        Debug.Log($"  Priority: {plan.PriorityAction}");
    }
    
    [ContextMenu("Get Combat Assessment")]
    public void DebugCombatAssessment()
    {
        var assessment = GetCombatAssessment();
        Debug.Log($"[CombatManager] Combat Assessment:");
        Debug.Log($"  Difficulty: {assessment.Difficulty}");
        Debug.Log($"  Health Status: {assessment.Situation.HealthStatus}");
        Debug.Log($"  Can Act: {assessment.Situation.CanAct}");
        Debug.Log($"  Enemy Count: {assessment.EnemyCount}");
        Debug.Log($"  Recommended Action: {assessment.Situation.RecommendedAction}");
        Debug.Log($"  Urgency: {assessment.Situation.UrgencyLevel}");
        
        foreach (var rec in assessment.RecommendedActions)
        {
            Debug.Log($"    - {rec.Action}: {rec.Description} (Priority: {rec.Priority})");
        }
    }
    
    [ContextMenu("Test Smart Targeting")]
    public void DebugSmartTargeting()
    {
        AddSmartTarget(TargetingStrategy.Weakest);
        Debug.Log($"[CombatManager] Smart target selected: {(CurrentTargets.FirstOrDefault()?.EntityName ?? "None")}");
    }
    
    [ContextMenu("Get Combat Metrics")]
    public void DebugCombatMetrics()
    {
        var metrics = GetCombatMetrics();
        Debug.Log($"[CombatManager] Combat Metrics:");
        Debug.Log($"  Turns: {metrics.TurnsElapsed}");
        Debug.Log($"  Life: {metrics.LifePercentage:P0}");
        Debug.Log($"  Creativity: {metrics.CreativityPercentage:P0}");
        Debug.Log($"  Resource Efficiency: {metrics.ResourceEfficiency:P0}");
        Debug.Log($"  Combat Efficiency: {metrics.CombatEfficiency:F2}");
    }
#endif
}

// INTEGRATION: Supporting classes for CombatExtensions
[System.Serializable]
public class CombatAssessment
{
    public CombatSituation Situation;
    public CombatDifficulty Difficulty;
    public ResourcePortfolio ResourcePortfolio;
    public int EnemyCount;
    public bool HasBossEnemy;
    public bool HasEliteEnemies;
    public float AverageEnemyHealth;
    public List<ActionRecommendation> RecommendedActions;
}

[System.Serializable]
public class ActionRecommendation
{
    public ActionType Action;
    public ActionPriority Priority;
    public string Description;
    public UrgencyLevel Urgency;
}

[System.Serializable]
public class CombatMetrics
{
    public int TurnsElapsed;
    public float LifePercentage;
    public float CreativityPercentage;
    public float CombatDuration;
    public int EnemiesDefeated;
    public int TotalEnemies;
    public float CombatEfficiency;
    public float ResourceEfficiency;
}

[System.Serializable]
public class PlannedAction
{
    public string Description;
    public int CreativityCost;
    public int LifeCost;
    public int LifeGain;
    public ActionPriority Priority;
}

[System.Serializable]
public class TurnOutcomePrediction
{
    public ResourceOutcome LifeOutcome;
    public ResourceOutcome CreativityOutcome;
    public bool IsRisky;
    public string Recommendation;
}

[System.Serializable]
public class ResourceAllocationPlan
{
    public bool CanExecutePlan;
    public int AvailableCreativity;
    public int RecommendedReserve;
    public int MaxSpendingAllowed;
    public string PriorityAction;
}

public enum TargetingMode
{
    Single,
    Multiple,
    All,
    Random
}

public enum DamageType
{
    Normal,
    Fire,
    True
}