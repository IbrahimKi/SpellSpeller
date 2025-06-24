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
        
        this.TryWithManager<DeckManager>(dm => 
        {
            if (dm.DeckSize == 0)
                dm.GenerateTestDeck();
        });
        yield return null;
        
        if (startingHandSize > 0)
            yield return DrawCards(startingHandSize);
        
        this.TryWithManager<EnemyManager>(em => 
        {
            var firstEnemy = em.GetSmartTarget(TargetingStrategy.Optimal);
            if (firstEnemy != null)
                _currentTargets.Add(firstEnemy);
        });
        
        OnLifeChanged?.Invoke(_life);
        OnCreativityChanged?.Invoke(_creativity);
        OnDeckSizeChanged?.Invoke(DeckSize);
        OnTurnChanged?.Invoke(_currentTurn);
        OnTurnPhaseChanged?.Invoke(_currentPhase);
        OnCombatStarted?.Invoke();
        OnPlayerTurnStarted?.Invoke(_currentTurn);
    }
    
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
        
        // Smart resource recovery using extensions
        var situation = this.GetCombatSituation();
        if (situation.UrgencyLevel >= UrgencyLevel.High)
        {
            this.TryOptimalRecovery(ResourceType.Creativity, _creativity.MaxValue);
        }
        else
        {
            _creativity.Reset();
        }
        OnCreativityChanged?.Invoke(_creativity);
        
        int cardsToRefill = Mathf.Max(0, startingHandSize - this.TryWithManager<CardManager, int>(cm => cm.HandSize));
        if (cardsToRefill > 0)
            yield return DrawCards(cardsToRefill);
        
        yield return new WaitForSeconds(turnTransitionDelay);
        
        _currentPhase = TurnPhase.EnemyTurn;
        _isProcessingTurn = true;
        OnTurnPhaseChanged?.Invoke(_currentPhase);
        OnEnemyTurnStarted?.Invoke(_currentTurn);
        
        yield return ProcessEnemyTurn();
        
        _currentTurn++;
        OnTurnChanged?.Invoke(_currentTurn);
        
        _currentPhase = TurnPhase.PlayerTurn;
        _isProcessingTurn = false;
        OnTurnPhaseChanged?.Invoke(_currentPhase);
        OnPlayerTurnStarted?.Invoke(_currentTurn);
    }
    
    private IEnumerator ProcessEnemyTurn()
    {
        yield return new WaitForSeconds(1f);
        
        if (_life.CurrentValue > 0)
        {
            var difficulty = this.GetCombatDifficulty();
            var situation = this.GetCombatSituation();
            
            int baseDamage = difficulty switch
            {
                CombatDifficulty.Easy => UnityEngine.Random.Range(3, 8),
                CombatDifficulty.Moderate => UnityEngine.Random.Range(5, 12),
                CombatDifficulty.Hard => UnityEngine.Random.Range(8, 18),
                CombatDifficulty.Desperate => UnityEngine.Random.Range(12, 25),
                _ => UnityEngine.Random.Range(5, 15)
            };
            
            if (situation.HealthStatus <= HealthStatus.Critical)
                baseDamage = Mathf.RoundToInt(baseDamage * 0.7f);
            
            ModifyLife(-baseDamage);
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    private IEnumerator DrawCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (this.TryWithManager<DeckManager>(dm => dm.TryDrawCard()))
                yield return null;
        }
    }
    
    public void ModifyLife(int delta)
    {
        int oldValue = _life.CurrentValue;
        _life.ModifyBy(delta);
        
        if (_life.CurrentValue != oldValue)
        {
            OnLifeChanged?.Invoke(_life);
            
            if (_life.IsExhausted())
                OnPlayerDeath?.Invoke();
        }
    }
    
    public void ModifyCreativity(int delta)
    {
        int oldValue = _creativity.CurrentValue;
        _creativity.ModifyBy(delta);
        
        if (_creativity.CurrentValue != oldValue)
            OnCreativityChanged?.Invoke(_creativity);
    }
    
    // INTEGRATION: Simplified using ResourceExtensions
    public bool CanSpendCreativity(int amount) 
        => _creativity.CanAfford(new ResourceCost { ResourceType = ResourceType.Creativity, Amount = amount });

    public bool SpendCreativity(int amount)
        => _creativity.TryApplyCost(new ResourceCost { ResourceType = ResourceType.Creativity, Amount = amount });
    
    public void AddTarget(EntityBehaviour target)
    {
        if (target != null && target.IsAlive && !_currentTargets.Contains(target))
        {
            _currentTargets.Clear();
            _currentTargets.Add(target);
        }
    }
    
    public void AddSmartTarget(TargetingStrategy strategy = TargetingStrategy.Optimal)
        => this.TryWithManager<EnemyManager>(em => 
        {
            var smartTarget = em.GetSmartTarget(strategy);
            if (smartTarget != null)
                AddTarget(smartTarget);
        });
    
    public void DealDamageToTargets(int damage, DamageType damageType = DamageType.Normal)
        => this.TryWithManager<EnemyManager>(em => em.DamageTargetedEnemies(damage));
    
    public bool TrySmartHealing(int healAmount, HealingMode mode = HealingMode.Self)
        => this.TrySmartHeal(healAmount, mode);
    
    private void HandleEnemyKilled(EntityBehaviour enemy)
    {
        _currentTargets.Remove(enemy);
        
        if (_currentTargets.Count == 0)
            AddSmartTarget(TargetingStrategy.Optimal);
    }
    
    private void HandleAllEnemiesDefeated()
    {
        _currentTargets.Clear();
        EndCombat();
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
        var portfolio = this.GetResourcePortfolio();
        Debug.Log($"[CombatManager] Resource Analysis:");
        Debug.Log($"  Life: {_life.GetResourceHealth()} ({_life.Percentage:P0})");
        Debug.Log($"  Creativity: {_creativity.GetResourceHealth()} ({_creativity.Percentage:P0})");
        Debug.Log($"  Overall Health Score: {portfolio.OverallHealth.HealthScore:P0}");
    }
    
    [ContextMenu("Get Combat Assessment")]
    public void DebugCombatAssessment()
    {
        var assessment = this.GetCombatAssessment();
        Debug.Log($"[CombatManager] Combat Assessment:");
        Debug.Log($"  Difficulty: {assessment.Difficulty}");
        Debug.Log($"  Health Status: {assessment.Situation.HealthStatus}");
        Debug.Log($"  Recommended Action: {assessment.Situation.RecommendedAction}");
    }
#endif
}